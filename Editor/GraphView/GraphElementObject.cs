using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    public sealed class GraphElementObject : ScriptableObject, ISerializedGraphElement, ISerializationCallbackReceiver
    {
        GraphElementObject() { }

        [SerializeField]
        string m_Guid = "";
        [SerializeField]
        string m_TypeName = "";
        [SerializeField]
        Rect m_Position = Rect.zero;
        [SerializeField]
        List<string> m_ReferenceGuids = new List<string>();
        [SerializeField]
        SerializableFieldValue m_FieldValue = new SerializableFieldValue();

        SerializedObject m_SerializedObject;
        SerializedProperty m_GuidProperty;
        SerializedProperty m_PositionProperty;
        SerializedProperty m_ReferenceGuidsProperty;

        public string Guid
        {
            get => m_Guid;
            set
            {
                m_SerializedObject.Update();
                m_GuidProperty.stringValue = value;
                m_SerializedObject.ApplyModifiedProperties();
            }
        }
        public string TypeName { get => m_TypeName; set => m_TypeName = value; }
        public Rect Position
        {
            get => m_Position;
            set
            {
                m_SerializedObject.Update();
                m_PositionProperty.rectValue = value;
                m_SerializedObject.ApplyModifiedProperties();
            }
        }
        public IList<string> ReferenceGuids { get; private set; }
        public SerializableFieldValue FieldValue => m_FieldValue;

        void Awake()
        {
            hideFlags = HideFlags.DontSave;
        }

        void OnEnable()
        {
            m_SerializedObject = new SerializedObject(this);
            m_GuidProperty = m_SerializedObject.FindProperty("m_Guid");
            m_PositionProperty = m_SerializedObject.FindProperty("m_Position");
            m_ReferenceGuidsProperty = m_SerializedObject.FindProperty("m_ReferenceGuids");
        }

        void OnDestroy()
        {
            m_GuidProperty?.Dispose();
            m_PositionProperty?.Dispose();
            m_ReferenceGuidsProperty?.Dispose();
            m_SerializedObject?.Dispose();
            m_GuidProperty = null;
            m_PositionProperty = null;
            m_ReferenceGuidsProperty = null;
            m_SerializedObject = null;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            var callbackList = new CallbackList<string>(m_ReferenceGuids);
            callbackList.OnSetIndexer += (index, item) =>
            {
                m_SerializedObject.Update();
                using (var sp = m_ReferenceGuidsProperty.GetArrayElementAtIndex(index))
                    sp.stringValue = item;
                m_SerializedObject.ApplyModifiedProperties();
            };
            ReferenceGuids = callbackList;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
    }

    public sealed class CallbackList<T> : IList<T>
    {
        readonly IList<T> m_SourceList;

        public CallbackList(IList<T> list)
        {
            m_SourceList = list;
        }

        public T this[int index]
        {
            get => m_SourceList[index];
            set => OnSetIndexer?.Invoke(index, value);
        }

        public event Action<int, T> OnSetIndexer;

        public int Count => m_SourceList.Count;
        bool ICollection<T>.IsReadOnly => false;

        public int IndexOf(T item) => m_SourceList.IndexOf(item);
        public void Insert(int index, T item) => m_SourceList.Insert(index, item);
        public void RemoveAt(int index) => m_SourceList.RemoveAt(index);

        public void Add(T item) => m_SourceList.Add(item);
        public void Clear() => m_SourceList.Clear();
        public bool Contains(T item) => m_SourceList.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => m_SourceList.CopyTo(array, arrayIndex);
        public bool Remove(T item) => m_SourceList.Remove(item);
        public IEnumerator<T> GetEnumerator() => m_SourceList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => m_SourceList.GetEnumerator();
    }

    [Serializable]
    public sealed class SerializedGraphElement : ISerializedGraphElement
    {
        [SerializeField]
        string m_Guid;
        [SerializeField]
        string m_TypeName;
        [SerializeField]
        Rect m_Position;
        [SerializeField]
        List<string> m_ReferenceGuids = new List<string>();
        [SerializeField]
        SerializableFieldValue m_FieldValue = new SerializableFieldValue();

        public string Guid { get => m_Guid; set => m_Guid = value; }
        public string TypeName { get => m_TypeName; set => m_TypeName = value; }
        public Rect Position { get => m_Position; set => m_Position = value; }
        public IList<string> ReferenceGuids => m_ReferenceGuids;
        public SerializableFieldValue FieldValue => m_FieldValue;
    }

    public static class SerializedGraphElementExtensions
    {
        static readonly Dictionary<string, ConstructorInfo> s_ConstructorInfos = new Dictionary<string, ConstructorInfo>();

        public static T Serialize<T>(this GraphElement graphElement) where T : ScriptableObject, ISerializedGraphElement
        {
            if (graphElement == null)
                throw new ArgumentNullException("graphElement");
            var serializedGraphElement = ScriptableObject.CreateInstance<T>();
            return graphElement.SerializeInternal<T>(serializedGraphElement);
        }

        public static T Serialize<T>(this GraphElement graphElement, T existing) where T : class, ISerializedGraphElement, new()
        {
            if (graphElement == null)
                throw new ArgumentNullException("graphElement");
            var serializedGraphElement = existing ?? new T();
            return graphElement.SerializeInternal<T>(serializedGraphElement);
        }

        static T SerializeInternal<T>(this GraphElement graphElement, T serializedGraphElement) where T : ISerializedGraphElement
        {
            serializedGraphElement.Guid = graphElement.persistenceKey;
            serializedGraphElement.TypeName = graphElement.GetType().AssemblyQualifiedName;
            serializedGraphElement.Position = graphElement.GetPosition();
            var referenceGuids = serializedGraphElement.ReferenceGuids;
            switch (graphElement)
            {
                case Node node:
                    node.Query<Port>().ForEach(port => referenceGuids.Add(port.persistenceKey));
                    break;
                case Edge edge:
                    referenceGuids.Add(edge.input?.persistenceKey);
                    referenceGuids.Add(edge.output?.persistenceKey);
                    break;
            }
            return serializedGraphElement;
        }

        public static GraphElement Deserialize(this ISerializedGraphElement serializedGraphElement, GraphElement graphElement, GraphView graphView)
        {
            if (serializedGraphElement == null)
                throw new ArgumentNullException("serializedGraphElement");
            if (graphView == null)
                throw new ArgumentNullException("graphView");
            if (graphElement == null)
            {
                var typeName = serializedGraphElement.TypeName;
                if (!s_ConstructorInfos.TryGetValue(typeName, out var info))
                {
                    info = Type.GetType(typeName).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
                    s_ConstructorInfos[typeName] = info;
                }
                graphElement = info.Invoke(new object[0]) as GraphElement;
                graphView.AddElement(graphElement);
                if (graphElement is IBindableGraphElement bindable && serializedGraphElement is ScriptableObject scriptableObject)
                {
                    var so = new SerializedObject(scriptableObject);
                    using (var fieldSP = so.FindProperty("m_FieldValue"))
                    {
                        if (graphElement is IBindableGraphElement<float> floatField)
                        {
                            var arraySP = fieldSP.FindPropertyRelative("m_FloatValues");
                            floatField.Bind(arraySP);
                        }
                        if (graphElement is IBindableGraphElement<int> intField)
                        {
                            var arraySP = fieldSP.FindPropertyRelative("m_IntValues");
                            intField.Bind(arraySP);
                        }
                        if (graphElement is IBindableGraphElement<Vector4> vector4Field)
                        {
                            var arraySP = fieldSP.FindPropertyRelative("m_Vector4Values");
                            vector4Field.Bind(arraySP);
                        }
                        if (graphElement is IBindableGraphElement<AnimationCurve> curveField)
                        {
                            var arraySP = fieldSP.FindPropertyRelative("m_AnimationCurveValues");
                            curveField.Bind(arraySP);
                        }
                        if (graphElement is IBindableGraphElement<UnityEngine.Object> objectField)
                        {
                            var arraySP = fieldSP.FindPropertyRelative("m_ObjectReferenceValues");
                            objectField.Bind(arraySP);
                        }
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    if (graphView is IGraphViewCallback bindableGraphView)
                        bindable.onValueChanged += bindableGraphView.OnValueChanged;
                }
            }
            graphElement.persistenceKey = serializedGraphElement.Guid;
            graphElement.SetPosition(serializedGraphElement.Position);
            if (graphElement is Node node)
            {
                var guidsQueue = new Queue<string>(serializedGraphElement.ReferenceGuids);
                node.Query<Port>().ForEach(port => port.persistenceKey = guidsQueue.Dequeue());
            }
            return graphElement;
        }
    }
}
