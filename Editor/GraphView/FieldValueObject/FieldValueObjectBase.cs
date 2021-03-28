using UnityEngine;

namespace MomomaAssets
{
    abstract class FieldValueObjectBase : ScriptableObject, IFieldValue { }

    abstract class FieldValueObjectBase<T> : FieldValueObjectBase, IFieldValue<T>
    {
        public T Value => m_Value;

        [SerializeField]
        T m_Value = default(T);

        void Awake()
        {
            hideFlags = HideFlags.DontSave;
        }
    }

    abstract class FieldValue : IFieldValue { }

    abstract class FieldValue<T> : FieldValue, IFieldValue<T>
    {
        protected FieldValue(T defaultValue)
        {
            m_Value = defaultValue;
        }

        public T Value => m_Value;

        [SerializeField]
        T m_Value;
    }

    public interface IFieldValue { }

    public interface IFieldValue<T> : IFieldValue
    {
        T Value { get; }
    }
}
