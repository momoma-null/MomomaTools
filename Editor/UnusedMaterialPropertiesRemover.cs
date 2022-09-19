using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    public class UnusedMaterialPropertiesRemover : Editor
    {
        static readonly Dictionary<Shader, HashSet<string>> s_ShaderVariants = new Dictionary<Shader, HashSet<string>>();

        [MenuItem("MomomaTools/RemoveUnusedProperties")]
        static void Remove()
        {
            var allMaterialPaths = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path.EndsWith(".mat"));
            foreach (var path in allMaterialPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
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
#if UNITY_2019_1_OR_NEWER
                                    CollectKeywords(snippet, "m_VariantsUserGlobal0", variants);
                                    CollectKeywords(snippet, "m_VariantsUserGlobal1", variants);
                                    CollectKeywords(snippet, "m_VariantsUserGlobal2", variants);
                                    CollectKeywords(snippet, "m_VariantsUserGlobal3", variants);
                                    CollectKeywords(snippet, "m_VariantsUserGlobal4", variants);
                                    CollectKeywords(snippet, "m_VariantsUserGlobal5", variants);
                                    CollectKeywords(snippet, "m_VariantsUserGlobal6", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal0", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal1", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal2", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal3", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal4", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal5", variants);
                                    CollectKeywords(snippet, "m_VariantsUserLocal6", variants);
#else
                                    CollectKeywords(snippet, "m_VariantsUser0", variants);
                                    CollectKeywords(snippet, "m_VariantsUser1", variants);
                                    CollectKeywords(snippet, "m_VariantsUser2", variants);
                                    CollectKeywords(snippet, "m_VariantsUser3", variants);
                                    CollectKeywords(snippet, "m_VariantsUser4", variants);
                                    CollectKeywords(snippet, "m_VariantsUser5", variants);
#endif
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

        static void CollectKeywords(SerializedProperty snippet, string propertyName, HashSet<string> variants)
        {
            using (var variantsProperty = snippet.FindPropertyRelative(propertyName))
            {
                for (var i = 0; i < variantsProperty.arraySize; ++i)
                {
                    var tempArray = variantsProperty.GetArrayElementAtIndex(i);
                    for (var j = 0; j < tempArray.arraySize; ++j)
                        variants.Add(tempArray.GetArrayElementAtIndex(j).stringValue);
                }
            }
        }
    }
}// namespace MomomaAssets
