
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AssetImporters;
using UnityEngine;
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
            [CustomPropertyDrawer(typeof(OverrideMotion))]
            sealed class OverrideMotionDrawer : PropertyDrawer
            {
                public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
                {
                    using (var m_OriginalMotionProperty = property.FindPropertyRelative(nameof(m_OriginalMotion)))
                    using (var m_OverrideMotionProperty = property.FindPropertyRelative(nameof(m_OverrideMotion)))
                    {
                        label.text = m_OriginalMotionProperty.objectReferenceValue?.name;
                        EditorGUI.PropertyField(position, m_OverrideMotionProperty, label);
                    }
                }
            }

            public Motion originalMotion => m_OriginalMotion;
            public Motion overrideMotion { get => m_OverrideMotion; set => m_OverrideMotion = value; }

            [SerializeField]
            Motion m_OriginalMotion = null;
            [SerializeField]
            Motion m_OverrideMotion = null;

            public OverrideMotion(Motion motion) => m_OriginalMotion = motion;
        }

        [Serializable]
        sealed class OverrideAvatarMask
        {
            [CustomPropertyDrawer(typeof(OverrideAvatarMask))]
            sealed class OverrideAvatarMaskDrawer : PropertyDrawer
            {
                public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
                {
                    using (var m_OriginalAvatarMaskProperty = property.FindPropertyRelative(nameof(m_OriginalAvatarMask)))
                    using (var m_OverrideAvatarMaskProperty = property.FindPropertyRelative(nameof(m_OverrideAvatarMask)))
                    {
                        label.text = m_OriginalAvatarMaskProperty.objectReferenceValue?.name;
                        EditorGUI.PropertyField(position, m_OverrideAvatarMaskProperty, label);
                    }
                }
            }

            public AvatarMask originalAvatarMask => m_OriginalAvatarMask;
            public AvatarMask overrideAvatarMask => m_OverrideAvatarMask;

            [SerializeField]
            AvatarMask m_OriginalAvatarMask = null;
            [SerializeField]
            AvatarMask m_OverrideAvatarMask = null;

            public OverrideAvatarMask(AvatarMask avatarMask) => m_OriginalAvatarMask = avatarMask;
        }

        [SerializeField]
        AnimatorController m_AnimatorController = null;
        [SerializeField]
        OverrideMotion[] m_OverrideMotions = Array.Empty<OverrideMotion>();
        [SerializeField]
        OverrideAvatarMask[] m_OverrideAvatarMasks = Array.Empty<OverrideAvatarMask>();
        [SerializeField]
        AnimationClip m_ResetAnimationClip = null;

        [MenuItem("Assets/Create/Pseudo Animator Override", false, 409)]
        static void Create()
        {
            ProjectWindowUtil.CreateAssetWithContent($"{Selection.activeObject?.name ?? k_DefaultAssetName}.pseudoao", "MomomaAssets.PseudoAnimatorOverride");
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var mainAssetKey = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (m_AnimatorController == null)
            {
                var controller = new AnimatorController();
                ctx.AddObjectToAsset(mainAssetKey, controller);
                ctx.SetMainObject(controller);
                m_ResetAnimationClip = null;
                m_OverrideMotions = Array.Empty<OverrideMotion>();
                m_OverrideAvatarMasks = Array.Empty<OverrideAvatarMask>();
                return;
            }
            var path = AssetDatabase.GetAssetPath(m_AnimatorController);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"{m_AnimatorController.name} is not an asset.", m_AnimatorController);
                return;
            }
            var motions = new HashSet<Motion>();
            var avatarMasks = new HashSet<AvatarMask>();
            foreach (var layer in m_AnimatorController.layers)
            {
                CollectMotion(motions, layer.stateMachine);
                var avatarMask = layer.avatarMask;
                if (avatarMask != null)
                    avatarMasks.Add(avatarMask);
            }
            motions.Remove(null);
            if (m_OverrideMotions.Length != motions.Count)
                m_OverrideMotions = new OverrideMotion[motions.Count];
            var index = 0;
            foreach (var i in motions)
            {
                if (m_OverrideMotions[index]?.originalMotion != i)
                    m_OverrideMotions[index] = new OverrideMotion(i);
                ++index;
            }
            if (m_OverrideAvatarMasks.Length != avatarMasks.Count)
                m_OverrideAvatarMasks = new OverrideAvatarMask[avatarMasks.Count];
            index = 0;
            foreach (var i in avatarMasks)
            {
                if (m_OverrideAvatarMasks[index]?.originalAvatarMask != i)
                    m_OverrideAvatarMasks[index] = new OverrideAvatarMask(i);
                ++index;
            }
            var newAnimatorController = Instantiate(m_AnimatorController);
            newAnimatorController.name = mainAssetKey;
            ctx.AddObjectToAsset(mainAssetKey, newAnimatorController);
            var animatorAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            index = 0;
            foreach (var asset in animatorAssets)
            {
                if (asset == null || asset == m_AnimatorController)
                    continue;
                ctx.AddObjectToAsset($"{asset.name}{index}", asset);
                ++index;
            }
            ctx.SetMainObject(newAnimatorController);
            ctx.DependsOnSourceAsset(path);
            var resetAnimationClip = new AnimationClip() { name = "Reset" };
            var overrideMotions = new Dictionary<Motion, Motion>();
            var newCurve = AnimationCurve.Constant(0, 1, 0);
            var currentResetClipId = m_ResetAnimationClip?.GetInstanceID();
            foreach (var overrideMotion in m_OverrideMotions)
            {
                if (overrideMotion.originalMotion != null)
                {
                    if (currentResetClipId != 0 && overrideMotion.overrideMotion?.GetInstanceID() == currentResetClipId)
                    {
                        overrideMotion.overrideMotion = resetAnimationClip;
                        overrideMotions.Add(overrideMotion.originalMotion, resetAnimationClip);
                    }
                    else
                    {
                        if (overrideMotion.overrideMotion != null)
                            overrideMotions.Add(overrideMotion.originalMotion, overrideMotion.overrideMotion);
                        if (overrideMotion.overrideMotion is AnimationClip animationClip)
                        {
                            ctx.DependsOnSourceAsset(AssetDatabase.GetAssetPath(animationClip));
                            var bindings = AnimationUtility.GetCurveBindings(animationClip);
                            foreach (var i in bindings)
                            {
                                var curve = AnimationUtility.GetEditorCurve(animationClip, i);
                                if (curve == null || curve.length == 0)
                                    continue;
                                var newKey = newCurve[0];
                                newKey.value = curve[0].value;
                                newCurve.MoveKey(0, newKey);
                                newKey.time = newCurve[1].time;
                                newCurve.MoveKey(1, newKey);
                                AnimationUtility.SetEditorCurve(resetAnimationClip, i, newCurve);
                            }
                            var objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);
                            foreach (var i in bindings)
                            {
                                var curve = AnimationUtility.GetObjectReferenceCurve(animationClip, i);
                                if (curve == null || curve.Length == 0)
                                    continue;
                                var newObjectReferenceCurve0 = curve[0];
                                newObjectReferenceCurve0.time = 0;
                                var newObjectReferenceCurve1 = curve[0];
                                newObjectReferenceCurve1.time = 1;
                                curve = new ObjectReferenceKeyframe[] { newObjectReferenceCurve0, newObjectReferenceCurve1 };
                                AnimationUtility.SetObjectReferenceCurve(resetAnimationClip, i, curve);
                            }
                        }
                    }
                }
            }
            ctx.AddObjectToAsset("ResetClip", resetAnimationClip);
            m_ResetAnimationClip = resetAnimationClip;
            var overrideAvatarMasks = new Dictionary<AvatarMask, AvatarMask>();
            foreach (var overrideAvatarMask in m_OverrideAvatarMasks)
                if (overrideAvatarMask.originalAvatarMask != null && overrideAvatarMask.overrideAvatarMask != null)
                    overrideAvatarMasks.Add(overrideAvatarMask.originalAvatarMask, overrideAvatarMask.overrideAvatarMask);
            var layers = newAnimatorController.layers;
            foreach (var layer in layers)
            {
                ReplaceMotion(overrideMotions, layer.stateMachine);
                if (layer.avatarMask != null && overrideAvatarMasks.TryGetValue(layer.avatarMask, out var overrideAvatarMask))
                    layer.avatarMask = overrideAvatarMask;
            }
            newAnimatorController.layers = layers;
        }

        void ReplaceMotion(Dictionary<Motion, Motion> overrideMotions, AnimatorStateMachine stateMachine)
        {
            foreach (var state in stateMachine.states)
                if (state.state?.motion != null && overrideMotions.TryGetValue(state.state.motion, out var overrideMotion))
                    state.state.motion = overrideMotion;
            foreach (var childMachine in stateMachine.stateMachines)
                ReplaceMotion(overrideMotions, childMachine.stateMachine);
        }

        public void SelectClip(ReorderableList list)
        {
            var index = list.index;
            if (0 <= index && index < m_OverrideMotions.Length)
                EditorGUIUtility.PingObject(m_OverrideMotions[index].originalMotion);
        }

        public void SelectAvatarMask(ReorderableList list)
        {
            var index = list.index;
            if (0 <= index && index < m_OverrideAvatarMasks.Length)
                EditorGUIUtility.PingObject(m_OverrideAvatarMasks[index].originalAvatarMask);
        }

        void CollectMotion(HashSet<Motion> motions, AnimatorStateMachine stateMachine)
        {
            motions.UnionWith(stateMachine.states.Select(i => i.state.motion));
            foreach (var childMachine in stateMachine.stateMachines)
                CollectMotion(motions, childMachine.stateMachine);
        }
    }
}
