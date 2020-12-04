using System;
using System.IO;
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

    class TextureGraphWindow : EditorWindow
    {
        TextureGraph m_TextureGraph;

        [MenuItem("MomomaTools/TextureGraph", false, 150)]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<TextureGraphWindow>("TextureGraph");
        }

        void OnEnable()
        {
            var rootVisualElement = this.GetRootVisualContainer();
            m_TextureGraph = new TextureGraph(this) { style = { flexGrow = 1 } };
            rootVisualElement.Add(m_TextureGraph);
            rootVisualElement.Add(new Button(m_TextureGraph.SaveAsAsset) { text = "Save Graph" });
            rootVisualElement.Add(new Button(m_TextureGraph.SaveTexture) { text = "Export Texture" });
        }
    }

    [Serializable]
    class TextureGraph : GraphView
    {
        internal readonly Dictionary<Port, object> processData = new Dictionary<Port, object>();
        internal readonly TextureGraphWindow window;
        internal TextureGraphData graphData;
        internal readonly ExportTextureNode exportTextureNode;

        readonly Image m_PreviewImage;
        readonly IVisualElementScheduledItem m_RecalculateScheduledItem;

        [SerializeField]
        List<string> m_SerializeNodes;
        [SerializeField]
        Vector3 m_ViewPortPosition;
        [SerializeField]
        Vector3 m_ViewPortScale;

        internal int width => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value : 128;
        internal int height => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value : 128;

        bool isProduction = false;

        internal TextureGraph(TextureGraphWindow window) : base()
        {
            this.window = window;
            AddStyleSheetPath("TextureGraphStyles");
            var background = new GridBackground();
            Insert(0, background);
            m_PreviewImage = new Image() { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore, style = { positionType = PositionType.Absolute, marginLeft = 20f, marginRight = 20f, marginTop = 20f, marginBottom = 20f, height = 128f, width = 128f } };
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
            viewTransformChanged += view => { m_ViewPortPosition = viewTransform.position; m_ViewPortScale = viewTransform.scale; };
            serializeGraphElements += OnSerializeGraphElements;
            unserializeAndPaste += OnUnserializeAndPaste;
            m_RecalculateScheduledItem = schedule.Execute(() => Recalculate());
        }

        ~TextureGraph()
        {
            if (m_PreviewImage?.image.value != null)
            {
                Texture.DestroyImmediate(m_PreviewImage.image.value);
            }
        }

        protected override void CollectCopyableGraphElements(IEnumerable<GraphElement> elements, HashSet<GraphElement> elementsToCopySet)
        {
            elements.Where(e => !(e is ExportTextureNode));
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

        internal static T AssignTo<T>(object obj)
        {
            var fromType = obj.GetType();
            var toType = typeof(T);
            if (toType.IsAssignableFrom(fromType))
                return (T)obj;
            if (toType == typeof(float))
            {
                if (obj is Vector4 vector4)
                    return (T)(object)vector4.x;
                else if (obj is Vector3 vector3)
                    return (T)(object)vector3.x;
                else if (obj is Vector2 vector2)
                    return (T)(object)vector2.x;
            }
            else if (obj is float f)
            {
                if (toType == typeof(Vector4))
                    return (T)(object)new Vector4(f, f, f, f);
                else if (toType == typeof(Vector3))
                    return (T)(object)new Vector3(f, f, f);
                else if (toType == typeof(Vector2))
                    return (T)(object)new Vector2(f, f);
            }
            throw new InvalidCastException(fromType.Name + " can't cast " + toType.Name);
        }

        string OnSerializeGraphElements(IEnumerable<GraphElement> elements)
        {
            return string.Join("\n", elements.Select(e => e.GetType().AssemblyQualifiedName + '&' + EditorJsonUtility.ToJson(e)));
        }

        void OnUnserializeAndPaste(string operationName, string data)
        {
            var nodes = new Dictionary<string, (Node, ISerializableNode)>();
            var ports = new Dictionary<string, (Node, ISerializableNode)>();
            var edges = new Queue<TextureGraphEdge>();
            var newEdges = new Queue<TextureGraphEdge>();
            var replaceGuids = new Dictionary<string, string>();
            foreach (var str in data.Split('\n'))
            {
                var subs = str.Split(new char[] { '&' }, 2);
                var element = Activator.CreateInstance(Type.GetType(subs[0]), true) as GraphElement;
                if (element == null || element is ExportTextureNode)
                    continue;
                if (element is Node node && element is ISerializableNode serializableNode)
                {
                    EditorJsonUtility.FromJsonOverwrite(subs[1], element);
                    AddElement(element);
                    MarkNordIsDirty();
                    nodes[serializableNode.guid] = (node, serializableNode);
                    replaceGuids[serializableNode.guid] = Guid.NewGuid().ToString("N");
                    foreach (var guid in serializableNode.inputPortGuids)
                    {
                        ports[guid] = (node, serializableNode);
                        replaceGuids[guid] = Guid.NewGuid().ToString("N");
                    }
                    foreach (var guid in serializableNode.outputPortGuids)
                    {
                        ports[guid] = (node, serializableNode);
                        replaceGuids[guid] = Guid.NewGuid().ToString("N");
                    }
                }
                else if (element is TextureGraphEdge edge)
                {
                    EditorJsonUtility.FromJsonOverwrite(subs[1], element);
                    edges.Enqueue(edge);
                }
            }
            foreach (var edge in edges)
            {
                if (string.IsNullOrEmpty(edge.inputGuid) || string.IsNullOrEmpty(edge.outputGuid) || !ports.ContainsKey(edge.inputGuid) || !ports.ContainsKey(edge.outputGuid))
                    continue;
                var inputNode = ports[edge.inputGuid];
                var outputNode = ports[edge.outputGuid];
                Port inputPort, outputPort;
                if (inputNode.Item1 is TokenNode iToken)
                {
                    inputPort = iToken.input;
                }
                else
                {
                    var index = Array.FindIndex(inputNode.Item2.inputPortGuids, s => s == edge.inputGuid);
                    inputPort = inputNode.Item1.inputContainer.Query<Port>().AtIndex(index);
                }
                if (outputNode.Item1 is TokenNode oToken)
                {
                    outputPort = oToken.output;
                }
                else
                {
                    var index = Array.FindIndex(outputNode.Item2.outputPortGuids, s => s == edge.outputGuid);
                    outputPort = outputNode.Item1.outputContainer.Query<Port>().AtIndex(index);
                }
                var newEdge = inputPort.ConnectTo<TextureGraphEdge>(outputPort);
                newEdges.Enqueue(newEdge);
                AddElement(newEdge);
            }
            var allRect = Rect.zero;
            foreach (var pair in nodes)
            {
                var rect = pair.Value.Item2.serializePosition;
                if (allRect == Rect.zero)
                    allRect = rect;
                else
                {
                    var xMax = Math.Max(rect.xMax, allRect.xMax);
                    var xMin = Math.Min(rect.xMin, allRect.xMin);
                    var yMax = Math.Max(rect.yMax, allRect.yMax);
                    var yMin = Math.Min(rect.yMin, allRect.yMin);
                    allRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
                }
            }
            var offset = contentViewContainer.WorldToLocal(contentRect.center) - allRect.center;
            foreach (var pair in nodes)
            {
                var rect = pair.Value.Item2.serializePosition;
                rect.position += offset;
                pair.Value.Item1.SetPosition(rect);
            }
            var guidReplacers = nodes.Select(n => n.Value.Item1 as IGuidReplacer).Union(newEdges.Select(e => e as IGuidReplacer));
            foreach(var e in guidReplacers)
                e.ReplaceGuids(replaceGuids);
        }

        internal void MarkNordIsDirty()
        {
            m_RecalculateScheduledItem.Resume();
        }

        void Recalculate()
        {
            m_RecalculateScheduledItem.Pause();
            var texture = ProcessAll();
            if (m_PreviewImage?.image.value != null)
                UnityEngine.Object.DestroyImmediate(m_PreviewImage.image.value);
            m_PreviewImage.image = texture;
            Debug.Log("Recalculate");
        }

        internal void SaveAsAsset()
        {
            m_SerializeNodes = this.Query<TextureGraphNode>().ForEach(n => n.guid);
            Debug.Log(m_SerializeNodes.Count);
            Debug.Log(EditorJsonUtility.ToJson(this, true));
            //TextureGraphData.SaveGraph(AssetDatabase.GetAssetPath(graphData), this);
        }

        internal void SaveTexture()
        {
            isProduction = true;
            var texture = ProcessAll();
            isProduction = false;
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
            var texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            texture.SetPixels(exportTextureNode.colors);
            texture.Apply();
            return texture;
        }
    }

    [Serializable]
    class TextureGraphEdge : Edge, IGuidReplacer
    {
        [SerializeField]
        string m_InputGuid;
        public string inputGuid => m_InputGuid;
        [SerializeField]
        string m_OutputGuid;
        public string outputGuid => m_OutputGuid;

        TextureGraph m_Graph;
        TextureGraph graph => m_Graph ?? (m_Graph = GetFirstAncestorOfType<TextureGraph>());

        bool isConnected = false;

        public TextureGraphEdge() : base()
        {
            this.AddManipulator(new ContextualMenuManipulator(context => context.menu.AppendAction("Add Token", action => AddToken(action), action => DropdownMenu.MenuAction.StatusFlags.Normal)));
        }

        public void ReplaceGuids(Dictionary<string, string> replaceGuids)
        {
            replaceGuids.TryGetValue(m_InputGuid, out m_InputGuid);
            replaceGuids.TryGetValue(m_OutputGuid, out m_OutputGuid);
        }

        void AddToken(DropdownMenu.MenuAction action)
        {
            var inputPort = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, output.portType);
            var outputPort = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, input.portType);
            var token = new SerializableTokenNode(inputPort, outputPort);
            graph.AddElement(token);
            var rect = new Rect(action.eventInfo.localMousePosition, Vector2.zero);
            token.SetPosition(rect);
            input.Disconnect(this);
            inputPort.Connect(this);
            graph.AddElement(input.ConnectTo<TextureGraphEdge>(outputPort));
            input = inputPort;
        }

        public override void OnPortChanged(bool isInput)
        {
            base.OnPortChanged(isInput);
            if (isGhostEdge)
                return;
            if (input != null && input.node is ISerializableNode iNode)
            {
                if (input.node is TokenNode)
                {
                    m_InputGuid = iNode.inputPortGuids[0];
                }
                else
                {
                    var index = input.node.inputContainer.Query<Port>().ToList().FindIndex(p => p == input);
                    m_InputGuid = iNode.inputPortGuids[index];
                }
            }
            else
            {
                m_InputGuid = null;
            }
            if (output != null && output.node is ISerializableNode oNode)
            {
                if (output.node is TokenNode)
                {
                    m_OutputGuid = oNode.outputPortGuids[0];
                }
                else
                {
                    var index = output.node.outputContainer.Query<Port>().ToList().FindIndex(p => p == output);
                    m_OutputGuid = oNode.outputPortGuids[index];
                }
            }
            else
            {
                m_OutputGuid = null;
            }
            if (graph != null)
            {
                if ((input != null && output != null))
                {
                    graph.MarkNordIsDirty();
                    isConnected = true;
                }
                else if (input == null && output == null && isConnected)
                {
                    graph.MarkNordIsDirty();
                }
            }
        }
    }

}
