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

        void OnDisable()
        {
            m_TextureGraph = null;
        }
    }

    class TextureGraph : GraphView
    {
        internal readonly Dictionary<Port, object> processData = new Dictionary<Port, object>();
        internal readonly TextureGraphWindow window;

        TextureGraphData m_GraphData;
        internal TextureGraphData graphData
        {
            get
            {
                if (m_GraphData == null)
                    CreateDataObject();
                return m_GraphData;
            }

            set
            {
                if (value == null)
                    throw new ArgumentNullException("graphData");
                if (value == m_GraphData)
                    return;
                m_GraphData = value;
                m_SerializedObject = new SerializedObject(m_GraphData);
                m_Nodes = m_SerializedObject.FindProperty("m_Nodes");
                m_Edges = m_SerializedObject.FindProperty("m_Edges");
                FullReload();
            }
        }

        SerializedObject m_SerializedObject;
        SerializedObject serializedObject
        {
            get
            {
                if (m_SerializedObject == null || m_SerializedObject.targetObject == null)
                    CreateDataObject();
                return m_SerializedObject;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("serializedObject");
                m_SerializedObject = value;
            }
        }
        SerializedProperty m_Nodes;
        SerializedProperty m_Edges;

        ExportTextureNode m_ExportTextureNode;
        internal ExportTextureNode exportTextureNode => m_ExportTextureNode ?? (m_ExportTextureNode = nodes.ToList().Find(n => n is ExportTextureNode) as ExportTextureNode);

        readonly PreviewWindow m_PreviewWindow;
        readonly IVisualElementScheduledItem m_RecalculateScheduledItem;

        internal int width => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Width").value : 128;
        internal int height => isProduction ? exportTextureNode.extensionContainer.Q<PopupField<int>>("Height").value : 128;

        bool isProduction = false;

        internal TextureGraph(TextureGraphWindow window) : base()
        {
            this.window = window;
            CreateDataObject();
            AddStyleSheetPath("TextureGraphStyles");
            var background = new GridBackground() { style = { alignItems = Align.Center, justifyContent = Justify.Center } };
            Insert(0, background);
            m_PreviewWindow = new PreviewWindow();
            Add(m_PreviewWindow);
            Add(new MiniMap());
            Add(new Button(SaveAsAsset) { text = "Save Graph", style = { alignSelf = Align.FlexEnd } });
            Add(new Button(SaveTexture) { text = "Export Texture", style = { alignSelf = Align.FlexEnd } });
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
                UnityEngine.Object.DestroyImmediate(m_PreviewWindow.image);
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(graphData)))
                UnityEngine.Object.DestroyImmediate(graphData);
        }

        void CreateDataObject()
        {
            graphData = ScriptableObject.CreateInstance<TextureGraphData>();
            graphData.hideFlags = HideFlags.DontSave;
            m_ExportTextureNode = new ExportTextureNode();
            AddElementWithRecord(m_ExportTextureNode, true);
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

        internal void UpdateNodeObject(ISerializableNode node, bool withoutUndo = false)
        {
            var index = Array.FindIndex(graphData.nodes, d => d.guid == node.guid);
            if (index < 0)
                return;
            serializedObject.Update();
            m_Nodes.GetArrayElementAtIndex(index).FindPropertyRelative("m_SerializedNodeObject").stringValue = EditorJsonUtility.ToJson(node.nodeObject);
            if (withoutUndo)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            else
                serializedObject.ApplyModifiedProperties();
        }

        internal void UpdateEdgeObject(TextureGraphEdge edge, bool withoutUndo = false)
        {
            var index = Array.FindIndex(graphData.edges, d => d.guid == edge.guid);
            if (index < 0)
                return;
            serializedObject.Update();
            m_Edges.GetArrayElementAtIndex(index).FindPropertyRelative("m_SerializedEdgeObject").stringValue = EditorJsonUtility.ToJson(edge.edgeObject);
            if (withoutUndo)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            else
                serializedObject.ApplyModifiedProperties();
        }

        internal void AddElementWithRecord(GraphElement element, bool withoutUndo = false, bool onlyRecord = false)
        {
            if (!onlyRecord)
                AddElement(element);
            if (element is ISerializableNode serializableNode)
            {
                if (Array.Exists(graphData.nodes, data => data.guid == serializableNode.guid))
                    return;
                serializedObject.Update();
                ++m_Nodes.arraySize;
                var nodeData = m_Nodes.GetArrayElementAtIndex(m_Nodes.arraySize - 1);
                nodeData.FindPropertyRelative("m_TypeName").stringValue = element.GetType().AssemblyQualifiedName;
                nodeData.FindPropertyRelative("m_Guid").stringValue = serializableNode.guid;
                nodeData.FindPropertyRelative("m_SerializedNodeObject").stringValue = EditorJsonUtility.ToJson(serializableNode.nodeObject);
                if (withoutUndo)
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                else
                    serializedObject.ApplyModifiedProperties();
            }
            else if (element is TextureGraphEdge edge)
            {
                if (Array.Exists(graphData.edges, data => data.guid == edge.guid))
                    return;
                serializedObject.Update();
                ++m_Edges.arraySize;
                var edgeData = m_Edges.GetArrayElementAtIndex(m_Edges.arraySize - 1);
                edgeData.FindPropertyRelative("m_TypeName").stringValue = element.GetType().AssemblyQualifiedName;
                edgeData.FindPropertyRelative("m_Guid").stringValue = edge.guid;
                edgeData.FindPropertyRelative("m_SerializedEdgeObject").stringValue = EditorJsonUtility.ToJson(edge.edgeObject);
                if (withoutUndo)
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                else
                    serializedObject.ApplyModifiedProperties();
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
                        var index = Array.FindIndex(graphData.nodes, data => data.guid == serializableNode.guid);
                        if (index >= 0)
                        {
                            serializedObject.Update();
                            m_Nodes.DeleteArrayElementAtIndex(index);
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                    else if (element is TextureGraphEdge edge)
                    {
                        var index = Array.FindIndex(graphData.edges, data => data.guid == edge.guid);
                        if (index >= 0)
                        {
                            serializedObject.Update();
                            m_Edges.DeleteArrayElementAtIndex(index);
                            serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }
            return graphViewChange;
        }

        void UndoRedoPerformed()
        {
            FullReload();
        }

        void FullReload()
        {
            var viewNodeGuids = nodes.ToList().ToDictionary(n => (n as ISerializableNode).guid, n => n as ISerializableNode);
            var dataNodeGuids = new HashSet<string>(graphData.nodes.Select(d => d.guid));
            var toRemoveNodes = nodes.ToList().Where(e => !dataNodeGuids.Contains((e as ISerializableNode).guid)).ToArray();
            foreach (var data in graphData.nodes)
            {
                if (!viewNodeGuids.ContainsKey(data.guid))
                {
                    var node = Activator.CreateInstance(Type.GetType(data.typeName), true) as Node;
                    AddElement(node);
                    var serializableNode = node as ISerializableNode;
                    EditorJsonUtility.FromJsonOverwrite(data.serializedNodeObject, serializableNode.nodeObject);
                    serializableNode.LoadSerializedFields();
                }
                else
                {
                    var serializableNode = viewNodeGuids[data.guid];
                    EditorJsonUtility.FromJsonOverwrite(data.serializedNodeObject, serializableNode.nodeObject);
                    serializableNode.LoadSerializedFields();
                }
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
            var viewEdgeGuids = edges.ToList().ToDictionary(e => (e as TextureGraphEdge).guid, edges => edges as TextureGraphEdge);
            var dataEdgeGuids = new HashSet<string>(graphData.edges.Select(d => d.guid));
            var toRemoveEdges = edges.ToList().Where(e => !dataEdgeGuids.Contains((e as TextureGraphEdge).guid)).ToArray();
            foreach (var data in graphData.edges)
            {
                TextureGraphEdge edge;
                var isNewEdge = !viewEdgeGuids.TryGetValue(data.guid, out edge);
                if (isNewEdge)
                {
                    edge = Activator.CreateInstance(Type.GetType(data.typeName), true) as TextureGraphEdge;
                }
                EditorJsonUtility.FromJsonOverwrite(data.serializedEdgeObject, edge.edgeObject);
                if (string.IsNullOrEmpty(edge.edgeObject.inputGuid) || string.IsNullOrEmpty(edge.edgeObject.outputGuid) || !ports.ContainsKey(edge.edgeObject.inputGuid) || !ports.ContainsKey(edge.edgeObject.outputGuid))
                    continue;
                var inputPort = ports[edge.edgeObject.inputGuid];
                var outputPort = ports[edge.edgeObject.outputGuid];
                if (edge.input != inputPort)
                {
                    edge.input?.Disconnect(edge);
                    edge.input = inputPort;
                    inputPort.Connect(edge);
                }
                if (edge.output != outputPort)
                {
                    edge.output?.Disconnect(edge);
                    edge.output = outputPort;
                    outputPort.Connect(edge);
                }
                if (isNewEdge)
                {
                    AddElement(edge);
                    MarkNordIsDirty();
                }
                edge.LoadSerializedFields();
            }
            foreach (var edge in toRemoveEdges)
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                edge.input = null;
                edge.output = null;
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
                edge.SaveSerializedFields();
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

        internal void MarkNordIsDirty(ISerializableNode node)
        {
            UpdateNodeObject(node);
            MarkNordIsDirty();
        }

        internal void MarkNordIsDirty(TextureGraphEdge edge)
        {
            UpdateEdgeObject(edge);
            MarkNordIsDirty();
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
            graphData.hideFlags &= ~HideFlags.DontSaveInEditor;
            AssetDatabase.CreateAsset(graphData, path);
            graphData.hideFlags |= HideFlags.DontSaveInEditor;
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
                path = Path.ChangeExtension(path, "png");
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
        public string guid => m_Guid.stringValue;

        protected readonly SerializedObject serializedObject;
        protected readonly SerializedProperty m_Guid;
        protected readonly SerializedProperty m_InputGuid;
        protected readonly SerializedProperty m_OutputGuid;

        TextureGraph m_Graph;
        protected TextureGraph graph => m_Graph ?? (m_Graph = GetFirstAncestorOfType<TextureGraph>());

        bool isConnected = false;

        public TextureGraphEdge() : base()
        {
            edgeObject = ScriptableObject.CreateInstance<EdgeObject>();
            serializedObject = new SerializedObject(edgeObject);
            m_Guid = serializedObject.FindProperty("m_Guid");
            m_InputGuid = serializedObject.FindProperty("m_InputGuid");
            m_OutputGuid = serializedObject.FindProperty("m_OutputGuid");
            m_Guid.stringValue = persistenceKey;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            this.AddManipulator(new ContextualMenuManipulator(context => context.menu.AppendAction("Add Token", action => AddToken(action), action => DropdownMenu.MenuAction.StatusFlags.Normal)));
        }

        ~TextureGraphEdge()
        {
            UnityEngine.Object.DestroyImmediate(edgeObject);
        }

        public virtual void SaveSerializedFields()
        {
            serializedObject.Update();
            m_Guid.stringValue = persistenceKey;
            serializedObject.ApplyModifiedProperties();
        }

        public virtual void LoadSerializedFields()
        {
            serializedObject.Update();
            persistenceKey = m_Guid.stringValue;
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
                    if (isConnected)
                        graph.MarkNordIsDirty(this);
                    else
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
