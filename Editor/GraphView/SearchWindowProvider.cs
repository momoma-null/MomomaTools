using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    sealed class SearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        static readonly Dictionary<Type, ConstructorInfo> s_ConstructorInfos = new Dictionary<Type, ConstructorInfo>();

        public event Action<GraphElement, Vector2> addGraphElement;
        public Type graphViewType { get; set; } = typeof(GraphView);

        List<SearchTreeEntry> m_SearchTree;

        void Awake()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            if (m_SearchTree != null)
                return m_SearchTree;
            m_SearchTree = new List<SearchTreeEntry>();
            m_SearchTree.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));
            var nodeTypes = new List<(Type, NodeMenuAttribute)>();
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(asm => asm.GetTypes()).Where(type => type.IsSubclassOf(typeof(GraphElement)) && !type.IsAbstract))
            {
                var attrs = type.GetCustomAttributes<NodeMenuAttribute>().Where(attr => attr.GraphViewType == null || attr.GraphViewType == graphViewType).ToList();
                if (attrs.Count == 0)
                    continue;
                nodeTypes.Add((type, attrs[0]));
            }
            nodeTypes.Sort((x, y) => string.Compare(x.Item2.Path, y.Item2.Path));
            var groupPaths = new HashSet<string>();
            foreach (var type in nodeTypes)
            {
                var names = new Queue<string>(type.Item2.Path.Split('/'));
                var level = 1;
                while (names.Count > 1)
                {
                    var entryName = names.Dequeue();
                    if (groupPaths.Add(entryName))
                    {
                        m_SearchTree.Add(new SearchTreeGroupEntry(new GUIContent(entryName), level));
                    }
                    ++level;
                }
                m_SearchTree.Add(new SearchTreeEntry(new GUIContent(names.Dequeue())) { level = level, userData = type.Item1 });
            }
            return m_SearchTree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var type = entry.userData as Type;
            if (!s_ConstructorInfos.TryGetValue(type, out var info))
            {
                info = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
                s_ConstructorInfos[type] = info;
            }
            var graphElement = info?.Invoke(new object[0]) as GraphElement;
            if (addGraphElement != null && graphElement != null)
            {
                addGraphElement(graphElement, context.screenMousePosition);
                return true;
            }
            return false;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class NodeMenuAttribute : Attribute
    {
        public string Path { get; }
        public Type GraphViewType { get; }

        public NodeMenuAttribute(string path, Type graphViewType = null)
        {
            this.Path = path;
            this.GraphViewType = graphViewType;
        }
    }

}
