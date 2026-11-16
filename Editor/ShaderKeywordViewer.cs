using System.Text;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    static class ShaderKeywordViewer
    {
        [MenuItem("Assets/Check Shader Keywords", validate = true)]
        static bool CheckShaderKeywordsValidate()
        {
            return Selection.activeObject is Shader or Material;
        }

        [MenuItem("Assets/Check Shader Keywords")]
        static void CheckShaderKeywords()
        {
            if (Selection.activeObject is Shader shader)
            {
                CheckShader(shader);
            }
            else if (Selection.activeObject is Material material)
            {
                CheckMaterial(material);
            }
        }

        static void CheckShader(Shader shader)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Name|Type|Overridable|Dynamic");
            foreach (var i in shader.keywordSpace.keywords)
            {
                builder.AppendLine($"{i.name}|{i.type}({(int)i.type})|{i.isOverridable}|{i.isDynamic}");
            }
            Debug.Log(builder.ToString());
        }

        static void CheckMaterial(Material material)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Name|Type|Overridable|Dynamic");
            foreach (var i in material.enabledKeywords)
            {
                builder.AppendLine($"{i.name}|{i.type}({(int)i.type})|{i.isOverridable}|{i.isDynamic}");
            }
            Debug.Log(builder.ToString());
        }
    }
}
