using System;
using System.Collections.Generic;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    public interface IBindableGraphElement
    {
        IEnumerable<IBindable> BindableElements { get; }
        event Action<GraphElement> onValueChanged;
        void SetFieldValues(ISerializedGraphElement serializedGraphElement);
        void GetFieldValues(ISerializedGraphElement serializedGraphElement);
    }
}
