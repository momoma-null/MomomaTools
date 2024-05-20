
using UnityEngine;
using UnityEngine.UI;

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

            var selectables = rootCanvas.GetComponentsInChildren<Selectable>(true);
            var rootTransform = rootCanvas.GetComponent<RectTransform>();
            foreach(var selectable in selectables)
            {
                var graphic = selectable.targetGraphic;
                if (graphic == null || !graphic.raycastTarget)
                    continue;
                var rect = graphic.rectTransform.rect;
                var center = graphic.rectTransform.TransformPoint(rect.center);
                center = rootTransform.InverseTransformPoint(center);
                var newCollider = rootGO.AddComponent<BoxCollider>();
                newCollider.size = rect.size;
                newCollider.center = center;
                newCollider.isTrigger = true;
            }
        }
    }
}
