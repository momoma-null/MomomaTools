using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    sealed class AssetsCleaner : EditorWindow
    {
        [SerializeField]
        Object _rootFloder;

        [MenuItem("MomomaTools/Assets Cleaner")]
        static void ShowWindow()
        {
            GetWindow<AssetsCleaner>(ObjectNames.NicifyVariableName(nameof(AssetsCleaner)));
        }

        void OnGUI()
        {
            var rootFolder = EditorGUILayout.ObjectField("Root", _rootFloder, typeof(Object), false);
            if (rootFolder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(rootFolder)))
            {
                _rootFloder = rootFolder;
            }

            using (new EditorGUI.DisabledScope(_rootFloder == null))
            {
                if (GUILayout.Button("Clean Assets"))
                {
                    try
                    {
                        AssetDatabase.StartAssetEditing();
                        var rootPath = AssetDatabase.GetAssetPath(_rootFloder);
                        AssetDatabase.ForceReserializeAssets(GetAssetPaths(rootPath, "Object"));

                        ModelImporterCleaner.Remove(GetAssetPaths(rootPath, "Model"));
                        PrefabCleaner.Remove(GetAssetPaths(rootPath, "Prefab"));
                        UnusedMaterialPropertiesRemover.Remove(GetAssetPaths(rootPath, "Material"));
                        UnusedAnimatorAssetsRemover.Remove(GetAssetPaths(rootPath, "AnimatorController"));
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        static IEnumerable<string> GetAssetPaths(string rootPath, string typeFilter)
        {
            var guids = AssetDatabase.FindAssets($"t:{typeFilter}", new[] { $"{rootPath}/" });
            return guids.Select(AssetDatabase.GUIDToAssetPath);
        }
    }
}
