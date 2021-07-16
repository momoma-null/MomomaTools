using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

namespace MomomaAssets.Extensions
{
    public class SerializedPropertyList<T> : IList<T>, IReadOnlyList<T>
    {
        readonly SerializedProperty m_ArrayProperty;
        readonly Func<SerializedProperty, T> GetValue;
        readonly Action<SerializedProperty, T> SetValue;

        public SerializedPropertyList(SerializedProperty arrayProperty, Func<SerializedProperty, T> getValue, Action<SerializedProperty, T> setValue)
        {
            if (!arrayProperty.isArray)
                throw new InvalidOperationException($"{nameof(arrayProperty)} should be Array.");
            if (arrayProperty == null)
                throw new ArgumentNullException(nameof(arrayProperty));
            if (getValue == null)
                throw new ArgumentNullException(nameof(getValue));
            if (setValue == null)
                throw new ArgumentNullException(nameof(setValue));
            m_ArrayProperty = arrayProperty;
            GetValue = getValue;
            SetValue = setValue;
        }

        public int Count => m_ArrayProperty.arraySize;
        bool ICollection<T>.IsReadOnly => false;

        public T this[int index]
        {
            get
            {
                m_ArrayProperty.serializedObject.Update();
                return GetValue(m_ArrayProperty.GetArrayElementAtIndex(index));
            }
            set
            {
                m_ArrayProperty.serializedObject.Update();
                SetValue(m_ArrayProperty.GetArrayElementAtIndex(index), value);
                m_ArrayProperty.serializedObject.ApplyModifiedProperties();
            }
        }

        public int IndexOf(T item)
        {
            m_ArrayProperty.serializedObject.Update();
            for (var i = 0; i < m_ArrayProperty.arraySize; ++i)
                using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(i))
                    if (GetValue(elementSP).Equals(item))
                        return i;
            throw new ArgumentOutOfRangeException(nameof(item));
        }

        public void Insert(int index, T item)
        {
            m_ArrayProperty.serializedObject.Update();
            m_ArrayProperty.InsertArrayElementAtIndex(index);
            using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(index))
            {
                SetValue(elementSP, item);
            }
            m_ArrayProperty.serializedObject.ApplyModifiedProperties();
        }

        public void RemoveAt(int index)
        {
            m_ArrayProperty.serializedObject.Update();
            m_ArrayProperty.DeleteArrayElementAtIndex(index);
            m_ArrayProperty.serializedObject.ApplyModifiedProperties();
        }

        public void Add(T item)
        {
            m_ArrayProperty.serializedObject.Update();
            ++m_ArrayProperty.arraySize;
            using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(m_ArrayProperty.arraySize - 1))
                SetValue(elementSP, item);
            m_ArrayProperty.serializedObject.ApplyModifiedProperties();
        }

        public void Clear()
        {
            m_ArrayProperty.serializedObject.Update();
            m_ArrayProperty.ClearArray();
            m_ArrayProperty.serializedObject.ApplyModifiedProperties();
        }

        public bool Contains(T item)
        {
            m_ArrayProperty.serializedObject.Update();
            for (var i = 0; i < m_ArrayProperty.arraySize; ++i)
                using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(i))
                    if (GetValue(elementSP).Equals(item))
                        return true;
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_ArrayProperty.serializedObject.Update();
            for (var i = 0; i < m_ArrayProperty.arraySize; ++i)
                using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(i + arrayIndex))
                    array[i] = GetValue(elementSP);
            m_ArrayProperty.serializedObject.ApplyModifiedProperties();
        }

        public bool Remove(T item)
        {
            m_ArrayProperty.serializedObject.Update();
            for (var i = 0; i < m_ArrayProperty.arraySize; ++i)
                using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(i))
                    if (GetValue(elementSP).Equals(item))
                    {
                        m_ArrayProperty.DeleteArrayElementAtIndex(i);
                        return true;
                    }
            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            m_ArrayProperty.serializedObject.Update();
            for (var i = 0; i < m_ArrayProperty.arraySize; ++i)
                using (var elementSP = m_ArrayProperty.GetArrayElementAtIndex(i))
                    yield return GetValue(elementSP);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
