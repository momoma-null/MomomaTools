#if !UNITY_2021_3_OR_NEWER
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{

    [CustomEditor(typeof(Texture2DArray))]
    public class Texture2DArrayInspector : Editor
    {
        readonly static Type s_Texture2DArrayInspectorType = Type.GetType("UnityEditor.Texture2DArrayInspector, UnityEditor.dll");
        readonly static FieldInfo s_SliceInfo = s_Texture2DArrayInspectorType.GetField("m_Slice", BindingFlags.NonPublic | BindingFlags.Instance);

        Editor m_Texture2DArrayEditor;
        Editor m_PreviewTextureEditor;

        void OnEnable()
        {
            m_Texture2DArrayEditor = Editor.CreateEditor(target, s_Texture2DArrayInspectorType);
        }

        void OnDisable()
        {
            DestroyImmediate(m_Texture2DArrayEditor);
            m_Texture2DArrayEditor = null;
            DestroyImmediate(m_PreviewTextureEditor);
            m_PreviewTextureEditor = null;
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
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target)) as Texture2DArrayImporter;
            if (importer != null)
            {
                var currentTexture = importer.GetTexture(sliceInternal);
                Editor.CreateCachedEditor(currentTexture, null, ref m_PreviewTextureEditor);
            }
            m_PreviewTextureEditor?.OnPreviewSettings();
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            m_Texture2DArrayEditor.OnPreviewGUI(rect, background);
            rect.yMin += 24f;
            m_PreviewTextureEditor?.OnPreviewGUI(rect, background);
        }
    }

}// namespace MomomaAssets
#endif
