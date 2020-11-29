using System;
using System.IO;
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

    class TextureGraphWindow : EditorWindow
    {
        TextureGraph m_TextureGraph;

        [MenuItem("MomomaTools/TextureGraph", false, 105)]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<TextureGraphWindow>("TextureGraph");
        }

        void OnEnable()
        {
            var rootVisualElement = this.GetRootVisualContainer();
            m_TextureGraph = new TextureGraph(this)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(m_TextureGraph);
            rootVisualElement.Add(new Button(m_TextureGraph.SaveAsAsset) { text = "Save Graph" });
            rootVisualElement.Add(new Button(m_TextureGraph.SaveTexture) { text = "Export Texture" });
        }

        void OnDisable()
        {
            m_TextureGraph.OnDisable();
        }
    }

    class TextureGraph : GraphView
    {
        internal readonly Dictionary<Port, object> processData = new Dictionary<Port, object>();
        internal readonly TextureGraphWindow window;
        internal TextureGraphData graphData;
        internal readonly ExportTextureNode exportTextureNode;

        internal int width => exportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value;
        internal int height => exportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value;

        HashSet<Port> m_RelativePorts;
        readonly Image m_PreviewImage;

        internal TextureGraph(TextureGraphWindow window) : base()
        {
            this.window = window;
            AddStyleSheetPath("TextureGraphStyles");
            var background = new GridBackground();
            Insert(0, background);
            m_PreviewImage = new Image() { scaleMode = ScaleMode.ScaleToFit, style = { positionType = PositionType.Absolute, marginLeft = 20f, marginTop = 20f, height = 64f, width = 64f } };
            Insert(1, m_PreviewImage);
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new RectangleSelector());
            var searchWindowProvider = ScriptableObject.CreateInstance<SearchWindowProvider>();
            searchWindowProvider.graphView = this;
            nodeCreationRequest += context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindowProvider);
            exportTextureNode = new ExportTextureNode();
            AddElement(exportTextureNode);
            graphViewChanged += OnGraphViewChanged;
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

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            var texture = ProcessAll();
            if (m_PreviewImage.image.value != null)
                UnityEngine.Object.DestroyImmediate(m_PreviewImage.image.value);
            m_PreviewImage.image = texture;
            Debug.Log(texture.GetPixel(16, 16).ToString());
            return change;
        }

        internal void OnDisable()
        {
            if (m_PreviewImage?.image != null)
            {
                UnityEngine.Object.DestroyImmediate(m_PreviewImage.image);
            }
        }

        internal void SaveAsAsset()
        {
            TextureGraphData.SaveGraph(AssetDatabase.GetAssetPath(graphData), this);
        }

        internal void SaveTexture()
        {
            var texture = ProcessAll();
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            var path = AssetDatabase.GetAssetPath(graphData);
            if (string.IsNullOrEmpty(path))
            {
                path = @"Assets/NewTexture.png";
            }
            else
            {
                Path.ChangeExtension(path, "png");
            }
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            File.WriteAllBytes(Path.GetDirectoryName(Application.dataPath) + '/' + path, bytes);
            AssetDatabase.ImportAsset(path);
        }

        Texture2D ProcessAll()
        {
            processData.Clear();
            exportTextureNode.Process();
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.SetPixels32(exportTextureNode.colors);
            return texture;
        }
    }

    class SearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        internal TextureGraph graphView;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>();
            entries.Add(new SearchTreeGroupEntry(new GUIContent("Create Node")));
            entries.Add(new SearchTreeEntry(new GUIContent("Stack Node")) { level = 1, userData = typeof(StackNode) });
            entries.AddRange(this.GetType().Assembly.GetTypes().
            Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(TextureGraphNode)) && type != typeof(ExportTextureNode)).
            Select(type => new SearchTreeEntry(new GUIContent(type.Name.ToSentence())) { level = 1, userData = type }));
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var type = entry.userData as System.Type;
            var node = Activator.CreateInstance(type, true) as Node;
            graphView.AddElement(node);
            var rect = node.GetPosition();
            var root = graphView.window.GetRootVisualContainer();
            rect.position = graphView.contentViewContainer.WorldToLocal(root.ChangeCoordinatesTo(root.parent, context.screenMousePosition - graphView.window.position.position));
            node.SetPosition(rect);
            return true;
        }
    }

    abstract class TextureGraphNode : Node
    {
        TextureGraph m_Graph;
        protected TextureGraph graph => m_Graph ?? (m_Graph = GetFirstAncestorOfType<TextureGraph>());

        public override void SetPosition(Rect newPos)
        {
            newPos.x = Mathf.Round(newPos.x * 0.1f) * 10f;
            newPos.y = Mathf.Round(newPos.y * 0.1f) * 10f;
            base.SetPosition(newPos);
        }

        internal abstract void Process();

        protected bool IsProcessed()
        {
            foreach (var port in outputContainer.Query<Port>().ToList())
            {
                if (graph.processData.ContainsKey(port))
                    return true;
            }
            foreach (var port in inputContainer.Query<Port>().ToList())
            {
                if (graph.processData.ContainsKey(port))
                    return true;
            }
            return false;
        }
    }

    class ImportTextureNode : TextureGraphNode
    {
        readonly ObjectField objectField;

        internal ImportTextureNode()
        {
            title = "Import Texture";
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Color32));
            outputPort.portName = "Color";
            outputContainer.Add(outputPort);
            RefreshPorts();
            objectField = new ObjectField() { objectType = typeof(Texture2D) };
            extensionContainer.Add(objectField);
            RefreshExpandedState();
        }

        internal override void Process()
        {
            if (IsProcessed())
                return;
            var texture = objectField.value as Texture2D;
            if (texture == null)
                texture = Texture2D.blackTexture;
            var renderTexture = new RenderTexture(graph.width, graph.height, 1, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, renderTexture);
            var currentRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var readableTexture = new Texture2D(graph.width, graph.height, TextureFormat.ARGB32, false);
            readableTexture.ReadPixels(new Rect(0, 0, graph.width, graph.height), 0, 0);
            readableTexture.Apply();
            var colors = readableTexture.GetPixels32();
            RenderTexture.active = currentRT;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(readableTexture);
            var port = outputContainer.Q<Port>();
            graph.processData[port] = colors;
        }
    }

    class ExportTextureNode : TextureGraphNode
    {
        static readonly List<int> s_PopupValues = new List<int>() { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        Color32[] m_Colors;
        internal Color32[] colors => m_Colors;

        readonly PopupField<int> widthPopupField;
        readonly PopupField<int> heightPopupField;

        internal ExportTextureNode()
        {
            title = "Export Texture";
            capabilities = capabilities & ~Capabilities.Deletable;
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Color32));
            inputPort.portName = "Color";
            inputContainer.Add(inputPort);
            RefreshPorts();
            widthPopupField = new PopupField<int>(s_PopupValues, 6) { name = "Width" };
            heightPopupField = new PopupField<int>(s_PopupValues, 6) { name = "Height" };
            extensionContainer.Add(widthPopupField);
            extensionContainer.Add(heightPopupField);
            RefreshExpandedState();
        }

        internal override void Process()
        {
            var width = widthPopupField.value;
            var height = heightPopupField.value;
            m_Colors = new Color32[width * height];
            var port = inputContainer.Q<Port>();
            foreach (var edge in port.connections)
            {
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                m_Colors = graph.processData[edge.output] as Color32[];
            }
        }
    }

    class DecomposeChannelsNode : TextureGraphNode
    {
        internal DecomposeChannelsNode()
        {
            title = "Decompose Channels";
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Color32));
            inputPort.portName = "Color";
            inputContainer.Add(inputPort);
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(byte));
            outputPort.name = "Red";
            outputPort.portName = "R";
            outputContainer.Add(outputPort);
            outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(byte));
            outputPort.name = "Green";
            outputPort.portName = "G";
            outputContainer.Add(outputPort);
            outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(byte));
            outputPort.name = "Blue";
            outputPort.portName = "B";
            outputContainer.Add(outputPort);
            outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(byte));
            outputPort.name = "Alpha";
            outputPort.portName = "A";
            outputContainer.Add(outputPort);
            RefreshPorts();
        }

        internal override void Process()
        {
            if (IsProcessed())
                return;
            var colors = new Color32[graph.width * graph.height];
            var port = inputContainer.Q<Port>();
            foreach (var edge in port.connections)
            {
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                colors = graph.processData[edge.output] as Color32[];
            }
            port = outputContainer.Q<Port>("Red");
            graph.processData[port] = colors.Select(c => c.r).ToArray();
            port = outputContainer.Q<Port>("Green");
            graph.processData[port] = colors.Select(c => c.g).ToArray();
            port = outputContainer.Q<Port>("Blue");
            graph.processData[port] = colors.Select(c => c.b).ToArray();
            port = outputContainer.Q<Port>("Alpha");
            graph.processData[port] = colors.Select(c => c.a).ToArray();
        }
    }

    class CombineChannelsNode : TextureGraphNode
    {
        internal CombineChannelsNode()
        {
            title = "Combine Channels";
            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(byte));
            inputPort.name = "Red";
            inputPort.portName = "R";
            inputContainer.Add(inputPort);
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(byte));
            inputPort.name = "Green";
            inputPort.portName = "G";
            inputContainer.Add(inputPort);
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(byte));
            inputPort.name = "Blue";
            inputPort.portName = "B";
            inputContainer.Add(inputPort);
            inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(byte));
            inputPort.name = "Alpha";
            inputPort.portName = "A";
            inputContainer.Add(inputPort);
            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Color32));
            outputPort.portName = "Color";
            outputContainer.Add(outputPort);
            RefreshPorts();
        }

        internal override void Process()
        {
            if (IsProcessed())
                return;
            var colors = new Color32[graph.width * graph.height];
            var port = inputContainer.Q<Port>("Red");
            foreach (var edge in port.connections)
            {
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                var bytes = graph.processData[edge.output] as byte[];
                for (var i = 0; i < bytes.Length; ++i)
                {
                    colors[i].r = bytes[i];
                }
            }
            port = inputContainer.Q<Port>("Blue");
            foreach (var edge in port.connections)
            {
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                var bytes = graph.processData[edge.output] as byte[];
                for (var i = 0; i < bytes.Length; ++i)
                {
                    colors[i].b = bytes[i];
                }
            }
            port = inputContainer.Q<Port>("Green");
            foreach (var edge in port.connections)
            {
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                var bytes = graph.processData[edge.output] as byte[];
                for (var i = 0; i < bytes.Length; ++i)
                {
                    colors[i].g = bytes[i];
                }
            }
            port = inputContainer.Q<Port>("Alpha");
            foreach (var edge in port.connections)
            {
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                var bytes = graph.processData[edge.output] as byte[];
                for (var i = 0; i < bytes.Length; ++i)
                {
                    colors[i].a = bytes[i];
                }
            }
            port = outputContainer.Q<Port>();
            graph.processData[port] = colors;
        }
    }

}
