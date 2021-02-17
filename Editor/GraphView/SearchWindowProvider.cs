using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{

    class SearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        internal TextureGraph graphView;
        static readonly Dictionary<Type, List<(Type, NodeMenuAttribute)>> s_NodesCache = new Dictionary<Type, List<(Type, NodeMenuAttribute)>>();

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));
            List<(Type, NodeMenuAttribute)> nodeTypes;
            if (!s_NodesCache.TryGetValue(typeof(TextureGraph), out nodeTypes))
            {
                nodeTypes = new List<(Type, NodeMenuAttribute)>();
                foreach(var type in GetType().Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(GraphElement)) && !type.IsAbstract))
                {
                    var attrs = type.GetCustomAttributes<NodeMenuAttribute>().Where(attr => attr.GraphViewType == null || attr .GraphViewType == typeof(TextureGraph));
                    if (attrs.Count() == 0)
                        continue;
                    nodeTypes.Add((type, attrs.First()));
                }
                nodeTypes.Sort((x, y) => string.Compare(x.Item2.Path, y.Item2.Path));
                s_NodesCache[typeof(TextureGraph)] = nodeTypes;
            }
            var groupPaths = new HashSet<string>();
            foreach(var type in nodeTypes)
            {
                var names = new Queue<string>(type.Item2.Path.Split('/'));
                var level = 1;
                while(names.Count > 1)
                {
                    var entryName = names.Dequeue();
                    if(groupPaths.Add(entryName))
                    {
                        entries.Add(new SearchTreeGroupEntry(new GUIContent(entryName), level));
                    }
                    ++level;
                }
                entries.Add(new SearchTreeEntry(new GUIContent(names.Dequeue())) { level = level, userData = type.Item1 });
            }
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var type = entry.userData as Type;
            var newElement = Activator.CreateInstance(type, true);
            if (newElement is Node node)
            {
                graphView.AddElementWithRecord(node);
                var rect = new Rect();
                var root = graphView.window.GetRootVisualContainer();
                rect.position = graphView.contentViewContainer.WorldToLocal(root.ChangeCoordinatesTo(root.parent, context.screenMousePosition - graphView.window.position.position));
                node.SetPosition(rect);
                return true;
            }
            else if (newElement is ISerializableGraphElement serializableElement)
            {
                graphView.AddElementWithRecord(serializableElement as GraphElement);
                return true;
            }
            return false;
        }
    }

}
