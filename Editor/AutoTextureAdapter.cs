using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    class AutoTextureAdapter
    {
        enum PBRTextureType
        {
            Diffuse,
            Normal,
            Metalic,
            Specular,
            Roughness,
            Height,
            Occlusion,
            Undefined
        }

        [MenuItem("Assets/Create/Auto Material", true, 301)]
        static bool CreateAutoMaterialValidate()
        {
            return Selection.activeObject is Texture2D;
        }

        [MenuItem("Assets/Create/Auto Material", false, 301)]
        static void CreateAutoMaterial()
        {
            var targetTex = Selection.activeObject as Texture2D;
            if (targetTex == null)
                return;
            string baseName;
            if (DeterminePBRTextureType(targetTex.name, out baseName) == PBRTextureType.Undefined)
            {
                Debug.LogWarning("Can not determine texture type.", targetTex);
                return;
            }
            var basePath = Path.GetDirectoryName(Application.dataPath) + '/';
            var targetDirectoryPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(targetTex));
            var path = basePath + targetDirectoryPath;
            var texPaths = Directory.GetFiles(path).Where(s => !s.EndsWith(".meta")).Select(s => s.Replace(basePath, "")).ToArray();
            var texDict = new Dictionary<PBRTextureType, Texture2D>();
            foreach (var texPath in texPaths)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null)
                    continue;
                string baseName2;
                var texType = DeterminePBRTextureType(tex.name, out baseName2);
                if (baseName != baseName2)
                    continue;
                texDict[texType] = tex;
            }
            var shaderName = "Standard";
            if (texDict.ContainsKey(PBRTextureType.Roughness))
                shaderName = "Autodesk Interactive";
            else if (texDict.ContainsKey(PBRTextureType.Specular))
                shaderName = "Standard (Specular setup)";
            var shader = Shader.Find(shaderName);
            var material = new Material(shader);
            foreach (var pair in texDict)
            {
                switch (pair.Key)
                {
                    case PBRTextureType.Diffuse:
                        material.SetTexture("_MainTex", pair.Value);
                        break;
                    case PBRTextureType.Normal:
                        material.SetTexture("_BumpMap", pair.Value);
                        material.EnableKeyword("_NORMALMAP");
                        break;
                    case PBRTextureType.Metalic:
                        material.SetTexture("_MetallicGlossMap", pair.Value);
                        material.EnableKeyword("_METALLICGLOSSMAP");
                        break;
                    case PBRTextureType.Specular:
                    case PBRTextureType.Roughness:
                        material.SetTexture("_SpecGlossMap", pair.Value);
                        material.EnableKeyword("_SPECGLOSSMAP");
                        break;
                    case PBRTextureType.Height:
                        material.SetTexture("_ParallaxMap", pair.Value);
                        material.EnableKeyword("_PARALLAXMAP");
                        break;
                    case PBRTextureType.Occlusion:
                        material.SetTexture("_OcclusionMap", pair.Value);
                        break;
                }
            }
            var matPath = targetDirectoryPath + '/' + baseName.Trim('_', '-') + ".mat";
            matPath = AssetDatabase.GenerateUniqueAssetPath(matPath);
            AssetDatabase.CreateAsset(material, matPath);
            AssetDatabase.ImportAsset(matPath);
        }

        static PBRTextureType DeterminePBRTextureType(string name, out string baseName)
        {
            int index;
            if (-1 < (index = name.IndexOf("diff", StringComparison.OrdinalIgnoreCase))
             || -1 < (index = name.IndexOf("col", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Diffuse;
            }
            else if (-1 < (index = name.IndexOf("Nor", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Normal;
            }
            else if (-1 < (index = name.IndexOf("Meta", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Metalic;
            }
            else if (-1 < (index = name.IndexOf("Spec", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Specular;
            }
            else if (-1 < (index = name.IndexOf("Rough", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Roughness;
            }
            else if (-1 < (index = name.IndexOf("Heigh", StringComparison.OrdinalIgnoreCase))
                  || -1 < (index = name.IndexOf("Disp", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Height;
            }
            else if (-1 < (index = name.IndexOf("AO", StringComparison.OrdinalIgnoreCase))
                  || -1 < (index = name.IndexOf("Ambient", StringComparison.OrdinalIgnoreCase))
                  || -1 < (index = name.IndexOf("Occ", StringComparison.OrdinalIgnoreCase)))
            {
                baseName = name.Remove(index);
                return PBRTextureType.Occlusion;
            }
            else
            {
                baseName = null;
                return PBRTextureType.Undefined;
            }
        }
    }

}// namespace MomomaAssets
