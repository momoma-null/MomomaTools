using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    public class Texture2DArrayCreator
    {
        [MenuItem("Assets/Create/Texture 2D Array", false, 310)]
        static void CreateTexture2DArray()
        {
            var srcTex = Selection.activeObject as Texture2D;
            if (srcTex == null || !AssetDatabase.Contains(srcTex))
                return;
            var colorSpace = 0;
            using (var texSO = new SerializedObject(srcTex))
            {
                colorSpace = texSO.FindProperty("m_ColorSpace").intValue;
            }
            var texture2DArray = new Texture2DArray(srcTex.width, srcTex.height, 1, srcTex.format, srcTex.mipmapCount > 1, colorSpace == 0);
            for (var mip = 0; mip < srcTex.mipmapCount; ++mip)
            {
                Graphics.CopyTexture(srcTex, 0, mip, texture2DArray, 0, mip);
            }
            texture2DArray.Apply(false, true);
            var path = AssetDatabase.GetAssetPath(srcTex);
            path = System.IO.Path.ChangeExtension(path, "asset");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(texture2DArray, path);
        }

        [MenuItem("Assets/Create/Texture 2D Array", validate = true)]
        static bool CreateTexture2DArrayValidation()
        {
            var select = Selection.activeObject;
            return (select is Texture2D) && AssetDatabase.Contains(select);
        }
    }

}// namespace MomomaAssets