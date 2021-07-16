using System;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    public interface IEdgeCallback
    {
        event Action<Edge> onPortChanged;
    }
}
