using System;
using System.Linq;
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
    public sealed class NodeGraph<TGraphView, TEdge> : IDisposable where TGraphView : GraphView, IGraphViewCallback, new() where TEdge : Edge, IEdgeCallback, new()
    {
        sealed class GraphViewObjectHandler : IDisposable
        {
            ~GraphViewObjectHandler() { Dispose(); }

            public string GraphViewTypeName
            {
                get
                {
                    using (var sp = m_SerializedObject.FindProperty("m_GraphViewTypeName"))
                        return sp.stringValue;
                }
                set
                {
                    using (var sp = m_SerializedObject.FindProperty("m_GraphViewTypeName"))
                        sp.stringValue = value;
                }
            }
            public bool CanFullReload => GraphViewObject != null && m_SerializedObject.UpdateIfRequiredOrScript();
            public GraphViewObject GraphViewObject
            {
                get => m_GraphViewObject;
                set
                {
                    if (m_GraphViewObject != value)
                    {
                        m_GraphViewObject = value;
                        Dispose();
                        if (m_GraphViewObject != null)
                        {
                            m_SerializedObject = new SerializedObject(m_GraphViewObject);
                            m_SerializedGraphElementsProperty = m_SerializedObject.FindProperty("m_SerializedGraphElements");
                        }
                    }
                }
            }

            SerializedObject m_SerializedObject;
            SerializedProperty m_SerializedGraphElementsProperty;
            GraphViewObject m_GraphViewObject;

            public void Dispose()
            {
                m_SerializedGraphElementsProperty?.Dispose();
                m_SerializedObject?.Dispose();
                m_SerializedGraphElementsProperty = null;
                m_SerializedObject = null;
            }

            void CheckObjectExistence()
            {
                if (m_SerializedObject != null && m_SerializedObject.targetObject == null)
                    Dispose();
            }

            public void Update()
            {
                CheckObjectExistence();
                m_SerializedObject?.Update();
            }

            public bool ApplyModifiedProperties()
            {
                CheckObjectExistence();
                return m_SerializedObject?.ApplyModifiedProperties() ?? false;
            }

            public bool ApplyModifiedPropertiesWithoutUndo()
            {
                CheckObjectExistence();
                return m_SerializedObject?.ApplyModifiedPropertiesWithoutUndo() ?? false;
            }

            public void CreateMainAsset(string pathName)
            {
                GraphViewObject.hideFlags &= ~HideFlags.DontSaveInEditor;
                AssetDatabase.CreateAsset(GraphViewObject, pathName);
                GraphViewObject.hideFlags |= HideFlags.DontSaveInEditor;
            }

            public void AddGraphElementObject(GraphElementObject graphElementObject)
            {
                CheckObjectExistence();
                if (m_SerializedGraphElementsProperty == null)
                    return;
                graphElementObject.hideFlags &= ~HideFlags.DontSaveInEditor;
                AssetDatabase.AddObjectToAsset(graphElementObject, AssetDatabase.GetAssetPath(GraphViewObject));
                graphElementObject.hideFlags |= HideFlags.DontSaveInEditor;
                ++m_SerializedGraphElementsProperty.arraySize;
                using (var sp = m_SerializedGraphElementsProperty.GetArrayElementAtIndex(m_SerializedGraphElementsProperty.arraySize - 1))
                    sp.objectReferenceValue = graphElementObject;
            }

            public UnityObject DeleteGraphElementObjectAtIndex(int index)
            {
                CheckObjectExistence();
                if (m_SerializedGraphElementsProperty == null)
                    return null;
                using (var sp = m_SerializedGraphElementsProperty.GetArrayElementAtIndex(index))
                {
                    var obj = sp.objectReferenceValue;
                    sp.objectReferenceValue = null;
                    sp.DeleteCommand();
                    obj.hideFlags &= ~HideFlags.DontSaveInEditor;
                    return obj;
                }
            }

            public GraphElementObject GetGraphElementObjectAtIndex(int index)
            {
                CheckObjectExistence();
                if (m_SerializedGraphElementsProperty == null)
                    return null;
                using (var sp = m_SerializedGraphElementsProperty.GetArrayElementAtIndex(index))
                    return sp.objectReferenceValue as GraphElementObject;
            }
        }

        readonly TGraphView m_GraphView;
        readonly EditorWindow m_EditorWindow;
        readonly SearchWindowProvider m_SearchWindowProvider;
        readonly GraphViewObjectHandler m_GraphViewObjectHandler;
        readonly VisualElement m_CreateGraphButton;

        bool isDisposed = false;

        public NodeGraph(EditorWindow editorWindow)
        {
            if (editorWindow == null)
                throw new ArgumentNullException("editorWindow");
            m_EditorWindow = editorWindow;
            m_GraphView = new TGraphView() { style = { flexGrow = 1 } };
            m_EditorWindow.GetRootVisualContainer().Add(m_GraphView);
            m_GraphView.serializeGraphElements = SerializeGraphElements;
            m_GraphView.unserializeAndPaste = UnserializeAndPaste;
            m_GraphView.graphViewChanged = GraphViewChanged;
            m_GraphView.AddStyleSheetPath("TextureGraphStyles");
            m_GraphView.Insert(0, new GridBackground() { style = { alignItems = Align.Center, justifyContent = Justify.Center } });
            m_GraphView.Add(new MiniMap());
            m_GraphView.AddManipulator(new SelectionDragger());
            m_GraphView.AddManipulator(new ContentDragger());
            m_GraphView.AddManipulator(new ContentZoomer());
            m_GraphView.AddManipulator(new RectangleSelector());
            m_GraphView.persistenceKey = Guid.NewGuid().ToString();
            m_SearchWindowProvider = ScriptableObject.CreateInstance<SearchWindowProvider>();
            m_SearchWindowProvider.addGraphElement += AddElement;
            m_SearchWindowProvider.graphViewType = typeof(TGraphView);
            m_GraphView.nodeCreationRequest = context => { if (m_GraphViewObjectHandler.GraphViewObject != null) SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), m_SearchWindowProvider); };
            m_CreateGraphButton = new VisualElement() { style = { positionType = PositionType.Absolute, flexDirection = FlexDirection.Row, justifyContent = Justify.Center } };
            m_GraphView.Insert(1, m_CreateGraphButton);
            m_CreateGraphButton.StretchToParentSize();
            m_CreateGraphButton.Add(new Button(CreateGraphObjectAsset) { text = "Create Graph", style = { alignSelf = Align.Center } });
            m_GraphViewObjectHandler = new GraphViewObjectHandler();
            OnSelectionChanged();
            Undo.undoRedoPerformed += FullReload;
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
        }

        ~NodeGraph() { Dispose(); }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            Undo.undoRedoPerformed -= FullReload;
            EditorApplication.update -= Update;
            Selection.selectionChanged -= OnSelectionChanged;
            m_GraphViewObjectHandler.Dispose();
            ScriptableObject.DestroyImmediate(m_SearchWindowProvider);
            if (m_GraphView is IDisposable disposable)
                disposable.Dispose();
        }

        void Update()
        {
            if (m_GraphViewObjectHandler.GraphViewObject == null && !m_CreateGraphButton.visible)
            {
                m_GraphView.DeleteElements(m_GraphView.graphElements.ToList());
                m_CreateGraphButton.visible = true;
            }
        }

        void OnSelectionChanged()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is GraphViewObject graphViewObject)
                {
                    if (graphViewObject.GraphViewType == typeof(TGraphView))
                    {
                        m_GraphViewObjectHandler.GraphViewObject = graphViewObject;
                        m_CreateGraphButton.visible = false;
                        FullReload();
                    }
                }
            }
        }

        void CreateGraphObjectAsset()
        {
            if (m_GraphViewObjectHandler.GraphViewObject != null)
                return;
            var graphViewObject = ScriptableObject.CreateInstance<GraphViewObject>();
            m_GraphViewObjectHandler.GraphViewObject = graphViewObject;
            var endAction = ScriptableObject.CreateInstance<CreateGraphObjectEndAction>();
            endAction.OnEndNameEdit += OnEndNameEdit;
            var icon = AssetPreview.GetMiniThumbnail(graphViewObject);
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(graphViewObject.GetInstanceID(), endAction, "NewTextureGraph.asset", icon, null);
        }

        void OnEndNameEdit(string pathName)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                m_GraphViewObjectHandler.CreateMainAsset(pathName);
                m_GraphView.Initialize();
                var initialiElements = m_GraphView.graphElements.ToList();
                m_GraphView.DeleteElements(initialiElements);
                m_GraphViewObjectHandler.Update();
                foreach (var element in initialiElements)
                {
                    var graphElementObject = element.Serialize<GraphElementObject>();
                    m_GraphViewObjectHandler.AddGraphElementObject(graphElementObject);
                    graphElementObject.Deserialize(null, m_GraphView);
                }
                m_GraphViewObjectHandler.GraphViewTypeName = typeof(TGraphView).AssemblyQualifiedName;
                m_GraphViewObjectHandler.ApplyModifiedPropertiesWithoutUndo();
                m_CreateGraphButton.visible = false;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.ImportAsset(pathName);
            AssetDatabase.SaveAssets();
            ProjectWindowUtil.ShowCreatedAsset(m_GraphViewObjectHandler.GraphViewObject);
        }

        GraphViewChange GraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.edgesToCreate != null)
            {
                m_GraphViewObjectHandler.Update();
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    if (edge is IEdgeCallback edgeCallback)
                    {
                        var graphElementObject = edge.Serialize<GraphElementObject>();
                        m_GraphViewObjectHandler.AddGraphElementObject(graphElementObject);
                        Undo.RegisterCreatedObjectUndo(graphElementObject, $"Create {edge.GetType().Name}");
                        edgeCallback.onPortChanged -= OnPortChanged;
                        edgeCallback.onPortChanged += OnPortChanged;
                        m_GraphView.OnValueChanged(edge);
                    }
                    edge.AddManipulator(new ContextualMenuManipulator(context => context.menu.AppendAction("Add Token", action => AddToken(edge, action), action => DropdownMenu.MenuAction.StatusFlags.Normal)));
                }
                m_GraphViewObjectHandler.ApplyModifiedProperties();
            }
            if (graphViewChange.elementsToRemove != null)
            {
                m_GraphViewObjectHandler.Update();
                var indicesToRemove = new List<int>();
                foreach (var element in graphViewChange.elementsToRemove)
                {
                    if (m_GraphViewObjectHandler.GraphViewObject.GuidToIndices.TryGetValue(element.persistenceKey, out var index))
                        indicesToRemove.Add(index);
                }
                indicesToRemove.Sort();
                var objectsToDelete = new List<UnityObject>();
                for (var i = indicesToRemove.Count - 1; i > -1; --i)
                {
                    objectsToDelete.Add(m_GraphViewObjectHandler.DeleteGraphElementObjectAtIndex(indicesToRemove[i]));
                }
                m_GraphViewObjectHandler.ApplyModifiedProperties();
                foreach (var obj in objectsToDelete)
                    Undo.DestroyObjectImmediate(obj);
            }
            if (graphViewChange.movedElements != null)
            {
                foreach (var element in graphViewChange.movedElements)
                {
                    if (m_GraphViewObjectHandler.GraphViewObject.GuidToIndices.TryGetValue(element.persistenceKey, out var index))
                        m_GraphViewObjectHandler.GetGraphElementObjectAtIndex(index).Position = element.GetPosition();
                }
            }
            return graphViewChange;
        }

        string SerializeGraphElements(IEnumerable<GraphElement> elements)
        {
            var serializedGraphView = new SerializedGraphView();
            foreach (var element in elements)
                serializedGraphView.SerializedGraphElements.Add(element.Serialize<SerializedGraphElement>(null));
            return JsonUtility.ToJson(serializedGraphView);
        }

        void UnserializeAndPaste(string operationName, string data)
        {
            m_GraphViewObjectHandler.Update();
            var serializedGraphElements = JsonUtility.FromJson<SerializedGraphView>(data).SerializedGraphElements;
            var guidsToReplace = new Dictionary<string, string>();
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                string newGuid;
                if (!guidsToReplace.TryGetValue(serializedGraphElement.Guid, out newGuid))
                {
                    newGuid = Guid.NewGuid().ToString();
                    guidsToReplace[serializedGraphElement.Guid] = newGuid;
                }
                serializedGraphElement.Guid = newGuid;
                for (var i = 0; i < serializedGraphElement.ReferenceGuids.Count; ++i)
                {
                    if (!guidsToReplace.TryGetValue(serializedGraphElement.ReferenceGuids[i], out newGuid))
                    {
                        newGuid = Guid.NewGuid().ToString();
                        guidsToReplace[serializedGraphElement.ReferenceGuids[i]] = newGuid;
                    }
                    serializedGraphElement.ReferenceGuids[i] = newGuid;
                }
            }
            var allRect = Rect.zero;
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                var rect = serializedGraphElement.Position;
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
            var offset = m_GraphView.contentViewContainer.WorldToLocal(m_GraphView.contentRect.center) - allRect.center;
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                var rect = serializedGraphElement.Position;
                rect.position += offset;
                serializedGraphElement.Position = rect;
            }
            var guids = new Dictionary<string, GraphElement>();
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                var graphElement = serializedGraphElement.Deserialize(null, m_GraphView);
                graphElement.Query<GraphElement>().ForEach(e => guids[e.persistenceKey] = e);
            }
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                PostDeserialize(serializedGraphElement, guids);
            }
            m_GraphViewObjectHandler.ApplyModifiedProperties();
        }

        void FullReload()
        {
            if (!m_GraphViewObjectHandler.CanFullReload)
                return;
            var serializedGraphElements = m_GraphViewObjectHandler.GraphViewObject.SerializedGraphElements.ToDictionary(element => element.Guid, element => element);
            var elementsToRemove = new HashSet<GraphElement>(m_GraphView.graphElements.ToList());
            elementsToRemove.RemoveWhere(element => serializedGraphElements.ContainsKey(element.persistenceKey));
            m_GraphView.DeleteElements(elementsToRemove);
            var guids = m_GraphView.graphElements.ToList().ToDictionary(element => element.persistenceKey, element => element);
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                if (guids.TryGetValue(serializedGraphElement.Key, out var graphElement))
                    graphElement = serializedGraphElement.Value.Deserialize(graphElement, m_GraphView);
                else
                    graphElement = serializedGraphElement.Value.Deserialize(null, m_GraphView);
                graphElement.Query<GraphElement>().ForEach(e => guids[e.persistenceKey] = e);
            }
            foreach (var serializedGraphElement in serializedGraphElements)
            {
                PostDeserialize(serializedGraphElement.Value, guids);
            }
        }

        void OnPortChanged(Edge edge)
        {
            if (edge.isGhostEdge)
                return;
            if (m_GraphViewObjectHandler.GraphViewObject.GuidToIndices.TryGetValue(edge.persistenceKey, out var index))
            {
                if (edge.input != null && edge.output != null)
                {
                    var referenceGuids = m_GraphViewObjectHandler.GetGraphElementObjectAtIndex(index).ReferenceGuids;
                    referenceGuids[0] = edge.input.persistenceKey;
                    referenceGuids[1] = edge.output.persistenceKey;
                }
            }
            m_GraphView.OnValueChanged(edge);
        }

        void AddToken(Edge edge, DropdownMenu.MenuAction action)
        {
            var inputPort = Port.Create<TEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, edge.output.portType);
            var outputPort = Port.Create<TEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, edge.input.portType);
            var token = new TokenNode<TEdge>(inputPort, outputPort);
            AddElement(token, m_EditorWindow.position.position + action.eventInfo.mousePosition);
            edge.input.Disconnect(edge);
            inputPort.Connect(edge);
            edge.input.ConnectTo<TEdge>(outputPort);
            edge.input = inputPort;
        }

        public void PostDeserialize(ISerializedGraphElement serializedGraphElement, Dictionary<string, GraphElement> guids)
        {
            var graphElement = guids[serializedGraphElement.Guid];
            if (graphElement is Edge edge)
            {
                guids.TryGetValue(serializedGraphElement.ReferenceGuids[0], out var inputPort);
                guids.TryGetValue(serializedGraphElement.ReferenceGuids[1], out var outputPort);
                var changed = false;
                if (edge.input != inputPort)
                {
                    edge.input?.Disconnect(edge);
                    edge.input = inputPort as Port;
                    edge.input.Connect(edge);
                    changed = true;
                }
                if (edge.output != outputPort)
                {
                    edge.output?.Disconnect(edge);
                    edge.output = outputPort as Port;
                    edge.output.Connect(edge);
                    changed = true;
                }
                if (edge.output == null || edge.input == null)
                {
                    m_GraphView.RemoveElement(edge);
                    changed = true;
                }
                if (changed)
                    m_GraphView.OnValueChanged(edge);
            }
        }

        public void AddElement(GraphElement graphElement, Vector2 screenMousePosition)
        {
            if (graphElement == null)
                throw new ArgumentNullException("graphElement");
            if (m_GraphView.Contains(graphElement))
                throw new UnityException($"{m_GraphView} has already contained {graphElement}.");
            var graphElementObject = graphElement.Serialize<GraphElementObject>();
            var position = Rect.zero;
            var root = m_EditorWindow.GetRootVisualContainer();
            position.center = m_GraphView.contentViewContainer.WorldToLocal(root.ChangeCoordinatesTo(root.parent ?? root, screenMousePosition - m_EditorWindow.position.position));
            graphElementObject.Position = position;
            m_GraphViewObjectHandler.Update();
            m_GraphViewObjectHandler.AddGraphElementObject(graphElementObject);
            m_GraphViewObjectHandler.ApplyModifiedProperties();
            Undo.RegisterCreatedObjectUndo(graphElementObject, $"Create {graphElement.GetType().Name}");
            graphElementObject.Deserialize(null, m_GraphView);
            if (graphElement is IBindableGraphElement bindable)
                bindable.Reset();
        }
    }

    [Serializable]
    public sealed class SerializableFieldValue
    {
        public IReadOnlyList<float> FloatValues => m_FloatValues;
        public IReadOnlyList<int> IntValues => m_IntValues;
        public IReadOnlyList<Vector4> Vector4Values => m_Vector4Values;
        public IReadOnlyList<AnimationCurve> AnimationCurveValues => m_AnimationCurveValues;
        public IReadOnlyList<UnityObject> ObjectReferenceValues => m_ObjectReferenceValues;

        [SerializeField]
        float[] m_FloatValues = new float[0];
        [SerializeField]
        int[] m_IntValues = new int[0];
        [SerializeField]
        Vector4[] m_Vector4Values = new Vector4[0];
        [SerializeField]
        AnimationCurve[] m_AnimationCurveValues = new AnimationCurve[0];
        [SerializeField]
        UnityObject[] m_ObjectReferenceValues = new UnityObject[0];
    }
}
