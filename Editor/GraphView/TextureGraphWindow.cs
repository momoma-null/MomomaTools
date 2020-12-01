using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

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

        readonly Image m_PreviewImage;

        internal int width => exportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value;
        internal int height => exportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value;

        internal TextureGraph(TextureGraphWindow window) : base()
        {
            this.window = window;
            AddStyleSheetPath("TextureGraphStyles");
            var background = new GridBackground();
            Insert(0, background);
            m_PreviewImage = new Image() { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore, style = { positionType = PositionType.Absolute, marginLeft = 20f, marginTop = 20f, height = 128f, width = 128f } };
            Insert(1, m_PreviewImage);
            exportTextureNode = new ExportTextureNode();
            AddElement(exportTextureNode);
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new RectangleSelector());
            var searchWindowProvider = ScriptableObject.CreateInstance<SearchWindowProvider>();
            searchWindowProvider.graphView = this;
            nodeCreationRequest += context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindowProvider);
            graphViewChanged += OnGraphViewChanged;
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

        void FindPortRecursively(Port port, HashSet<Port> relativePorts)
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

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.edgesToCreate?.Count > 0 || change.elementsToRemove?.Count > 0)
            {
                Recalculate();
            }
            return change;
        }

        internal void Recalculate()
        {
            var texture = ProcessAll();
            if (m_PreviewImage?.image.value != null)
                UnityEngine.Object.DestroyImmediate(m_PreviewImage.image.value);
            m_PreviewImage.image = texture;
        }

        internal void OnDisable()
        {
            if (m_PreviewImage?.image.value != null)
            {
                UnityEngine.Object.DestroyImmediate(m_PreviewImage.image.value);
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
            texture.SetPixels(exportTextureNode.colors);
            texture.Apply();
            return texture;
        }
    }

}
