using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    sealed class PrefabCleaner
    {
        [MenuItem("MomomaTools/Cleanup Prefab")]
        static void Remove()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/" });
            var prefabHash = new HashSet<GameObject>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (GameObject go in assets.Where(i => i is GameObject))
                {
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (prefabHash.Add(root))
                    {
                        var oldModifications = PrefabUtility.GetPropertyModifications(root);
                        if (oldModifications != null)
                        {
                            var modifications = new List<PropertyModification>(oldModifications);
                            for (var i = modifications.Count - 1; i >= 0; --i)
                            {
                                if (modifications[i].target == null)
                                {
                                    modifications.RemoveAt(i);
                                }
                            }
                            if (oldModifications.Length != modifications.Count)
                                PrefabUtility.SetPropertyModifications(root, modifications.ToArray());
                        }
                    }
                }
            }
        }
    }
}
