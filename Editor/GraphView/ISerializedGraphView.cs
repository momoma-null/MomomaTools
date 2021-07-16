using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace MomomaAssets
{
    public interface ISerializedGraphView
    {
        IList<ISerializedGraphElement> SerializedGraphElements { get; }
    }

    [Serializable]
    public sealed class SerializedGraphView : ISerializedGraphView, ISerializationCallbackReceiver
    {
        [SerializeField]
        List<SerializedGraphElement> m_SerializedGraphElements = new List<SerializedGraphElement>();

        public IList<ISerializedGraphElement> SerializedGraphElements { get; private set; } = new List<ISerializedGraphElement>();

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            SerializedGraphElements = m_SerializedGraphElements.ConvertAll(element => element as ISerializedGraphElement);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_SerializedGraphElements = SerializedGraphElements.Cast<SerializedGraphElement>().ToList();
        }
    }
}
