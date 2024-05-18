using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MomomaAssets
{
    [ScriptedImporter(1, "texture2darray")]
    sealed class Texture2DArrayImporter : ScriptedImporter
    {
        [SerializeField]
        List<Texture2D> m_Texture2Ds = new List<Texture2D>();
        [SerializeField]
        FilterMode m_FilterMode = 0;
        [SerializeField]
        TextureWrapMode m_WrapModeU = 0;
        [SerializeField]
        TextureWrapMode m_WrapModeV = 0;
        [SerializeField]
        TextureWrapMode m_WrapModeW = 0;
        [SerializeField]
        int m_AnisoLevel = 1;
        [SerializeField]
        int m_ColorSpace = 1;

        [MenuItem("Assets/Create/Texture 2D Array", false, 310)]
        static void CreateTexture2DArray()
        {
            ProjectWindowUtil.CreateAssetWithContent("NewTexture2DArray.texture2darray", "MomomaAssets.Texture2DArrayImporter");
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var srcTexture2Ds = new List<Texture2D>(m_Texture2Ds);
            srcTexture2Ds.RemoveAll(t => t == null);
            if (srcTexture2Ds.Count > 0 && srcTexture2Ds[0] != null)
            {
                var baseTex = srcTexture2Ds[0];
                srcTexture2Ds.RemoveAll(tex => tex.width != baseTex.width || tex.height != baseTex.height || tex.mipmapCount != baseTex.mipmapCount || tex.format != baseTex.format);
                var texture2DArray = new Texture2DArray(baseTex.width, baseTex.height, srcTexture2Ds.Count, baseTex.format, baseTex.mipmapCount > 1, m_ColorSpace == 0);
                for (var index = 0; index < srcTexture2Ds.Count; ++index)
                {
                    Graphics.CopyTexture(srcTexture2Ds[index], 0, texture2DArray, index);
                    var srcPath = AssetDatabase.GetAssetPath(srcTexture2Ds[index]);
                    if (!string.IsNullOrEmpty(srcPath))
                        ctx.DependsOnSourceAsset(srcPath);
                }
                texture2DArray.filterMode = m_FilterMode;
                texture2DArray.wrapModeU = m_WrapModeU;
                texture2DArray.wrapModeV = m_WrapModeV;
                texture2DArray.wrapModeW = m_WrapModeW;
                texture2DArray.anisoLevel = m_AnisoLevel;
                texture2DArray.Apply(false, true);

                var baseTexEditor = Editor.CreateEditor(baseTex);
                var thumbnail = baseTexEditor.RenderStaticPreview(AssetDatabase.GetAssetPath(baseTex), null, 64, 64);
                DestroyImmediate(baseTexEditor);
                ctx.AddObjectToAsset("Texture2DArray", texture2DArray, thumbnail);
                ctx.SetMainObject(texture2DArray);
            }
            else
            {
                var texture2DArray = new Texture2DArray(32, 32, 1, TextureFormat.ARGB32, false);
                texture2DArray.Apply(false, true);
                ctx.AddObjectToAsset("Texture2DArray", texture2DArray);
                ctx.SetMainObject(texture2DArray);
            }
        }

        public Texture2D GetTexture(int index) => m_Texture2Ds[index];
    }
}// namespace MomomaAssets
