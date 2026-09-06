using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    static class PrefabCleaner
    {
        public static void Remove(IEnumerable<string> paths)
        {
            var prefabHash = new HashSet<GameObject>();
            foreach (var path in paths)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var go in assets.OfType<GameObject>())
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
