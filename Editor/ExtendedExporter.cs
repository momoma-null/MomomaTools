using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    static class ExtendedExporter
    {
        [MenuItem("Assets/Export Assets (Extended)")]
        static void Export()
        {
            var exportPath = EditorUtility.SaveFilePanel("Export", EditorApplication.applicationPath, string.Empty, "unitypackage");
            if (string.IsNullOrEmpty(exportPath))
                return;
            AssetDatabase.ExportPackage(
                Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).ToArray(),
                exportPath,
                ExportPackageOptions.Interactive |
                ExportPackageOptions.Recurse);
        }
    }
}
