using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using ReorderableList = UnityEditorInternal.ReorderableList;

namespace MomomaAssets
{
    [CustomEditor(typeof(PseudoAnimatorOverride))]
    [CanEditMultipleObjects]
    sealed class PseudoAnimatorOverrideInspector : ScriptedImporterEditor
    {
        static class Style
        {
            public static GUIContent s_Original = EditorGUIUtility.TrTextContent("Original");
            public static GUIContent s_Override = EditorGUIUtility.TrTextContent("Override");
        }

        ReorderableList m_MotionList;
        ReorderableList m_AvatarMaskList;
        SerializedProperty m_AnimatorControllerProperty;
        SerializedProperty m_OverrideMotionsProperty;
        SerializedProperty m_OverrideAvatarMasksProperty;

        public override bool showImportedObject => false;
        protected override bool needsApplyRevert => false;

        public override void OnEnable()
        {
            base.OnEnable();
            var pseudoao = target as PseudoAnimatorOverride;
            m_AnimatorControllerProperty = serializedObject.FindProperty("m_AnimatorController");
            m_OverrideMotionsProperty = serializedObject.FindProperty("m_OverrideMotions");
            m_OverrideAvatarMasksProperty = serializedObject.FindProperty("m_OverrideAvatarMasks");
            m_MotionList = new ReorderableList(serializedObject, m_OverrideMotionsProperty, false, true, false, false);
            m_MotionList.drawHeaderCallback = DrawHeader;
            m_MotionList.drawElementCallback = DrawMotionElemnt;
            m_MotionList.onSelectCallback = pseudoao.SelectClip;
            m_AvatarMaskList = new ReorderableList(serializedObject, m_OverrideAvatarMasksProperty, false, true, false, false);
            m_AvatarMaskList.drawHeaderCallback = DrawHeader;
            m_AvatarMaskList.drawElementCallback = DrawAvatarMaskElemnt;
            m_AvatarMaskList.onSelectCallback = pseudoao.SelectAvatarMask;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_AnimatorControllerProperty);
            m_MotionList.DoLayoutList();
            m_AvatarMaskList.DoLayoutList();
            if (serializedObject.hasModifiedProperties)
            {
                ApplyAndImport();
            }
            ApplyRevertGUI();
        }

        void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, Style.s_Original, Style.s_Override, EditorStyles.label);
        }

        void DrawMotionElemnt(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (var element = m_OverrideMotionsProperty.GetArrayElementAtIndex(index))
            {
                EditorGUI.PropertyField(rect, element);
            }
        }

        void DrawAvatarMaskElemnt(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (var element = m_OverrideAvatarMasksProperty.GetArrayElementAtIndex(index))
            {
                EditorGUI.PropertyField(rect, element);
            }
        }
    }
}
