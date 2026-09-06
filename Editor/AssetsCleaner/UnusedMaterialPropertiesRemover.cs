using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    static class UnusedMaterialPropertiesRemover
    {
        public static void Remove(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!AssetDatabase.IsNativeAsset(mat))
                    continue;
                using (var so = new SerializedObject(mat))
                {
                    so.Update();
                    using (var savedProp = so.FindProperty("m_SavedProperties"))
                    {
                        RemoveProperties(savedProp.FindPropertyRelative("m_TexEnvs"), mat);
                        RemoveProperties(savedProp.FindPropertyRelative("m_Floats"), mat);
                        RemoveProperties(savedProp.FindPropertyRelative("m_Colors"), mat);
                        RemoveProperties(savedProp.FindPropertyRelative("m_Ints"), mat);
                    }
                    if (mat.shader != null)
                    {
                        using (var keywordsProp = so.FindProperty("m_InvalidKeywords"))
                        {
                            keywordsProp.arraySize = 0;
                        }
                    }
                    if (so.ApplyModifiedProperties())
                    {
                        Debug.Log("modify " + mat.name, mat);
                    }
                }
            }
        }

        static void RemoveProperties(SerializedProperty props, Material mat)
        {
            for (int i = props.arraySize - 1; i >= 0; --i)
            {
                var name = props.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                if (!mat.HasProperty(name))
                    props.DeleteArrayElementAtIndex(i);
            }
        }
    }
}
