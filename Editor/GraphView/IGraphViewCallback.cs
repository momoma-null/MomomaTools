using System.Collections.Generic;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    public interface IGraphViewCallback
    {
        void Initialize();
        void OnValueChanged(GraphElement graphElement);
    }
}
