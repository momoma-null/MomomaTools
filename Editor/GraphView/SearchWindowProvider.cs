using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using MomomaAssets.Utility;

namespace MomomaAssets
{

    class SearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        internal TextureGraph graphView;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));
            entries.Add(new SearchTreeEntry(new GUIContent("Stack Node")) { level = 1, userData = typeof(StackNode) });
            entries.Add(new SearchTreeEntry(new GUIContent("Group")) { level = 1, userData = typeof(Group) });
            entries.AddRange(this.GetType().Assembly.GetTypes().
            Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(TextureGraphNode)) && type != typeof(ExportTextureNode)).
            Select(type => new SearchTreeEntry(new GUIContent(type.Name.ToSentence())) { level = 1, userData = type }));
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var type = entry.userData as Type;
            var node = Activator.CreateInstance(type, true) as Node;
            graphView.AddElement(node);
            var rect = new Rect();
            var root = graphView.window.GetRootVisualContainer();
            rect.position = graphView.contentViewContainer.WorldToLocal(root.ChangeCoordinatesTo(root.parent, context.screenMousePosition - graphView.window.position.position));
            node.SetPosition(rect);
            return true;
        }
    }

}
