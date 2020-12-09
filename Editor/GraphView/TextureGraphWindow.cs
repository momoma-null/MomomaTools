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
        public static TextureGraphWindow ShowWindow()
        {
            return EditorWindow.GetWindow<TextureGraphWindow>("TextureGraph");
        }

        void OnEnable()
        {
            var rootVisualElement = this.GetRootVisualContainer();
            m_TextureGraph = new TextureGraph(this) { style = { flexGrow = 1 } };
            rootVisualElement.Add(m_TextureGraph);
        }
    }

    class TextureGraph : GraphView
    {
        internal readonly Dictionary<Port, object> processData = new Dictionary<Port, object>();
        internal readonly TextureGraphWindow window;

        TextureGraphData m_GraphData;
        internal TextureGraphData graphData
        {
            get { return m_GraphData; }
            set
            {
                m_GraphData = value;
                serializedObject = new SerializedObject(m_GraphData);
                m_Nodes = serializedObject.FindProperty("m_Nodes");
                m_Edges = serializedObject.FindProperty("m_Edges");
            }
        }

        SerializedObject serializedObject;
        SerializedProperty m_Nodes;
        SerializedProperty m_Edges;

        ExportTextureNode m_ExportTextureNode;
        internal ExportTextureNode exportTextureNode => m_ExportTextureNode ?? (m_ExportTextureNode = nodes.ToList().Find(n => n is ExportTextureNode) as ExportTextureNode);

        readonly PreviewWindow m_PreviewWindow;
        readonly IVisualElementScheduledItem m_RecalculateScheduledItem;
        readonly HashSet<NodeObject> m_NodeObjectHolder = new HashSet<NodeObject>();
        readonly HashSet<EdgeObject> m_EdgeObjectHolder = new HashSet<EdgeObject>();

        internal int width => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value : 128;
        internal int height => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value : 128;

        bool isProduction = false;

        internal TextureGraph(TextureGraphWindow window) : base()
        {
            this.window = window;
            graphData = ScriptableObject.CreateInstance<TextureGraphData>();
            AddStyleSheetPath("TextureGraphStyles");
            var background = new GridBackground() { style = { alignItems = Align.Center, justifyContent = Justify.Center } };
            Insert(0, background);
            m_PreviewWindow = new PreviewWindow();
            Add(m_PreviewWindow);
            Add(new MiniMap());
            Add(new Button(SaveAsAsset) { text = "Save Graph", style = { alignSelf = Align.FlexEnd } });
            Add(new Button(SaveTexture) { text = "Export Texture", style = { alignSelf = Align.FlexEnd } });
            m_ExportTextureNode = new ExportTextureNode();
            AddElementWithRecord(m_ExportTextureNode, true);
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new RectangleSelector());
            var searchWindowProvider = ScriptableObject.CreateInstance<SearchWindowProvider>();
            searchWindowProvider.graphView = this;
            nodeCreationRequest += context => SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindowProvider);
            serializeGraphElements += OnSerializeGraphElements;
            unserializeAndPaste += OnUnserializeAndPaste;
            graphViewChanged += OnGraphViewChanged;
            Undo.undoRedoPerformed += UndoRedoPerformed;
            m_RecalculateScheduledItem = schedule.Execute(() => Recalculate());
            m_RecalculateScheduledItem.Pause();
            schedule.Execute(() => { if (panel != null) OnPersistentDataReady(); }).Until(() => panel != null);
        }

        ~TextureGraph()
        {
            if (m_PreviewWindow.image != null)
            {
                Texture.DestroyImmediate(m_PreviewWindow.image);
            }
        }

        public override void OnPersistentDataReady()
        {
            base.OnPersistentDataReady();
            UpdateViewTransform(window.position.size * 0.5f, viewTransform.scale);
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

        internal void AddElementWithRecord(GraphElement element, bool withoutUndo = false, bool onlyRecord = false)
        {
            if (!onlyRecord)
                AddElement(element);
            if (element is ISerializableNode serializableNode)
            {
                if (Array.Exists(graphData.nodes, data => data.nodeObject == serializableNode.nodeObject))
                    return;
                if (AssetDatabase.IsMainAsset(graphData))
                    AssetDatabase.AddObjectToAsset(serializableNode.nodeObject, graphData);
                serializedObject.Update();
                ++m_Nodes.arraySize;
                var nodeData = m_Nodes.GetArrayElementAtIndex(m_Nodes.arraySize - 1);
                nodeData.FindPropertyRelative("m_TypeName").stringValue = element.GetType().AssemblyQualifiedName;
                nodeData.FindPropertyRelative("m_NodeObject").objectReferenceValue = serializableNode.nodeObject;
                if (withoutUndo)
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                else
                    serializedObject.ApplyModifiedProperties();
                m_NodeObjectHolder.Add(serializableNode.nodeObject);
            }
            else if (element is TextureGraphEdge edge)
            {
                if (Array.Exists(graphData.edges, data => data.edgeObject == edge.edgeObject))
                    return;
                if (AssetDatabase.IsMainAsset(graphData))
                    AssetDatabase.AddObjectToAsset(edge.edgeObject, graphData);
                serializedObject.Update();
                ++m_Edges.arraySize;
                var edgeData = m_Edges.GetArrayElementAtIndex(m_Edges.arraySize - 1);
                edgeData.FindPropertyRelative("m_TypeName").stringValue = element.GetType().AssemblyQualifiedName;
                edgeData.FindPropertyRelative("m_EdgeObject").objectReferenceValue = edge.edgeObject;
                if (withoutUndo)
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                else
                    serializedObject.ApplyModifiedProperties();
                m_EdgeObjectHolder.Add(edge.edgeObject);
            }
        }

        GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.elementsToRemove?.Exists(e => e is ExportTextureNode) ?? false)
            {
                m_ExportTextureNode = null;
            }
            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    AddElementWithRecord(edge, onlyRecord: true);
                }
            }
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var element in graphViewChange.elementsToRemove)
                {
                    if (element is ExportTextureNode)
                        continue;
                    if (element is ISerializableNode serializableNode)
                    {
                        var index = Array.FindIndex(graphData.nodes, data => data.nodeObject == serializableNode.nodeObject);
                        serializedObject.Update();
                        m_Nodes.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                    }
                    else if (element is TextureGraphEdge edge)
                    {
                        var index = Array.FindIndex(graphData.edges, data => data.edgeObject == edge.edgeObject);
                        serializedObject.Update();
                        m_Edges.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            return graphViewChange;
        }

        void UndoRedoPerformed()
        {
            FullReload();
        }

        public void FullReload()
        {
            var viewNodeObjects = new HashSet<NodeObject>(nodes.ToList().Select(e => (e as ISerializableNode).nodeObject));
            var toAddNodes = graphData.nodes.Where(d => !viewNodeObjects.Contains(d.nodeObject)).ToArray();
            var dataNodeObjects = new HashSet<NodeObject>(graphData.nodes.Select(d => d.nodeObject));
            var toRemoveNodes = nodes.ToList().Where(e => !dataNodeObjects.Contains((e as ISerializableNode).nodeObject)).ToArray();
            foreach (var data in toAddNodes)
            {
                var node = Activator.CreateInstance(Type.GetType(data.typeName), true) as Node;
                var serializableNode = node as ISerializableNode;
                EditorUtility.CopySerialized(data.nodeObject, serializableNode.nodeObject);
                AddElement(node);
                m_NodeObjectHolder.Add(serializableNode.nodeObject);
                serializableNode.LoadSerializedFields();
            }
            if (toRemoveNodes.Length > 0)
                DeleteElements(toRemoveNodes);
            var ports = new Dictionary<string, Port>();
            nodes.ForEach(node =>
            {
                var serializableNode = node as ISerializableNode;
                node.SetPosition(serializableNode.nodeObject.position);
                if (node is TokenNode token)
                {
                    ports[serializableNode.nodeObject.inputPortGuids[0]] = token.input;
                    ports[serializableNode.nodeObject.outputPortGuids[0]] = token.output;
                }
                else
                {
                    var iPorts = new Queue<Port>(node.inputContainer.Query<Port>().ToList());
                    foreach (var guid in serializableNode.nodeObject.inputPortGuids)
                    {
                        ports[guid] = iPorts.Dequeue();
                    }
                    var oPorts = new Queue<Port>(node.outputContainer.Query<Port>().ToList());
                    foreach (var guid in serializableNode.nodeObject.outputPortGuids)
                    {
                        ports[guid] = oPorts.Dequeue();
                    }
                }
            });
            var viewEdgeObjects = new HashSet<EdgeObject>(edges.ToList().Select(e => (e as TextureGraphEdge).edgeObject));
            var toAddEdges = graphData.edges.Where(d => !viewEdgeObjects.Contains(d.edgeObject)).ToArray();
            var dataEdgeObjects = new HashSet<EdgeObject>(graphData.edges.Select(d => d.edgeObject));
            var toRemoveEdges = edges.ToList().Where(e => !dataEdgeObjects.Contains((e as TextureGraphEdge).edgeObject)).ToArray();
            foreach (var data in toAddEdges)
            {
                var edge = Activator.CreateInstance(Type.GetType(data.typeName), true) as TextureGraphEdge;
                EditorUtility.CopySerialized(data.edgeObject, edge.edgeObject);
                if (string.IsNullOrEmpty(edge.edgeObject.inputGuid) || string.IsNullOrEmpty(edge.edgeObject.outputGuid) || !ports.ContainsKey(edge.edgeObject.inputGuid) || !ports.ContainsKey(edge.edgeObject.outputGuid))
                    continue;
                var inputPort = ports[edge.edgeObject.inputGuid];
                var outputPort = ports[edge.edgeObject.outputGuid];
                edge.input = inputPort;
                edge.output = outputPort;
                inputPort.Connect(edge);
                outputPort.Connect(edge);
                AddElement(edge);
                MarkNordIsDirty();
            }
            foreach (var edge in toRemoveEdges)
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                edge.input = null;
                edge.output = null;
                MarkNordIsDirty();
            }
            if (toRemoveEdges.Length > 0)
                DeleteElements(toRemoveEdges);
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
            var serializedElements = new List<string>();
            foreach (var e in elements)
            {
                if (e is ISerializableNode serializableNode)
                    serializedElements.Add(e.GetType().AssemblyQualifiedName + '&' + EditorJsonUtility.ToJson(serializableNode.nodeObject));
                else if (e is TextureGraphEdge edge)
                    serializedElements.Add(e.GetType().AssemblyQualifiedName + '&' + EditorJsonUtility.ToJson(edge.edgeObject));
            }
            return string.Join("\n", serializedElements);
        }

        void OnUnserializeAndPaste(string operationName, string data)
        {
            var nodes = new Dictionary<string, (Node, ISerializableNode)>();
            var ports = new Dictionary<string, Port>();
            var edges = new Queue<TextureGraphEdge>();
            foreach (var str in data.Split('\n'))
            {
                var subs = str.Split(new char[] { '&' }, 2);
                var element = Activator.CreateInstance(Type.GetType(subs[0]), true) as GraphElement;
                if (element == null || element is ExportTextureNode)
                    continue;
                if (element is Node node && element is ISerializableNode serializableNode)
                {
                    EditorJsonUtility.FromJsonOverwrite(subs[1], serializableNode.nodeObject);
                    AddElementWithRecord(element);
                    MarkNordIsDirty();
                    nodes[serializableNode.nodeObject.guid] = (node, serializableNode);
                    if (node is TokenNode token)
                    {
                        ports[serializableNode.nodeObject.inputPortGuids[0]] = token.input;
                        ports[serializableNode.nodeObject.outputPortGuids[0]] = token.output;
                    }
                    else
                    {
                        var iPorts = new Queue<Port>(node.inputContainer.Query<Port>().ToList());
                        foreach (var guid in serializableNode.nodeObject.inputPortGuids)
                        {
                            ports[guid] = iPorts.Dequeue();
                        }
                        var oPorts = new Queue<Port>(node.outputContainer.Query<Port>().ToList());
                        foreach (var guid in serializableNode.nodeObject.outputPortGuids)
                        {
                            ports[guid] = oPorts.Dequeue();
                        }
                    }
                    serializableNode.SaveSerializedFields();
                }
                else if (element is TextureGraphEdge edge)
                {
                    EditorJsonUtility.FromJsonOverwrite(subs[1], edge.edgeObject);
                    edges.Enqueue(edge);
                }
            }
            foreach (var edge in edges)
            {
                if (string.IsNullOrEmpty(edge.edgeObject.inputGuid) || string.IsNullOrEmpty(edge.edgeObject.outputGuid) || !ports.ContainsKey(edge.edgeObject.inputGuid) || !ports.ContainsKey(edge.edgeObject.outputGuid))
                    continue;
                var inputPort = ports[edge.edgeObject.inputGuid];
                var outputPort = ports[edge.edgeObject.outputGuid];
                edge.input = inputPort;
                edge.output = outputPort;
                inputPort.Connect(edge);
                outputPort.Connect(edge);
                AddElementWithRecord(edge);
            }
            var allRect = Rect.zero;
            foreach (var value in nodes.Values)
            {
                var rect = value.Item2.nodeObject.position;
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
            foreach (var value in nodes.Values)
            {
                var rect = value.Item2.nodeObject.position;
                rect.position += offset;
                value.Item1.SetPosition(rect);
            }
        }

        internal void MarkNordIsDirty()
        {
            m_RecalculateScheduledItem.Resume();
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

        void SaveAsAsset()
        {
            if (AssetDatabase.IsMainAsset(graphData))
                return;
            var path = @"Assets/NewTextureGraph.asset";
            AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(graphData, path);
            foreach (var data in m_NodeObjectHolder)
                AssetDatabase.AddObjectToAsset(data, graphData);
            foreach (var data in m_EdgeObjectHolder)
                AssetDatabase.AddObjectToAsset(data, graphData);
            AssetDatabase.ImportAsset(path);
            Selection.activeObject = graphData;
        }

        void SaveTexture()
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
            ProcessAll();
        }

        Texture2D ProcessAll()
        {
            processData.Clear();
            exportTextureNode.Process();
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(exportTextureNode.colors);
            texture.Apply();
            return texture;
        }
    }

    [Serializable]
    class TextureGraphEdge : Edge
    {
        public EdgeObject edgeObject { get; private set; }

        protected readonly SerializedObject serializedObject;
        protected readonly SerializedProperty m_InputGuid;
        protected readonly SerializedProperty m_OutputGuid;

        TextureGraph m_Graph;
        protected TextureGraph graph => m_Graph ?? (m_Graph = GetFirstAncestorOfType<TextureGraph>());

        bool isConnected = false;

        public TextureGraphEdge() : base()
        {
            edgeObject = ScriptableObject.CreateInstance<EdgeObject>();
            serializedObject = new SerializedObject(edgeObject);
            m_InputGuid = serializedObject.FindProperty("m_InputGuid");
            m_OutputGuid = serializedObject.FindProperty("m_OutputGuid");
            this.AddManipulator(new ContextualMenuManipulator(context => context.menu.AppendAction("Add Token", action => AddToken(action), action => DropdownMenu.MenuAction.StatusFlags.Normal)));
        }

        void AddToken(DropdownMenu.MenuAction action)
        {
            var inputPort = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, output.portType);
            var outputPort = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, input.portType);
            var token = new SerializableTokenNode(inputPort, outputPort);
            graph.AddElementWithRecord(token);
            var rect = new Rect(action.eventInfo.localMousePosition, Vector2.zero);
            token.SetPosition(rect);
            input.Disconnect(this);
            inputPort.Connect(this);
            graph.AddElementWithRecord(input.ConnectTo<TextureGraphEdge>(outputPort));
            input = inputPort;
        }

        public override void OnPortChanged(bool isInput)
        {
            base.OnPortChanged(isInput);
            if (isGhostEdge)
                return;
            serializedObject.Update();
            m_InputGuid.stringValue = input?.persistenceKey;
            m_OutputGuid.stringValue = output?.persistenceKey;
            serializedObject.ApplyModifiedProperties();
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

}
