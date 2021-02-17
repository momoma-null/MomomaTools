using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    [CustomEditor(typeof(Texture2DArray))]
    public class Texture2DArrayInspector : Editor
    {
        readonly static Type s_Texture2DArrayInspectorType = Type.GetType("UnityEditor.Texture2DArrayInspector, UnityEditor.dll");
        readonly static Type s_TextureInspectorType = Type.GetType("UnityEditor.TextureInspector, UnityEditor.dll");
        readonly static FieldInfo s_SliceInfo = s_Texture2DArrayInspectorType.GetField("m_Slice", BindingFlags.NonPublic | BindingFlags.Instance);

        Texture2DArray texture2DArray;
        Editor m_Texture2DArrayEditor;
        Editor m_PreviewTextureEditor;
        Texture2D m_PreviewTexture;
        int m_Slice = -1;
        Texture2DArrayImporter m_Importer;
        bool m_MipChain;
        bool m_Linear;

        void OnEnable()
        {
            texture2DArray = target as Texture2DArray;
            m_Texture2DArrayEditor = Editor.CreateEditor(target, s_Texture2DArrayInspectorType);
            var path = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(path))
                m_Importer = AssetImporter.GetAtPath(path) as Texture2DArrayImporter;
            m_MipChain = serializedObject.FindProperty("m_MipCount").intValue > 1;
            m_Linear = serializedObject.FindProperty("m_ColorSpace").intValue == 0;
        }

        void OnDisable()
        {
            if (m_Texture2DArrayEditor != null)
                DestroyImmediate(m_Texture2DArrayEditor);
            if (m_PreviewTextureEditor != null)
                DestroyImmediate(m_PreviewTextureEditor);
            if (m_PreviewTexture != null)
                DestroyImmediate(m_PreviewTexture);
        }

        public override bool HasPreviewGUI()
        {
            return target != null;
        }

        public override string GetInfoString()
        {
            return m_Texture2DArrayEditor.GetInfoString();
        }

        public override void OnPreviewSettings()
        {
            var sliceInternal = (int)s_SliceInfo.GetValue(m_Texture2DArrayEditor);
            if (m_PreviewTextureEditor == null || sliceInternal != m_Slice)
            {
                if (m_PreviewTextureEditor != null)
                    DestroyImmediate(m_PreviewTextureEditor);
                m_Slice = sliceInternal;
                var slice = Mathf.Clamp(sliceInternal, 0, texture2DArray.depth - 1);
                m_PreviewTexture = new Texture2D(texture2DArray.width, texture2DArray.height, texture2DArray.format, m_MipChain, m_Linear);
                Graphics.CopyTexture(texture2DArray, slice, m_PreviewTexture, 0);
                if (m_Importer != null)
                {
                    using (var texSO = new SerializedObject(m_PreviewTexture))
                    using (var importerSO = new SerializedObject(m_Importer))
                    {
                        texSO.FindProperty("m_LightmapFormat").intValue = importerSO.FindProperty("m_LightmapFormat").intValue;
                        texSO.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                m_PreviewTextureEditor = Editor.CreateEditor(m_PreviewTexture, s_TextureInspectorType);
            }
            m_PreviewTextureEditor.OnPreviewSettings();
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            m_Texture2DArrayEditor.OnPreviewGUI(rect, background);
            rect.yMin += 24f;
            m_PreviewTextureEditor?.OnPreviewGUI(rect, background);
        }
    }

}// namespace MomomaAssets
