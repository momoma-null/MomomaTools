using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    static class UnusedAnimatorAssetsRemover
    {
        public static void Remove(IEnumerable<string> paths)
        {
            while (true)
            {
                var isRemoved = false;
                foreach (var path in paths)
                {
                    if (!AssetDatabase.IsNativeAsset(AssetDatabase.LoadMainAssetAtPath(path)))
                        continue;
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    var objHash = new HashSet<Object>(subAssets);
                    foreach (var subAsset in subAssets)
                    {
                        if (subAsset == null)
                            continue;
                        using (var so = new SerializedObject(subAsset))
                        using (var sp = so.GetIterator())
                        {
                            while (sp.Next(true))
                            {
                                if (sp.propertyType == SerializedPropertyType.ObjectReference && sp.objectReferenceValue != null)
                                    objHash.Remove(sp.objectReferenceValue);
                            }
                        }
                    }
                    foreach (var obj in objHash)
                    {
                        if (obj == null)
                            continue;
                        AssetDatabase.RemoveObjectFromAsset(obj);
                        isRemoved = true;
                        Debug.Log($"Remove : {obj}");
                    }
                }
                if (!isRemoved)
                    break;
            }
        }
    }
}// namespace
