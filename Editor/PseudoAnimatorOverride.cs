using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor.Animations;
using ReorderableList = UnityEditorInternal.ReorderableList;

namespace MomomaAssets
{
    [ScriptedImporter(1, "pseudoao")]
    sealed class PseudoAnimatorOverride : ScriptedImporter
    {
        const string k_DefaultAssetName = "NewPseudoAnimatorOverride";

        [Serializable]
        sealed class OverrideMotion
        {
            public Motion originalMotion => m_OriginalMotion;
            public Motion overrideMotion => m_OverrideMotion;

            [SerializeField]
            Motion m_OriginalMotion = null;
            [SerializeField]
            Motion m_OverrideMotion = null;
        }

        [SerializeField]
        AnimatorController m_AnimatorController = null;
        [SerializeField]
        OverrideMotion[] m_OverrideMotions = null;

        [MenuItem("Assets/Create/Pseudo Animator Override", false, 409)]
        static void Create()
        {
            ProjectWindowUtil.CreateAssetWithContent($"{Selection.activeObject?.name ?? k_DefaultAssetName}.pseudoao", "MomomaAssets.PseudoAnimatorOverride");
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (m_AnimatorController == null)
            {
                var controller = new AnimatorController();
                ctx.AddObjectToAsset("DefaultController", controller);
                ctx.SetMainObject(controller);
                return;
            }
            var path = AssetDatabase.GetAssetPath(m_AnimatorController);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"{m_AnimatorController.name} is not an asset.", m_AnimatorController);
                return;
            }
            var newAnimatorController = Instantiate(m_AnimatorController);
            newAnimatorController.name = m_AnimatorController.name;
            ctx.AddObjectToAsset($"{newAnimatorController.name}", newAnimatorController);
            var animatorAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            var index = 0;
            foreach (var asset in animatorAssets)
            {
                if (asset == null || asset == m_AnimatorController)
                    continue;
                ctx.AddObjectToAsset($"{asset.name}{index}", asset);
                ++index;
            }
            ctx.SetMainObject(newAnimatorController);
            ctx.DependsOnSourceAsset(path);
            var overrideMotions = new Dictionary<Motion, Motion>();
            foreach (var overrideMotion in m_OverrideMotions)
                if (overrideMotion.originalMotion != null && overrideMotion.overrideMotion != null)
                    overrideMotions.Add(overrideMotion.originalMotion, overrideMotion.overrideMotion);
            var motions = new HashSet<Motion>();
            foreach (var layer in newAnimatorController.layers)
                ReplaceMotion(overrideMotions, layer.stateMachine);
        }

        void ReplaceMotion(Dictionary<Motion, Motion> overrideMotions, AnimatorStateMachine stateMachine)
        {
            foreach (var state in stateMachine.states)
                if (state.state?.motion != null && overrideMotions.TryGetValue(state.state.motion, out var overrideMotion))
                    state.state.motion = overrideMotion;
            foreach (var childMachine in stateMachine.stateMachines)
                ReplaceMotion(overrideMotions, childMachine.stateMachine);
        }

        [CustomEditor(typeof(PseudoAnimatorOverride))]
        sealed class PseudoAnimatorOverrideInspector : AssetImporterEditor
        {
            ReorderableList m_ReorderableList;
            SerializedProperty m_AnimatorController;
            SerializedProperty m_OverrideMotions;

            public override bool showImportedObject => false;

            public override void OnEnable()
            {
                base.OnEnable();
                m_AnimatorController = serializedObject.FindProperty(nameof(m_AnimatorController));
                m_OverrideMotions = serializedObject.FindProperty(nameof(m_OverrideMotions));
                m_ReorderableList = new ReorderableList(serializedObject, m_OverrideMotions, false, true, false, false);
                m_ReorderableList.drawHeaderCallback = DrawHeader;
                m_ReorderableList.drawElementCallback = DrawElemnt;
                m_ReorderableList.onSelectCallback = SelectClip;
                m_ReorderableList.elementHeight = 16f;
            }

            public override void OnInspectorGUI()
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_AnimatorController);
                if (EditorGUI.EndChangeCheck())
                {
                    ResetOverrideMotions();
                }
                m_ReorderableList.DoLayoutList();
                ApplyRevertGUI();
            }

            void ResetOverrideMotions()
            {
                if (m_AnimatorController.objectReferenceValue == null)
                {
                    m_OverrideMotions.arraySize = 0;
                    return;
                }
                var controller = m_AnimatorController.objectReferenceValue as AnimatorController;
                var motions = new HashSet<Motion>();
                foreach (var layer in controller.layers)
                    CollectMotion(motions, layer.stateMachine);
                motions.Remove(null);
                m_OverrideMotions.arraySize = motions.Count;
                var index = 0;
                foreach (var motion in motions)
                {
                    using (var element = m_OverrideMotions.GetArrayElementAtIndex(index))
                    using (var m_OriginalMotionProperty = element.FindPropertyRelative("m_OriginalMotion"))
                        m_OriginalMotionProperty.objectReferenceValue = motion;
                    ++index;
                }
            }

            void CollectMotion(HashSet<Motion> motions, AnimatorStateMachine stateMachine)
            {
                motions.UnionWith(stateMachine.states.Select(s => s.state.motion));
                foreach (var childMachine in stateMachine.stateMachines)
                    CollectMotion(motions, childMachine.stateMachine);
            }

            void DrawHeader(Rect rect)
            {
                EditorGUI.LabelField(rect, "Original", "Override", EditorStyles.label);
            }

            void DrawElemnt(Rect rect, int index, bool isActive, bool isFocused)
            {
                using (var element = m_OverrideMotions.GetArrayElementAtIndex(index))
                using (var m_OriginalMotionProperty = element.FindPropertyRelative("m_OriginalMotion"))
                using (var m_OverrideMotionProperty = element.FindPropertyRelative("m_OverrideMotion"))
                {
                    EditorGUI.PropertyField(rect, m_OverrideMotionProperty, new GUIContent(m_OriginalMotionProperty.objectReferenceValue?.name));
                }
            }
            void SelectClip(ReorderableList list)
            {
                if (0 <= list.index && list.index < m_OverrideMotions.arraySize)
                {
                    using (var element = m_OverrideMotions.GetArrayElementAtIndex(list.index))
                    using (var m_OriginalMotionProperty = element.FindPropertyRelative("m_OriginalMotion"))
                        EditorGUIUtility.PingObject(m_OriginalMotionProperty.objectReferenceValue);
                }
            }
        }
    }
}
