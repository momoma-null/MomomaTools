using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{

    sealed class TextureGraphWindow : EditorWindow
    {
        Graph<TextureGraph, TextureGraphEdge> m_SerializedGraphView;

        [MenuItem("MomomaTools/TextureGraph", false, 150)]
        public static TextureGraphWindow ShowWindow()
        {
            return EditorWindow.GetWindow<TextureGraphWindow>("TextureGraph");
        }

        void OnEnable()
        {
            m_SerializedGraphView = new Graph<TextureGraph, TextureGraphEdge>(this);
        }

        void OnDisable()
        {
            m_SerializedGraphView?.Dispose();
            m_SerializedGraphView = null;
        }
    }

    sealed class TextureGraph : GraphView, IGraphViewCallback, IDisposable
    {
        internal readonly Dictionary<Port, IList> processData = new Dictionary<Port, IList>();

        ExportTextureNode m_ExportTextureNode;
        ExportTextureNode exportTextureNode => m_ExportTextureNode ?? (m_ExportTextureNode = nodes.ToList().Find(n => n is ExportTextureNode) as ExportTextureNode);

        readonly PreviewWindow m_PreviewWindow;
        readonly IVisualElementScheduledItem m_RecalculateScheduledItem;

        internal int width => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value : 128;
        internal int height => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value : 128;

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
                UnityEngine.Object.DestroyImmediate(m_PreviewWindow.image);
        }

        protected override void CollectCopyableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToCopySet)
        {
            elements = elements.Where(e => (e.capabilities & Capabilities.Deletable) != 0);
            base.CollectCopyableGraphElements(elements, elementsToCopySet);
        }

        public override List<Port> GetCompatiblePorts(Port startAnchor, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            var relativePorts = new HashSet<Port>();
            var isInput = startAnchor.direction == Direction.Input;
            var container = isInput ? startAnchor.node.outputContainer : startAnchor.node.inputContainer;
            foreach (Port port in container)
            {
                FindPortRecursively(port, relativePorts);
            }
            foreach (var port in ports.ToList())
            {
                if (startAnchor.node == port.node
                 || startAnchor.direction == port.direction
                 || !(isInput ? IsAssignableType(port.portType, startAnchor.portType) : IsAssignableType(startAnchor.portType, port.portType))
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

        static bool IsAssignableType(Type fromType, Type toType)
        {
            if (toType.IsAssignableFrom(fromType))
                return true;
            if (toType == typeof(float) && (fromType == typeof(Vector4) || fromType == typeof(Vector3) || fromType == typeof(Vector2)))
                return true;
            if (fromType == typeof(float) && (toType == typeof(Vector4) || toType == typeof(Vector3) || toType == typeof(Vector2)))
                return true;
            return false;
        }

        internal static T[] AssignTo<T>(IList obj)
        {
            var fromType = obj.GetType().GetElementType();
            var toType = typeof(T);
            if (toType.IsAssignableFrom(fromType))
                return obj.Cast<T>().ToArray();
            if (toType == typeof(float))
            {
                if (obj is Vector4[] vector4s)
                    return vector4s.Select(v => v.x).ToArray() as T[];
                else if (obj is Vector3[] vector3s)
                    return vector3s.Select(v => v.x).ToArray() as T[];
                else if (obj is Vector2[] vector2s)
                    return vector2s.Select(v => v.x).ToArray() as T[];
            }
            else if (obj is float[] floats)
            {
                if (toType == typeof(Vector4))
                    return floats.Select(f => new Vector4(f, f, f, f)).ToArray() as T[];
                else if (toType == typeof(Vector3))
                    return floats.Select(f => new Vector3(f, f, f)).ToArray() as T[];
                else if (toType == typeof(Vector2))
                    return floats.Select(f => new Vector2(f, f)).ToArray() as T[];
            }
            throw new InvalidCastException(string.Format("can't cast {0} to {1}", fromType.Name, toType.Name));
        }

        void Recalculate()
        {
            m_RecalculateScheduledItem.Pause();
            var texture = ProcessAll();
            if (m_PreviewWindow.image != null)
                UnityEngine.Object.DestroyImmediate(m_PreviewWindow.image);
            m_PreviewWindow.image = texture;
            Debug.Log("Recalculate");
        }

        void SaveTexture()
        {
            isProduction = true;
            var texture = ProcessAll();
            isProduction = false;
            var bytes = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            var path = @"Assets/NewTexture.png";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            File.WriteAllBytes(Path.GetDirectoryName(Application.dataPath) + '/' + path, bytes);
            AssetDatabase.ImportAsset(path);
            ProcessAll();
        }

        Texture2D ProcessAll()
        {
            processData.Clear();
            exportTextureNode.Process(width, height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(exportTextureNode.colors);
            texture.Apply();
            return texture;
        }
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

    class PreviewWindow : GraphElement
    {
        readonly Image m_Image;

        internal Texture image
        {
            get { return m_Image.image.value; }
            set { m_Image.image = value; }
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
    class SerializableGroup : Group { }

}
