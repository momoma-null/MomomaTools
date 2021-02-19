using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace MomomaAssets
{
    public class UnusedAnimatorAssetsRemover : Editor
    {
        [MenuItem("MomomaTools/RemoveUnusedAnimatorAssets")]
        static void Remove()
        {
            var allControllerPaths = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path.EndsWith(".controller"));
            try
            {
                AssetDatabase.StartAssetEditing();
                while (true)
                {
                    var isRemoved = false;
                    foreach (var path in allControllerPaths)
                    {
                        var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                        var objHash = new HashSet<UnityEngine.Object>(subAssets);
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
            catch
            {
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }
        }
    }
}// namespace
