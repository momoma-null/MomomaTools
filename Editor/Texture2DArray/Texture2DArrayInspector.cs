using System;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using MomomaAssets.Utility;

namespace MomomaAssets
{

    [CustomEditor(typeof(Texture2DArray))]
    public class Texture2DArrayInspector : Editor
    {
        readonly static Type s_Texture2DArrayInspectorType = Type.GetType("UnityEditor.Texture2DArrayInspector, UnityEditor.dll");
        readonly static Type s_TextureInspectorType = Type.GetType("UnityEditor.TextureInspector, UnityEditor.dll");

        Texture2DArray texture2DArray;
        int m_Width;
        int m_Height;
        int m_MipCount;
        TextureFormat m_Format;
        SerializedProperty m_ColorSpace;

        List<Texture2D> m_Texture2DList;
        ReorderableList m_ReorderableList;

        Editor m_TextureEditor;
        Editor m_PreviewTextureEditor;

        void OnEnable()
        {
            texture2DArray = target as Texture2DArray;
            m_Width = texture2DArray.width;
            m_Height = texture2DArray.height;
            m_Format = texture2DArray.format;
            m_MipCount = serializedObject.FindProperty("m_MipCount").intValue;
            m_ColorSpace = serializedObject.FindProperty("m_ColorSpace");
            m_TextureEditor = Editor.CreateEditor(target, s_Texture2DArrayInspectorType);

            T2DAtoList();
            m_ReorderableList = new ReorderableList(m_Texture2DList, typeof(Texture2D));
            m_ReorderableList.onAddCallback += OnAdd;
            m_ReorderableList.onRemoveCallback += OnRemove;
            m_ReorderableList.onCanRemoveCallback += OnCanRemove;
            m_ReorderableList.onChangedCallback += OnChanged;
            m_ReorderableList.drawHeaderCallback += DrawHeader;
            m_ReorderableList.drawElementCallback += DrawElemnt;
            m_ReorderableList.elementHeight = 64;
        }

        void OnDisable()
        {
            if (m_Texture2DList != null)
            {
                foreach (var tex in m_Texture2DList)
                {
                    DestroyImmediate(tex);
                }
            }
            m_Texture2DList = null;
            if (m_TextureEditor != null)
                DestroyImmediate(m_TextureEditor);
            if (m_PreviewTextureEditor != null)
                DestroyImmediate(m_PreviewTextureEditor);
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            m_TextureEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var tex in m_Texture2DList)
                {
                    tex.filterMode = texture2DArray.filterMode;
                    tex.wrapModeU = texture2DArray.wrapModeU;
                    tex.wrapModeV = texture2DArray.wrapModeV;
                    tex.wrapModeW = texture2DArray.wrapModeW;
                    tex.anisoLevel = texture2DArray.anisoLevel;
                }
            }
            EditorGUILayout.Space();
            m_ReorderableList.DoLayoutList();
        }

        void T2DAtoList()
        {
            m_Texture2DList = new List<Texture2D>();

            for (var i = 0; i < texture2DArray.depth; ++i)
            {
                var tex = new Texture2D(m_Width, m_Height, m_Format, m_MipCount > 1, m_ColorSpace.intValue == 0);
                for (var mip = 0; mip < m_MipCount; ++mip)
                {
                    Graphics.CopyTexture(texture2DArray, i, mip, tex, 0, mip);
                }
                m_Texture2DList.Add(tex);
            }
        }

        void ListtoT2DA()
        {
            var newTexture2DArray = new Texture2DArray(m_Width, m_Height, m_Texture2DList.Count, m_Format, m_MipCount > 1, m_ColorSpace.intValue == 0);

            for (var i = 0; i < m_Texture2DList.Count; ++i)
            {
                for (var mip = 0; mip < m_MipCount; ++mip)
                {
                    Graphics.CopyTexture(m_Texture2DList[i], 0, mip, newTexture2DArray, i, mip);
                }
            }

            serializedObject.CopySerializedObject(new SerializedObject(newTexture2DArray), new string[] { "m_Name", "m_IsReadable" }, false);
        }

        void OnAdd(ReorderableList list)
        {
            list.list.Add(new Texture2D(m_Width, m_Height, m_Format, m_MipCount > 1, m_ColorSpace.intValue == 0));
        }

        void OnRemove(ReorderableList list)
        {
            if (m_Texture2DList[list.index] != null)
                DestroyImmediate(m_Texture2DList[list.index]);
            list.list.RemoveAt(list.index);
            if (list.index > list.list.Count - 1)
                list.index = list.list.Count - 1;
        }

        bool OnCanRemove(ReorderableList list)
        {
            return list.count > 1;
        }

        void OnChanged(ReorderableList list)
        {
            ListtoT2DA();
        }

        void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Texture2D Array");
        }

        void DrawElemnt(Rect rect, int index, bool isActive, bool isFocused)
        {
            EditorGUI.BeginChangeCheck();
            var newTex = EditorGUI.ObjectField(rect, "Texture " + index.ToString(), m_Texture2DList[index], typeof(Texture2D), false) as Texture2D;
            if (EditorGUI.EndChangeCheck() && newTex != null)
            {
                if (newTex.width != m_Width || newTex.height != m_Height)
                {
                    EditorUtility.DisplayDialog("Failed to copy", "Texture size is different", "OK");
                    return;
                }
                if (newTex.format != m_Format)
                {
                    EditorUtility.DisplayDialog("Failed to copy", "Texture format is different", "OK");
                    return;
                }
                if (newTex.mipmapCount != m_MipCount)
                {
                    EditorUtility.DisplayDialog("Failed to copy", "Mipmap count is different", "OK");
                    return;
                }

                var tex = new Texture2D(m_Width, m_Height, m_Format, m_MipCount > 1, false);
                Graphics.CopyTexture(newTex, tex);
                m_Texture2DList[index] = tex;
                ListtoT2DA();
            }
        }

        public override bool HasPreviewGUI()
        {
            return target != null;
        }

        public override string GetInfoString()
        {
            return m_TextureEditor.GetInfoString();
        }

        public override void OnPreviewSettings()
        {
            var select = Mathf.Clamp(m_ReorderableList.index, 0, m_Texture2DList.Count - 1);
            if (m_PreviewTextureEditor == null || m_PreviewTextureEditor.target != m_Texture2DList[select])
            {
                DestroyImmediate(m_PreviewTextureEditor);
                m_PreviewTextureEditor = Editor.CreateEditor(m_Texture2DList[select], s_TextureInspectorType);
            }
            m_PreviewTextureEditor.OnPreviewSettings();
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            m_PreviewTextureEditor?.OnPreviewGUI(rect, background);
        }
    }

}// namespace MomomaAssets
