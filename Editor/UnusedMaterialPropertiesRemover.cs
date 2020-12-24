using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    public class UnusedMaterialPropertiesRemover : Editor
    {
        static readonly Dictionary<Shader, HashSet<string>> s_ShaderVariants = new Dictionary<Shader, HashSet<string>>();

        [MenuItem("MomomaTools/RemoveUnusedProperties")]
        static void Remove()
        {
            var mats = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in mats)
            {
                using (var so = new SerializedObject(mat))
                {
                    so.Update();
                    using (var savedProp = so.FindProperty("m_SavedProperties"))
                    {
                        RemoveProperties(savedProp.FindPropertyRelative("m_TexEnvs"), mat);
                        RemoveProperties(savedProp.FindPropertyRelative("m_Floats"), mat);
                        RemoveProperties(savedProp.FindPropertyRelative("m_Colors"), mat);
                    }
                    if (mat.shader != null)
                    {
                        HashSet<string> variants;
                        if (!s_ShaderVariants.TryGetValue(mat.shader, out variants))
                        {
                            variants = new HashSet<string>();
                            s_ShaderVariants[mat.shader] = variants;
                            using (var shaderSo = new SerializedObject(mat.shader))
                            {
                                shaderSo.Update();
                                var m_Snippets = shaderSo.FindProperty("m_CompileInfo.m_Snippets");
                                for (var i = 0; i < m_Snippets.arraySize; ++i)
                                {
                                    var snippet = m_Snippets.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                                    using (var m_VariantsUser0 = snippet.FindPropertyRelative("m_VariantsUser0"))
                                    {
                                        for (var j = 0; j < m_VariantsUser0.arraySize; ++j)
                                        {
                                            var tempArray = m_VariantsUser0.GetArrayElementAtIndex(j);
                                            for (var k = 0; k < tempArray.arraySize; ++k)
                                                variants.Add(tempArray.GetArrayElementAtIndex(k).stringValue);
                                        }
                                    }
                                    using (var m_VariantsUser1 = snippet.FindPropertyRelative("m_VariantsUser1"))
                                    {
                                        for (var j = 0; j < m_VariantsUser1.arraySize; ++j)
                                        {
                                            var tempArray = m_VariantsUser1.GetArrayElementAtIndex(j);
                                            for (var k = 0; k < tempArray.arraySize; ++k)
                                                variants.Add(tempArray.GetArrayElementAtIndex(k).stringValue);
                                        }
                                    }
                                    using (var m_VariantsUser2 = snippet.FindPropertyRelative("m_VariantsUser2"))
                                    {
                                        for (var j = 0; j < m_VariantsUser2.arraySize; ++j)
                                        {
                                            var tempArray = m_VariantsUser2.GetArrayElementAtIndex(j);
                                            for (var k = 0; k < tempArray.arraySize; ++k)
                                                variants.Add(tempArray.GetArrayElementAtIndex(k).stringValue);
                                        }
                                    }
                                    using (var m_VariantsUser3 = snippet.FindPropertyRelative("m_VariantsUser3"))
                                    {
                                        for (var j = 0; j < m_VariantsUser3.arraySize; ++j)
                                        {
                                            var tempArray = m_VariantsUser3.GetArrayElementAtIndex(j);
                                            for (var k = 0; k < tempArray.arraySize; ++k)
                                                variants.Add(tempArray.GetArrayElementAtIndex(k).stringValue);
                                        }
                                    }
                                    using (var m_VariantsUser4 = snippet.FindPropertyRelative("m_VariantsUser4"))
                                    {
                                        for (var j = 0; j < m_VariantsUser4.arraySize; ++j)
                                        {
                                            var tempArray = m_VariantsUser4.GetArrayElementAtIndex(j);
                                            for (var k = 0; k < tempArray.arraySize; ++k)
                                                variants.Add(tempArray.GetArrayElementAtIndex(k).stringValue);
                                        }
                                    }
                                    using (var m_VariantsUser5 = snippet.FindPropertyRelative("m_VariantsUser5"))
                                    {
                                        for (var j = 0; j < m_VariantsUser5.arraySize; ++j)
                                        {
                                            var tempArray = m_VariantsUser5.GetArrayElementAtIndex(j);
                                            for (var k = 0; k < tempArray.arraySize; ++k)
                                                variants.Add(tempArray.GetArrayElementAtIndex(k).stringValue);
                                        }
                                    }
                                }
                            }
                        }
                        using (var m_ShaderKeywords = so.FindProperty("m_ShaderKeywords"))
                        {
                            var keywords = m_ShaderKeywords.stringValue.Split(' ');
                            keywords = Array.FindAll(keywords, k => variants.Contains(k));
                            m_ShaderKeywords.stringValue = string.Join(" ", keywords);
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

}// namespace MomomaAssets
