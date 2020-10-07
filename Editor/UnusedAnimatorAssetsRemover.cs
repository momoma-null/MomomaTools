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
        AssetDatabase.StartAssetEditing();
        foreach (var ctrl in ctrls)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl));
            var objHash = new HashSet<UnityEngine.Object>(subAssets);
            foreach (var subAsset in subAssets)
            {
                var so = new SerializedObject(subAsset);
                so.Update();
                var sp = so.GetIterator();
                while (sp.Next(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        objHash.Remove(sp.objectReferenceValue);
                    }
                }
            }
            foreach (var obj in objHash)
            {
                AssetDatabase.RemoveObjectFromAsset(obj);
                Debug.Log("Remove : " + obj.ToString());
            }
        }
        AssetDatabase.StopAssetEditing();
    }
}

}// namespace MomomaAssets
