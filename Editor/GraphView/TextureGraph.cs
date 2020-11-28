using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{

    class TextureGraphWindow : EditorWindow
    {

        [MenuItem("MomomaTools/TextureGraph", false, 105)]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<TextureGraphWindow>("TextureGraph");
        }

        void OnEnable()
        {
            var rootVisualElemnt = this.GetRootVisualContainer();
            var graph = new TextureGraph(this)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElemnt.Add(graph);
        }
    }

    class TextureGraph : GraphView
    {
        internal readonly TextureGraphWindow window;

        HashSet<Port> m_RelativePorts;

        internal TextureGraph(TextureGraphWindow window) : base()
        {
            this.window = window;
            AddStyleSheetPath("TextureGraphStyles");
            var background = new GridBackground();
            Insert(0, background);
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new RectangleSelector());
            var searchWindowProvider = ScriptableObject.CreateInstance<SearchWindowProvider>();
            searchWindowProvider.graphView = this;
            nodeCreationRequest += context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindowProvider);
        }

        public override List<Port> GetCompatiblePorts(Port startAnchor, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            m_RelativePorts = new HashSet<Port>();
            var container = startAnchor.direction == Direction.Input ? startAnchor.node.outputContainer : startAnchor.node.inputContainer;
            foreach (Port port in container)
            {
                FindPortRecursively(port);
            }
            foreach (var port in ports.ToList())
            {
                if (startAnchor.node == port.node || startAnchor.direction == port.direction || startAnchor.portType != port.portType || m_RelativePorts.Contains(port))
                    continue;
                compatiblePorts.Add(port);
            }
            m_RelativePorts = null;
            return compatiblePorts;
        }

        void FindPortRecursively(Port port)
        {
            foreach (var edge in port.connections)
            {
                var pairPort = port.direction == Direction.Input ? edge.output : edge.input;
                var container = port.direction == Direction.Input ? pairPort.node.inputContainer : pairPort.node.outputContainer;
                foreach (Port nextport in container)
                {
                    if (m_RelativePorts.Add(nextport))
                        FindPortRecursively(nextport);
                }
            }
        }
    }

    class SearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        internal TextureGraph graphView;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));
            entries.AddRange(this.GetType().Assembly.GetTypes().
            Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(TextureGraphNode))).
            Select(type => new SearchTreeEntry(new GUIContent(type.Name)) { level = 1, userData = type }));
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var type = entry.userData as System.Type;
            var node = Activator.CreateInstance(type, true) as TextureGraphNode;
            graphView.AddElement(node);
            var rect = node.GetPosition();
            var root = graphView.window.GetRootVisualContainer();
            rect.position = graphView.contentViewContainer.WorldToLocal(root.ChangeCoordinatesTo(root.parent, context.screenMousePosition - graphView.window.position.position));
            node.SetPosition(rect);
            return true;
        }
    }

    abstract class TextureGraphNode : Node { }

    class ImportTextureNode : TextureGraphNode
    {
        internal ImportTextureNode()
        {
            title = "Import Texture";
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Port));
            inputContainer.Add(inputPort);
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(Port));
            outputContainer.Add(outputPort);
        }
    }

}
