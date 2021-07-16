using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
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
        List<float> m_FloatValues = new List<float>();
        [SerializeField]
        List<int> m_IntValues = new List<int>();
        [SerializeField]
        List<Color> m_ColorValues = new List<Color>();
        [SerializeField]
        List<Vector4> m_Vector4Values = new List<Vector4>();
        [SerializeField]
        List<AnimationCurve> m_AnimationCurveValues = new List<AnimationCurve>();
        [SerializeField]
        List<UnityObject> m_UnityObjectValues = new List<UnityObject>();

        SerializedObject m_SerializedObject;
        SerializedProperty m_GuidProperty;
        SerializedProperty m_TypeNameProperty;
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

        public string TypeName
        {
            get => m_TypeName;
            set
            {
                m_SerializedObject.Update();
                m_TypeNameProperty.stringValue = value;
                m_SerializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
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

        static readonly Dictionary<Type, string> s_FieldValueListNames = new Dictionary<Type, string>
        {
            { typeof(float), nameof(m_FloatValues) },
            { typeof(int), nameof(m_IntValues) },
            { typeof(Color), nameof(m_ColorValues) },
            { typeof(Vector4), nameof(m_Vector4Values) },
            { typeof(AnimationCurve), nameof(m_AnimationCurveValues) },
            { typeof(UnityObject), nameof(m_UnityObjectValues) },
        };

        public void SetFieldValues<T>(IGraphViewCallback graphView, params INotifyValueChanged<T>[] fields)
        {
            if (!s_FieldValueListNames.TryGetValue(typeof(T), out var propertyName))
                throw new ArgumentOutOfRangeException(nameof(T));
            m_SerializedObject.Update();
            using (var listSP = m_SerializedObject.FindProperty(propertyName))
            {
                listSP.arraySize = fields.Length;
                for (var i = 0; i < fields.Length; ++i)
                {
                    var field = fields[i];
                    var sp = listSP.GetArrayElementAtIndex(i);
                    FieldValueToSerializedValue(sp, field);
                    if (field is VisualElement visualElement)
                        field.OnValueChanged(evt => graphView.OnValueChanged(visualElement));
                    if (field is IBindable bindable)
                        bindable.BindProperty(sp);
                }
            }
            m_SerializedObject.ApplyModifiedProperties();
        }

        static void FieldValueToSerializedValue<T>(SerializedProperty property, INotifyValueChanged<T> field)
        {
            switch (field)
            {
                case INotifyValueChanged<int> castedField:
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Enum:
                            property.enumValueIndex = castedField.value;
                            break;
                        case SerializedPropertyType.ArraySize:
                            property.arraySize = castedField.value;
                            break;
                        default:
                            property.intValue = castedField.value;
                            break;
                    }
                    break;
                case INotifyValueChanged<bool> castedField:
                    property.boolValue = castedField.value;
                    break;
                case INotifyValueChanged<float> castedField:
                    property.floatValue = castedField.value;
                    break;
                case INotifyValueChanged<string> castedField:
                    property.stringValue = castedField.value;
                    break;
                case INotifyValueChanged<Color> castedField:
                    property.colorValue = castedField.value;
                    break;
                case INotifyValueChanged<UnityObject> castedField:
                    property.objectReferenceValue = castedField.value;
                    break;
                case INotifyValueChanged<Vector2> castedField:
                    property.vector2Value = castedField.value;
                    break;
                case INotifyValueChanged<Vector3> castedField:
                    property.vector3Value = castedField.value;
                    break;
                case INotifyValueChanged<Vector4> castedField:
                    property.vector4Value = castedField.value;
                    break;
                case INotifyValueChanged<Rect> castedField:
                    property.rectValue = castedField.value;
                    break;
                case INotifyValueChanged<AnimationCurve> castedField:
                    property.animationCurveValue = castedField.value;
                    break;
                case INotifyValueChanged<Bounds> castedField:
                    property.boundsValue = castedField.value;
                    break;
                case INotifyValueChanged<Quaternion> castedField:
                    property.quaternionValue = castedField.value;
                    break;
                case INotifyValueChanged<Vector2Int> castedField:
                    property.vector2IntValue = castedField.value;
                    break;
                case INotifyValueChanged<Vector3Int> castedField:
                    property.vector3IntValue = castedField.value;
                    break;
                case INotifyValueChanged<RectInt> castedField:
                    property.rectIntValue = castedField.value;
                    break;
                case INotifyValueChanged<BoundsInt> castedField:
                    property.boundsIntValue = castedField.value;
                    break;
                case null:
                    throw new ArgumentNullException(nameof(field));
                default:
                    throw new ArgumentOutOfRangeException(nameof(T));
            }
        }

        public void GetFieldValues<T>(IGraphViewCallback graphView, params INotifyValueChanged<T>[] fields)
        {
            if (!s_FieldValueListNames.TryGetValue(typeof(T), out var propertyName))
                throw new ArgumentOutOfRangeException(nameof(T));
            m_SerializedObject.Update();
            using (var listSP = m_SerializedObject.FindProperty(propertyName))
            {
                for (var i = 0; i < fields.Length; ++i)
                {
                    var field = fields[i];
                    if (field is VisualElement visualElement)
                        field.OnValueChanged(evt => graphView.OnValueChanged(visualElement));
                    if (field is IBindable bindable)
                    {
                        var sp = listSP.GetArrayElementAtIndex(i);
                        bindable.BindProperty(sp);
                    }
                }
            }
        }

        void Awake()
        {
            hideFlags = HideFlags.DontSave;
        }

        void OnEnable()
        {
            m_SerializedObject = new SerializedObject(this);
            m_GuidProperty = m_SerializedObject.FindProperty(nameof(m_Guid));
            m_TypeNameProperty = m_SerializedObject.FindProperty(nameof(m_TypeName));
            m_PositionProperty = m_SerializedObject.FindProperty(nameof(m_Position));
            m_ReferenceGuidsProperty = m_SerializedObject.FindProperty(nameof(m_ReferenceGuids));
            ReferenceGuids = new SerializedPropertyList<string>(m_ReferenceGuidsProperty, sp => sp.stringValue, (sp, val) => sp.stringValue = val);
        }

        void OnDestroy()
        {
            m_GuidProperty?.Dispose();
            m_TypeNameProperty?.Dispose();
            m_PositionProperty?.Dispose();
            m_ReferenceGuidsProperty?.Dispose();
            m_SerializedObject?.Dispose();
            m_GuidProperty = null;
            m_TypeNameProperty = null;
            m_PositionProperty = null;
            m_ReferenceGuidsProperty = null;
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
        List<float> m_FloatValues = new List<float>();
        [SerializeField]
        List<int> m_IntValues = new List<int>();
        [SerializeField]
        List<Color> m_ColorValues = new List<Color>();
        [SerializeField]
        List<Vector4> m_Vector4Values = new List<Vector4>();
        [SerializeField]
        List<AnimationCurve> m_AnimationCurveValues = new List<AnimationCurve>();
        [SerializeField]
        List<UnityObject> m_UnityObjectValues = new List<UnityObject>();

        public string Guid { get => m_Guid; set => m_Guid = value; }
        public string TypeName { get => m_TypeName; set => m_TypeName = value; }
        public Rect Position { get => m_Position; set => m_Position = value; }
        public IList<string> ReferenceGuids => m_ReferenceGuids;

        readonly ListDictionary m_ListDictionary = new ListDictionary();

        sealed class ListDictionary : IEnumerable
        {
            readonly Dictionary<Type, IEnumerable> m_Lists = new Dictionary<Type, IEnumerable>();

            public void Add<T>(List<T> list)
            {
                m_Lists.Add(typeof(T), list);
            }

            public bool TryGetValue<T>(out List<T> list)
            {
                if (m_Lists.TryGetValue(typeof(T), out var enumerable))
                {
                    list = enumerable as List<T>;
                    return true;
                }
                else
                {
                    list = null;
                    return false;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => m_Lists.GetEnumerator();
        }

        public SerializedGraphElement()
        {
            m_ListDictionary = new ListDictionary { m_FloatValues, m_IntValues, m_ColorValues, m_Vector4Values, m_AnimationCurveValues, m_UnityObjectValues };
        }

        public void SetFieldValues<T>(IGraphViewCallback graphView, params INotifyValueChanged<T>[] fields)
        {
            if (!m_ListDictionary.TryGetValue<T>(out var fieldValues))
                throw new ArgumentOutOfRangeException(nameof(T));
            fieldValues.Clear();
            fieldValues.AddRange(fields.Select(field => field.value));
        }

        public void GetFieldValues<T>(IGraphViewCallback graphView, params INotifyValueChanged<T>[] fields)
        {
            if (!m_ListDictionary.TryGetValue<T>(out var fieldValues))
                throw new ArgumentOutOfRangeException(nameof(T));
            for (var i = 0; i < fieldValues.Count; ++i)
            {
                fields[i].value = fieldValues[i];
            }
        }
    }
}
