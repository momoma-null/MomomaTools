
using UnityEngine;
using UnityEngine.EventSystems;

namespace MomomaAssets
{
    sealed class CanvasColliderHelper : MonoBehaviour, IPreprocessBehaviour
    {
        [SerializeField]
        Canvas rootCanvas;

        void IPreprocessBehaviour.Process()
        {
            var rootGO = rootCanvas.gameObject;
            if (!rootCanvas.TryGetComponent<BoxCollider>(out var collider))
            {
                collider = rootGO.AddComponent<BoxCollider>();
            }
            collider.enabled = false;

            var eventSystemHandlers = rootCanvas.GetComponentsInChildren<IEventSystemHandler>(true);
            var rootTransform = rootCanvas.GetComponent<RectTransform>();
            foreach (var eventSystemHandler in eventSystemHandlers)
            {
                if ((eventSystemHandler as Component).transform is not RectTransform rectTransform)
                    continue;
                var rect = rectTransform.rect;
                var center = rectTransform.TransformPoint(rect.center);
                center = rootTransform.InverseTransformPoint(center);
                var newCollider = rootGO.AddComponent<BoxCollider>();
                newCollider.size = rect.size;
                newCollider.center = center;
                newCollider.isTrigger = true;
            }
        }
    }
}
