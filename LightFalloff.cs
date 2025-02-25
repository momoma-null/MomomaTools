
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

namespace MomomaAssets
{
    [ExecuteInEditMode]
    sealed class LightFalloff : MonoBehaviour, IPreprocessBehaviour
    {
        [SerializeField]
        FalloffType falloffType;

        void IPreprocessBehaviour.Process() { }

        static void UpdateFalloffType(FalloffType falloffType)
        {
            Lightmapping.ResetDelegate();
            if (falloffType != FalloffType.Undefined)
            {
                var defaultDelegate = Lightmapping.GetDelegate();
                Lightmapping.SetDelegate((requests, output) =>
                {
                    defaultDelegate.Invoke(requests, output);
                    for (var i = 0; i < output.Length; ++i)
                    {
                        var lightData = output[i];
                        lightData.falloff = falloffType;
                        output[i] = lightData;
                    }
                });
            }
            foreach (var i in FindObjectsOfType<Light>())
            {
                i.SetLightDirty();
            }
        }

        void OnEnable()
        {
            UpdateFalloffType(falloffType);
        }

        void OnValidate()
        {
            UpdateFalloffType(falloffType);
        }

        void OnDisable()
        {
            UpdateFalloffType(FalloffType.Undefined);
        }
    }
}
