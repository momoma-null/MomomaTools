using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MomomaAssets
{

public class LightingSettingsAsset : ScriptableObject
{}

#if UNITY_EDITOR
[CustomEditor(typeof(LightingSettingsAsset))]
public class LightingSettingsAssetInspector : Editor
{
	public override void OnInspectorGUI()
	{
		if (GUILayout.Button("Render Settings"))
		{
			var objs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(target));
			foreach (var obj in objs)
			{
				if (obj is RenderSettings)
					Selection.activeObject = obj;
			}
		}

		if (GUILayout.Button("Lightmap Settings"))
		{
			var objs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(target));
			foreach (var obj in objs)
			{
				if (obj is LightmapSettings)
					Selection.activeObject = obj;
			}
		}

		EditorGUILayout.Space();

		if (GUILayout.Button("Copy Lighting Settings to Active Scene"))
		{
			RenderSettings rs = null;
			LightmapSettings ls = null;
			var objs = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(target));
			foreach (var obj in objs)
			{
				if (obj is RenderSettings)
					rs = (RenderSettings)obj;
				else
				if (obj is LightmapSettings)
					ls = (LightmapSettings)obj;
			}

			if (rs && ls)
			{
				CopyLightingSettings.CopySettings(rs, ls);
				CopyLightingSettings.PasteSettings();
				CopyLightingSettings.ResetStoredData();
			}
		}
	}

	[MenuItem("MomomaTools/LightingSettings/Save as Asset")]
	static void SaveSettings()
	{
		Scene activeScene = SceneManager.GetActiveScene();
		string activeScenePath = activeScene.path;
		var parentDirectory = System.IO.Path.GetDirectoryName(activeScenePath);
		var fileName = System.IO.Path.GetFileNameWithoutExtension(activeScenePath);
		var assetPath = parentDirectory + "/" + fileName + "_LightingSettings.asset";
		assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

		var settingsAsset = ScriptableObject.CreateInstance<LightingSettingsAsset>();
		AssetDatabase.CreateAsset(settingsAsset, assetPath);
		AssetDatabase.AddObjectToAsset(Instantiate(CopyLightingSettings.GetRenderSettings()), assetPath);
		AssetDatabase.AddObjectToAsset(Instantiate(CopyLightingSettings.GetLightmapSettings()), assetPath);
	}
}

[CustomEditor(typeof(RenderSettings))]
public class RenderSettingsInspector : Editor
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
	}
}
#endif

}// namespace MomomaAssets
