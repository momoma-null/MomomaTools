using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityObject = UnityEngine.Object;

namespace MomomaAssets
{

    sealed class TextureGraphWindow : EditorWindow
    {
        NodeGraph<TextureGraph, TextureGraphEdge> m_NodeGraph;

        [MenuItem("MomomaTools/TextureGraph", false, 150)]
        public static TextureGraphWindow ShowWindow()
        {
            return EditorWindow.GetWindow<TextureGraphWindow>("TextureGraph");
        }

        void OnEnable()
        {
            m_NodeGraph = new NodeGraph<TextureGraph, TextureGraphEdge>(this);
        }

        void OnDisable()
        {
            m_NodeGraph?.Dispose();
            m_NodeGraph = null;
        }
    }

    sealed class TextureGraph : GraphView, IGraphViewCallback, IDisposable
    {
        readonly PreviewWindow m_PreviewWindow;
        readonly IVisualElementScheduledItem m_RecalculateScheduledItem;

        ExportTextureNode m_ExportTextureNode;

        internal int width => isProduction ? m_ExportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value : 128;
        internal int height => isProduction ? m_ExportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value : 128;

        bool isProduction = false;

        public TextureGraph() : base()
        {
            m_RecalculateScheduledItem = schedule.Execute(Recalculate);
            m_RecalculateScheduledItem.Pause();
            m_PreviewWindow = new PreviewWindow();
            Add(m_PreviewWindow);
            Add(new Button(SaveTexture) { text = "Export Texture", style = { alignSelf = Align.FlexEnd } });
        }

        ~TextureGraph()
        {
            Dispose();
        }

        void IGraphViewCallback.Initialize()
        {
            m_ExportTextureNode = new ExportTextureNode();
            AddElement(m_ExportTextureNode);
            var pos = Rect.zero;
            pos.center = layout.center;
            m_ExportTextureNode.SetPosition(pos);
        }

        void IGraphViewCallback.OnValueChanged(GraphElement graphElement)
        {
            m_RecalculateScheduledItem.Pause();
            m_RecalculateScheduledItem.ExecuteLater(500);
        }

        public void Dispose()
        {
            if (m_PreviewWindow.image != null)
                UnityObject.DestroyImmediate(m_PreviewWindow.image);
        }

        protected override void CollectCopyableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToCopySet)
        {
            elements = elements.Where(e => (e.capabilities & Capabilities.Deletable) != 0);
            base.CollectCopyableGraphElements(elements, elementsToCopySet);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            var relativePorts = new HashSet<Port>();
            var isInput = startPort.direction == Direction.Input;
            var container = isInput ? startPort.node.outputContainer : startPort.node.inputContainer;
            foreach (Port port in container)
            {
                FindPortRecursively(port, relativePorts);
            }
            foreach (var port in ports.ToList())
            {
                if (startPort.node == port.node
                 || startPort.direction == port.direction
                 || !(isInput ? IsAssignableType(port.portType, startPort.portType, nodeAdapter) : IsAssignableType(startPort.portType, port.portType, nodeAdapter))
                 || relativePorts.Contains(port))
                    continue;
                compatiblePorts.Add(port);
            }
            return compatiblePorts;
        }

        static void FindPortRecursively(Port port, HashSet<Port> relativePorts)
        {
            foreach (var edge in port.connections)
            {
                var pairPort = port.direction == Direction.Input ? edge.output : edge.input;
                var container = port.direction == Direction.Input ? pairPort.node.inputContainer : pairPort.node.outputContainer;
                foreach (Port nextport in container)
                {
                    if (relativePorts.Add(nextport))
                        FindPortRecursively(nextport, relativePorts);
                }
            }
        }

        [TypeAdapter]
        public static Vector4 AdaptType(float from) => new Vector4(from, from, from, from);

        [TypeAdapter]
        public static float AdaptType(Vector4 from) => from.x;

        static bool IsAssignableType(Type fromType, Type toType, NodeAdapter nodeAdapter)
        {
            return toType.IsAssignableFrom(fromType) || nodeAdapter.GetTypeAdapter(fromType, toType) != null;
        }

        void Recalculate()
        {
            m_RecalculateScheduledItem.Pause();
            var texture = ProcessAll();
            if (m_PreviewWindow.image != null)
                UnityObject.DestroyImmediate(m_PreviewWindow.image);
            m_PreviewWindow.image = texture;
        }

        void SaveTexture()
        {
            isProduction = true;
            var texture = ProcessAll();
            isProduction = false;
            var bytes = texture.EncodeToPNG();
            UnityObject.DestroyImmediate(texture);
            var path = @"Assets/NewTexture.png";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            ProcessAll();
        }

        Texture2D ProcessAll()
        {
            if (m_ExportTextureNode == null)
                m_ExportTextureNode = this.Q<ExportTextureNode>();
            m_ExportTextureNode.StartProcess(new TextureGraphData(width, height));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave };
            texture.SetPixels(m_ExportTextureNode.colors);
            texture.Apply();
            return texture;
        }
    }

    sealed class TextureGraphData
    {
        public Dictionary<string, IList> portDatas { get; } = new Dictionary<string, IList>();
        public int width { get; }
        public int height { get; }

        public TextureGraphData(int width, int height)
        {
            this.width = width;
            this.height = height;
        }

        public T[] GetArray<T>() => new T[width * height];
    }

    sealed class TextureGraphEdge : Edge, IEdgeCallback
    {
        public event Action<Edge> onPortChanged;

        public TextureGraphEdge() : base() { }

        public override void OnPortChanged(bool isInput)
        {
            base.OnPortChanged(isInput);
            onPortChanged?.Invoke(this);
        }
    }

    sealed class PreviewWindow : GraphElement
    {
        readonly Image m_Image;

        internal Texture image
        {
            get => m_Image.image;
            set => m_Image.image = value;
        }

        internal PreviewWindow()
        {
            style.backgroundColor = new Color(0.2470588f, 0.2470588f, 0.2470588f, 1f);
            style.borderColor = new Color(0.09803922f, 0.09803922f, 0.09803922f, 1f);
            style.borderRadius = 6f;
            style.positionType = PositionType.Absolute;
            style.positionRight = 4f;
            style.positionBottom = 4f;
            style.width = 136f;
            style.height = 136f;
            style.marginRight = 0f;
            style.marginLeft = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            capabilities = Capabilities.Movable;
            m_Image = new Image { style = { marginRight = 3f, marginLeft = 3f, marginTop = 3f, marginBottom = 3f } };
            Add(m_Image);
            m_Image.StretchToParentSize();
            this.AddManipulator(new Dragger { clampToParentEdges = true });
        }
    }

    [NodeMenu("Group/Group", typeof(TextureGraph))]
    sealed class SerializableGroup : Group { }
}
