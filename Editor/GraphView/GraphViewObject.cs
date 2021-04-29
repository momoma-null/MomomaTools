using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace MomomaAssets
{
    public sealed class GraphViewObject : ScriptableObject, ISerializedGraphView, ISerializationCallbackReceiver
    {
        [SerializeField]
        string m_GraphViewTypeName = "";
        [SerializeField]
        GraphElementObject[] m_SerializedGraphElements = new GraphElementObject[0];

        public Type GraphViewType { get; private set; }
        public IList<ISerializedGraphElement> SerializedGraphElements { get; private set; } = new List<ISerializedGraphElement>();
        public IReadOnlyDictionary<string, int> GuidToIndices { get; private set; } = new Dictionary<string, int>();

        void Awake()
        {
            hideFlags = HideFlags.DontSave;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            GraphViewType = Type.GetType(m_GraphViewTypeName);
            SerializedGraphElements = m_SerializedGraphElements.Cast<ISerializedGraphElement>().ToList();
            var dict = new Dictionary<string, int>();
            var index = 0;
            foreach (var element in m_SerializedGraphElements)
            {
                dict[element.Guid] = index;
                ++index;
            }
            GuidToIndices = dict;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
    }
}
