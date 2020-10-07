#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Reflection;
using MomomaAssets.Utility;

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
		new SerializedObject(rs).CopySerializedObject(new SerializedObject(renderSettings), new string[1] {"m_Sun"});
		new SerializedObject(ls).CopySerializedObject(new SerializedObject(lightmapSettings), new string[1] {"m_LightingDataAsset"});
	}

	public static void ResetStoredData()
	{
		renderSettings = null;
		lightmapSettings = null;
	}
	
	public static RenderSettings GetRenderSettings()
	{
		var flags =  BindingFlags.NonPublic | BindingFlags.Static;
		var getRenderSettings = typeof(RenderSettings).GetMethod("GetRenderSettings", flags);
		return getRenderSettings.Invoke(null, null) as RenderSettings;
	}

	public static LightmapSettings GetLightmapSettings()
	{
		var flags =  BindingFlags.NonPublic | BindingFlags.Static;
		var getLightmapSettings = typeof(LightmapEditorSettings).GetMethod("GetLightmapSettings", flags);
		return getLightmapSettings.Invoke(null, null) as LightmapSettings;
	}
}

}// namespace MomomaAssets
#endif
