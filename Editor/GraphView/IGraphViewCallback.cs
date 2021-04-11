using UnityEngine.Experimental.UIElements;

namespace MomomaAssets
{
    public interface IGraphViewCallback
    {
        void Initialize();
        void OnValueChanged(VisualElement visualElement);
    }
}
