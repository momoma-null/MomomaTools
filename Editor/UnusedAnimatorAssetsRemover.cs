using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace MomomaAssets
{

    public class UnusedAnimatorAssetsRemover : Editor
    {
        [MenuItem("MomomaTools/RemoveUnusedAnimatorAssets")]
        static void Remove()
        {
            var ctrls = Resources.FindObjectsOfTypeAll<RuntimeAnimatorController>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var ctrl in ctrls)
                {
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl));
                    var objHash = new HashSet<UnityEngine.Object>(subAssets);
                    foreach (var subAsset in subAssets)
                    {
                        if (subAsset == null)
                            continue;
                        using (var so = new SerializedObject(subAsset))
                        {
                            var sp = so.GetIterator();
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
                        Debug.Log("Remove : " + obj.ToString());
                    }
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
