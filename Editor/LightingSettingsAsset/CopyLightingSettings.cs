using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Reflection;
using MomomaAssets.Extensions;

namespace MomomaAssets
{
    public static class CopyLightingSettings
    {
        static RenderSettings renderSettings = null;
        static LightmapSettings lightmapSettings = null;

        [MenuItem("MomomaTools/LightingSettings/Copy")]
        public static void CopySettings()
        {
            CopySettings(UnityEngine.Object.Instantiate(GetRenderSettings()), UnityEngine.Object.Instantiate(GetLightmapSettings()));
        }

        [MenuItem("MomomaTools/LightingSettings/Paste")]
        public static void PasteSettings()
        {
            PasteSettings(GetRenderSettings(), GetLightmapSettings());
            InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem("MomomaTools/LightingSettings/Paste", validate = true)]
        static bool PasteValidate()
        {
            return renderSettings != null && lightmapSettings != null;
        }

        public static void CopySettings(RenderSettings rs, LightmapSettings ls)
        {
            renderSettings = rs;
            lightmapSettings = ls;
        }

        public static void PasteSettings(RenderSettings rs, LightmapSettings ls)
        {
            using(var srcSO = new SerializedObject(renderSettings))
            using(var dstSO = new SerializedObject(rs))
            {
                dstSO.CopySerializedObject(srcSO, new string[1] { "m_Sun" });
            }
            using(var srcSO = new SerializedObject(lightmapSettings))
            using(var dstSO = new SerializedObject(ls))
            {
                dstSO.CopySerializedObject(srcSO, new string[1] { "m_LightingDataAsset" });
            }
        }

        public static void ResetStoredData()
        {
            renderSettings = null;
            lightmapSettings = null;
        }

        public static RenderSettings GetRenderSettings()
        {
            var getRenderSettings = typeof(RenderSettings).GetMethod("GetRenderSettings", BindingFlags.NonPublic | BindingFlags.Static);
            return getRenderSettings.Invoke(null, null) as RenderSettings;
        }

        public static LightmapSettings GetLightmapSettings()
        {
            var getLightmapSettings = typeof(LightmapEditorSettings).GetMethod("GetLightmapSettings", BindingFlags.NonPublic | BindingFlags.Static);
            return getLightmapSettings.Invoke(null, null) as LightmapSettings;
        }
    }

}// namespace
