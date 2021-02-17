using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEditorInternal;

namespace MomomaAssets
{

    [ScriptedImporter(1, "texture2darray")]
    public class Texture2DArrayImporter : ScriptedImporter
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
    }

    [CustomEditor(typeof(Texture2DArrayImporter))]
    public class Texture2DArrayImporterInspector : ScriptedImporterEditor
    {
        readonly static Type s_TextureInspectorType = Type.GetType("UnityEditor.TextureInspector, UnityEditor.dll");

        public override bool showImportedObject => false;

        ReorderableList m_ReorderableList;
        Texture2D m_InitialTexture;
        Editor m_TextureEditor;
        Editor m_Texture2DArrayInspector;
        Texture2D m_TempTexture;
        SerializedProperty m_FilterMode;
        SerializedProperty m_WrapModeU;
        SerializedProperty m_WrapModeV;
        SerializedProperty m_WrapModeW;
        SerializedProperty m_AnisoLevel;
        SerializedProperty m_ColorSpace;

        public override void OnEnable()
        {
            base.OnEnable();
            m_TempTexture = new Texture2D(2, 2);
            m_FilterMode = serializedObject.FindProperty("m_FilterMode");
            m_WrapModeU = serializedObject.FindProperty("m_WrapModeU");
            m_WrapModeV = serializedObject.FindProperty("m_WrapModeV");
            m_WrapModeW = serializedObject.FindProperty("m_WrapModeW");
            m_AnisoLevel = serializedObject.FindProperty("m_AnisoLevel");
            m_ColorSpace = serializedObject.FindProperty("m_ColorSpace");
            SyncTempTexture();
            m_ReorderableList = new ReorderableList(serializedObject, serializedObject.FindProperty("m_Texture2Ds"));
            m_ReorderableList.drawHeaderCallback += r => EditorGUI.LabelField(r, "Textures");
            m_ReorderableList.drawElementCallback += DrawElemnt;
            m_ReorderableList.onReorderCallbackWithDetails += OnChanged;
            m_ReorderableList.elementHeight = 64;
            m_TextureEditor = Editor.CreateEditor(m_TempTexture, s_TextureInspectorType);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (m_TempTexture != null)
                DestroyImmediate(m_TempTexture);
            if (m_TextureEditor != null)
                DestroyImmediate(m_TextureEditor);
        }

        public override void OnInspectorGUI()
        {
            Init();
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                m_TextureEditor.OnInspectorGUI();
                if (check.changed)
                {
                    m_FilterMode.enumValueIndex = (int)m_TempTexture.filterMode;
                    m_WrapModeU.enumValueIndex = (int)m_TempTexture.wrapModeU;
                    m_WrapModeV.enumValueIndex = (int)m_TempTexture.wrapModeV;
                    m_WrapModeW.enumValueIndex = (int)m_TempTexture.wrapModeW;
                    m_AnisoLevel.intValue = m_TempTexture.anisoLevel;
                }
            }
            EditorGUILayout.Separator();
            m_ReorderableList.DoLayoutList();
            ApplyRevertGUI();
        }

        protected override void Apply()
        {
            for (var index = m_ReorderableList.serializedProperty.arraySize - 1; index > -1; --index)
            {
                var element = m_ReorderableList.serializedProperty.GetArrayElementAtIndex(index);
                if (element.objectReferenceValue == null)
                    m_ReorderableList.serializedProperty.DeleteArrayElementAtIndex(index);
            }
            base.Apply();
            SyncTempTexture();
        }

        protected override void ResetValues()
        {
            base.ResetValues();
            SyncTempTexture();
        }

        void Init()
        {
            m_InitialTexture = null;
            for (var index = 0; index < m_ReorderableList.serializedProperty.arraySize; ++index)
            {
                var tex = m_ReorderableList.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue as Texture2D;
                if (tex != null)
                {
                    m_InitialTexture = tex;
                    break;
                }
            }
            if (m_InitialTexture != null)
            {
                using (var texSO = new SerializedObject(m_InitialTexture))
                {
                    m_ColorSpace.intValue = texSO.FindProperty("m_ColorSpace").intValue;
                }
            }
        }

        void SyncTempTexture()
        {
            if (m_TempTexture == null)
                return;
            m_TempTexture.filterMode = (FilterMode)m_FilterMode.enumValueIndex;
            m_TempTexture.wrapModeU = (TextureWrapMode)m_WrapModeU.enumValueIndex;
            m_TempTexture.wrapModeV = (TextureWrapMode)m_WrapModeV.enumValueIndex;
            m_TempTexture.wrapModeW = (TextureWrapMode)m_WrapModeW.enumValueIndex;
            m_TempTexture.anisoLevel = m_AnisoLevel.intValue;
        }

        void DrawElemnt(Rect rect, int index, bool isActive, bool isFocused)
        {
            var label = new GUIContent(string.Format("Element{0}", index));
            var property = m_ReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            var tex = property.objectReferenceValue as Texture2D;
            if (tex == null || m_InitialTexture == null || tex.width != m_InitialTexture.width || tex.height != m_InitialTexture.height || tex.format != m_InitialTexture.format || tex.mipmapCount != m_InitialTexture.mipmapCount)
            {
                var erroricon = EditorGUIUtility.IconContent("console.erroricon");
                label.image = erroricon.image;
            }
            var r = rect;
            r.width -= m_ReorderableList.elementHeight;
            EditorGUI.LabelField(r, label);
            r = rect;
            r.xMin = r.xMax - m_ReorderableList.elementHeight;
            EditorGUI.ObjectField(r, m_ReorderableList.serializedProperty.GetArrayElementAtIndex(index), typeof(Texture2D), GUIContent.none);
        }

        void OnChanged(ReorderableList list, int oldIndex, int newIndex)
        {
            list.serializedProperty.MoveArrayElement(newIndex, oldIndex);
            list.serializedProperty.serializedObject.ApplyModifiedProperties();
            list.serializedProperty.serializedObject.Update();
            list.serializedProperty.MoveArrayElement(oldIndex, newIndex);
        }
    }

}// namespace MomomaAssets
