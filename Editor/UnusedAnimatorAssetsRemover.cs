using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

namespace MomomaAssets
{

    public class UnusedAnimatorAssetsRemover : Editor
    {
        [MenuItem("MomomaTools/RemoveUnusedAnimatorAssets")]
        static void Remove()
        {
            var ctrls = Resources.FindObjectsOfTypeAll<AnimatorController>();
            try
            {
                AssetDatabase.StartAssetEditing();
                while (true)
                {
                    var isRemoved = false;
                    foreach (var ctrl in ctrls)
                    {
                        var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl));
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
                            Debug.Log("Remove : " + obj.ToString());
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
            }
        }
    }

}// namespace MomomaAssets
