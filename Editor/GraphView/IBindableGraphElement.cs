using System;
using UnityEditor;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    public interface IBindableGraphElement
    {
        void Reset();
        event Action<GraphElement> onValueChanged;
    }

    public interface IBindableGraphElement<T> : IBindableGraphElement
    {
        void Bind(SerializedProperty arrayProperty);
    }
}
