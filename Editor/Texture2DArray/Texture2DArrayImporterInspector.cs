using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEditorInternal;
using UnityEngine;

namespace MomomaAssets
{
    [CustomEditor(typeof(Texture2DArrayImporter))]
    sealed class Texture2DArrayImporterInspector : ScriptedImporterEditor
    {
        static class Styles
        {
            public static readonly GUIContent TexturesText = EditorGUIUtility.TrTextContent("Textures");
            public static readonly GUIContent ErrorIcon = EditorGUIUtility.IconContent("console.erroricon");
        }

        public override bool showImportedObject => false;

        ReorderableList m_ReorderableList;
        Texture2D m_InitialTexture;
        Editor m_TextureEditor;
        Texture m_TempTexture;
        SerializedProperty m_FilterMode;
        SerializedProperty m_WrapModeU;
        SerializedProperty m_WrapModeV;
        SerializedProperty m_WrapModeW;
        SerializedProperty m_AnisoLevel;
        SerializedProperty m_ColorSpace;

        public override void OnEnable()
        {
            base.OnEnable();
            m_TempTexture = new Texture2D(1, 1);
            m_FilterMode = serializedObject.FindProperty("m_FilterMode");
            m_WrapModeU = serializedObject.FindProperty("m_WrapModeU");
            m_WrapModeV = serializedObject.FindProperty("m_WrapModeV");
            m_WrapModeW = serializedObject.FindProperty("m_WrapModeW");
            m_AnisoLevel = serializedObject.FindProperty("m_AnisoLevel");
            m_ColorSpace = serializedObject.FindProperty("m_ColorSpace");
            SyncTempTexture();
            m_ReorderableList = new ReorderableList(serializedObject, serializedObject.FindProperty("m_Texture2Ds"));
            m_ReorderableList.drawHeaderCallback += r => EditorGUI.LabelField(r, Styles.TexturesText);
            m_ReorderableList.drawElementCallback += DrawElement;
            m_ReorderableList.onReorderCallbackWithDetails += OnChanged;
            m_ReorderableList.elementHeight = 64;
            m_TextureEditor = Editor.CreateEditor(m_TempTexture);
        }

        public override void OnDisable()
        {
            base.OnDisable();
            DestroyImmediate(m_TextureEditor);
            m_TextureEditor = null;
            DestroyImmediate(m_TempTexture);
            m_TempTexture = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Init();
            SyncTempTexture();
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
            m_ReorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
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
                if (m_ReorderableList.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue is Texture2D tex)
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

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var label = EditorGUIUtility.TrTextContent($"Element{index}");
            var property = m_ReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            var tex = property.objectReferenceValue as Texture2D;
            if (tex == null || m_InitialTexture == null || tex.width != m_InitialTexture.width || tex.height != m_InitialTexture.height || tex.format != m_InitialTexture.format || tex.mipmapCount != m_InitialTexture.mipmapCount)
                label.image = Styles.ErrorIcon.image;
            else
                label.image = null;
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
