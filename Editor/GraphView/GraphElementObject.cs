using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using MomomaAssets.Extensions;
using UnityObject = UnityEngine.Object;

namespace MomomaAssets
{
    public sealed class GraphElementObject : ScriptableObject, ISerializedGraphElement
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
        FieldValueObjectBase[] m_FieldValues = new FieldValueObjectBase[] { };

        SerializedObject m_SerializedObject;
        SerializedProperty m_GuidProperty;
        SerializedProperty m_PositionProperty;
        SerializedProperty m_ReferenceGuidsProperty;
        SerializedProperty m_FieldValuesProperty;

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
        public IReadOnlyList<IFieldValue> FieldValues => m_FieldValues;

        static readonly Dictionary<Type, Func<FieldValueObjectBase>> s_FieldValueObjectCreators = new Dictionary<Type, Func<FieldValueObjectBase>>
        {
            { typeof(float), CreateInstance<FloatValueObject> },
            { typeof(Color), CreateInstance<ColorValueObject> },
            { typeof(Vector4), CreateInstance<Vector4ValueObject> },
            { typeof(AnimationCurve), CreateInstance<AnimationCurveValueObject> },
            { typeof(UnityObject), CreateInstance<UnityObjectValueObject> },
        };

        public void AddFieldValue<T>(INotifyValueChanged<T> field)
        {
            if (!s_FieldValueObjectCreators.TryGetValue(typeof(T), out var getInstance))
                throw new ArgumentOutOfRangeException(nameof(T));
            var fieldValueObject = getInstance();
            var so = new SerializedObject(fieldValueObject);
            var sp = so.FindProperty("m_Value");
            sp.SetValue(field.value);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (field is IBindable bindable)
                bindable.BindProperty(sp);
            m_SerializedObject.Update();
            ++m_FieldValuesProperty.arraySize;
            using (var elementSP = m_FieldValuesProperty.GetArrayElementAtIndex(m_FieldValuesProperty.arraySize - 1))
                elementSP.objectReferenceValue = fieldValueObject;
            m_SerializedObject.ApplyModifiedProperties();
        }

        void Awake()
        {
            hideFlags = HideFlags.DontSave;
        }

        void OnEnable()
        {
            m_SerializedObject = new SerializedObject(this);
            m_GuidProperty = m_SerializedObject.FindProperty(nameof(m_Guid));
            m_PositionProperty = m_SerializedObject.FindProperty(nameof(m_Position));
            m_ReferenceGuidsProperty = m_SerializedObject.FindProperty(nameof(m_ReferenceGuids));
            m_FieldValuesProperty = m_SerializedObject.FindProperty(nameof(m_FieldValues));
            ReferenceGuids = new SerializedPropertyList<string>(m_ReferenceGuidsProperty, sp => sp.stringValue, (sp, val) => sp.stringValue = val);
        }

        void OnDestroy()
        {
            m_GuidProperty?.Dispose();
            m_PositionProperty?.Dispose();
            m_ReferenceGuidsProperty?.Dispose();
            m_FieldValuesProperty?.Dispose();
            m_SerializedObject?.Dispose();
            m_GuidProperty = null;
            m_PositionProperty = null;
            m_ReferenceGuidsProperty = null;
            m_FieldValuesProperty = null;
            m_SerializedObject = null;
        }
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
        List<FieldValue> m_FieldValues = new List<FieldValue>();

        public string Guid { get => m_Guid; set => m_Guid = value; }
        public string TypeName { get => m_TypeName; set => m_TypeName = value; }
        public Rect Position { get => m_Position; set => m_Position = value; }
        public IList<string> ReferenceGuids => m_ReferenceGuids;
        public IReadOnlyList<IFieldValue> FieldValues => m_FieldValues;

        public void AddFieldValue<T>(INotifyValueChanged<T> field)
        {
            switch (field.value)
            {
                case float floatValue:
                    m_FieldValues.Add(new FloatValue(floatValue));
                    break;
                case Color colorValue:
                    m_FieldValues.Add(new ColorValue(colorValue));
                    break;
                case Vector4 vector4Value:
                    m_FieldValues.Add(new Vector4Value(vector4Value));
                    break;
                case AnimationCurve animationCurveValue:
                    m_FieldValues.Add(new AnimationCurveValue(animationCurveValue));
                    break;
                case UnityObject unityObjectValue:
                    m_FieldValues.Add(new UnityObjectValue(unityObjectValue));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(T));
            }
        }
    }

    public static class SerializedGraphElementExtensions
    {
        public static void GetFieldValues<T>(this ISerializedGraphElement serializedGraphElement, params INotifyValueChanged<T>[] fields)
        {
            var index = 0;
            foreach (var fieldValue in serializedGraphElement.FieldValues)
            {
                if (fieldValue is IFieldValue<T> fieldValueGeneric)
                {
                    fields[index].value = fieldValueGeneric.Value;
                    ++index;
                }
            }
        }

        static readonly Dictionary<string, ConstructorInfo> s_ConstructorInfos = new Dictionary<string, ConstructorInfo>();

        public static T Serialize<T>(this GraphElement graphElement) where T : ScriptableObject, ISerializedGraphElement
        {
            var serializedGraphElement = ScriptableObject.CreateInstance<T>();
            return graphElement.SerializeInternal<T>(serializedGraphElement);
        }

        public static T Serialize<T>(this GraphElement graphElement, T existing) where T : class, ISerializedGraphElement, new()
        {
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
            if (graphElement is IBindableGraphElement bindableGraphElement)
            {
                bindableGraphElement.SetFieldValues(serializedGraphElement);
            }
            return serializedGraphElement;
        }

        public static GraphElement Deserialize<TGraphView>(this ISerializedGraphElement serializedGraphElement, GraphElement graphElement, TGraphView graphView) where TGraphView : GraphView, IGraphViewCallback
        {
            if (graphView == null)
                throw new ArgumentNullException(nameof(graphView));
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
                if (graphElement is IBindableGraphElement bindable)
                {
                    bindable.GetFieldValues(serializedGraphElement);
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
