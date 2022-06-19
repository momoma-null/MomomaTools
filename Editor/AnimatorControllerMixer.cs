using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MomomaAssets
{
    sealed class AnimatorControllerMixer : EditorWindow
    {
        static class Styles
        {
            public static readonly GUIContent AnimatorText = EditorGUIUtility.TrTextContent("Animator");
            public static readonly GUIContent ParametersText = EditorGUIUtility.TrTextContent("Parameters");
            public static readonly GUIContent PlayIcon = EditorGUIUtility.TrIconContent("Animation.Play");
        }

        [System.Serializable]
        class AnimatorControllerInfo
        {
            public RuntimeAnimatorController AnimatorController;
            public bool IsAdditive;
            public AvatarMask Mask;
        }

        PreviewRenderUtility Preview
        {
            get
            {
                if (preview == null)
                {
                    preview = new PreviewRenderUtility();
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.camera.backgroundColor = Color.clear;
                    preview.camera.transform.position = new Vector3(0, 1f, 8f);
                    preview.camera.transform.rotation = Quaternion.Euler(0, 180f, 0);
                }
                return preview;
            }
        }

        [SerializeField]
        Animator animator;
        [SerializeField]
        List<AnimatorControllerInfo> animatorControllers = new List<AnimatorControllerInfo>() { new AnimatorControllerInfo() };
        [SerializeField]
        bool isPlaying;
        [SerializeField]
        Vector2 parametersScrollPos;

        UnityEditorInternal.ReorderableList animatorControllersList;
        PlayableGraph graph;
        AnimationLayerMixerPlayable mixerPlayable;
        Dictionary<AnimatorControllerParameter, List<int>> parameters = new Dictionary<AnimatorControllerParameter, List<int>>();
        PreviewRenderUtility preview;
        Animator previewAnimator;
        SerializedObject serializedObject;
        SerializedProperty animatorControllersProperty;

        [MenuItem("MomomaTools/Animator Controlle rMixer", false, 700)]
        static void ShowWindow()
        {
            GetWindow<AnimatorControllerMixer>(ObjectNames.NicifyVariableName(nameof(AnimatorControllerMixer)));
        }

        void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            animatorControllersProperty = serializedObject.FindProperty(nameof(animatorControllers));
            animatorControllersList = new UnityEditorInternal.ReorderableList(serializedObject, animatorControllersProperty, true, true, true, true);
            animatorControllersList.drawElementCallback = DrawElement;
            animatorControllersList.elementHeightCallback = index => animatorControllersProperty.GetArrayElementAtIndex(index).isExpanded ? animatorControllersList.elementHeight * 4f : animatorControllersList.elementHeight;
            animatorControllersList.onReorderCallback = list => ReloadPlayable();
            ReloadPlayableGraph();
            ReloadPlayable();
        }

        void OnDisable()
        {
            if (graph.IsValid())
                graph.Destroy();
            preview?.Cleanup();
            preview = null;
            if (previewAnimator != null)
                DestroyImmediate(previewAnimator.gameObject);
            previewAnimator = null;
            parameters.Clear();
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            EditorGUI.PropertyField(rect, animatorControllersProperty.GetArrayElementAtIndex(index), true);
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            animator = EditorGUILayout.ObjectField(Styles.AnimatorText, animator, typeof(Animator), true) as Animator;
            if (EditorGUI.EndChangeCheck())
                ReloadPlayableGraph();
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            animatorControllersList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
                ReloadPlayable();
            EditorGUI.BeginChangeCheck();
            isPlaying = GUILayout.Toggle(isPlaying, Styles.PlayIcon, GUI.skin.button);
            if (EditorGUI.EndChangeCheck())
                UpdatePlaying();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(EditorGUIUtility.labelWidth + EditorGUIUtility.fieldWidth)))
                {
                    EditorGUILayout.LabelField(Styles.ParametersText, GUILayout.ExpandWidth(false));
                    using (var scroll = new EditorGUILayout.ScrollViewScope(parametersScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar))
                    {
                        parametersScrollPos = scroll.scrollPosition;
                        foreach (var i in parameters)
                        {
                            var parameter = i.Key;
                            var indices = i.Value;
                            var animatorPlayable = GetAnimatorControllerPlayable(indices[0]);
                            switch (parameter.type)
                            {
                                case AnimatorControllerParameterType.Float:
                                    var fv = animatorPlayable.GetFloat(parameter.nameHash);
                                    EditorGUI.BeginChangeCheck();
                                    fv = EditorGUILayout.FloatField(parameter.name, fv, GUILayout.ExpandWidth(false));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        foreach (var j in indices)
                                        {
                                            GetAnimatorControllerPlayable(j).SetFloat(parameter.nameHash, fv);
                                        }
                                    }
                                    break;
                                case AnimatorControllerParameterType.Int:
                                    var iv = animatorPlayable.GetInteger(parameter.nameHash);
                                    EditorGUI.BeginChangeCheck();
                                    iv = EditorGUILayout.IntField(parameter.name, iv, GUILayout.ExpandWidth(false));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        foreach (var j in indices)
                                        {
                                            GetAnimatorControllerPlayable(j).SetInteger(parameter.nameHash, iv);
                                        }
                                    }
                                    break;
                                case AnimatorControllerParameterType.Bool:
                                    var bv = animatorPlayable.GetBool(parameter.nameHash);
                                    EditorGUI.BeginChangeCheck();
                                    bv = EditorGUILayout.Toggle(parameter.name, bv, GUILayout.ExpandWidth(false));
                                    if (EditorGUI.EndChangeCheck())
                                    {
                                        foreach (var j in indices)
                                        {
                                            GetAnimatorControllerPlayable(j).SetBool(parameter.nameHash, bv);
                                        }
                                    }
                                    break;
                                case AnimatorControllerParameterType.Trigger:
                                    if (GUILayout.Button("Set", GUILayout.ExpandWidth(false)))
                                    {
                                        foreach (var j in indices)
                                        {
                                            GetAnimatorControllerPlayable(j).SetTrigger(parameter.nameHash);
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    var rect = EditorGUILayout.GetControlRect(false, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                    if (Event.current.type == EventType.Repaint)
                    {
                        Preview.BeginPreview(rect, GUIStyle.none);
                        Preview.Render();
                        Preview.EndAndDrawPreview(rect);
                    }
                }
            }
            if (isPlaying)
                Repaint();
        }

        AnimatorControllerPlayable GetAnimatorControllerPlayable(int index)
        {
            return (AnimatorControllerPlayable)mixerPlayable.GetInput(index);
        }

        void ReloadPlayableGraph()
        {
            if (animator == null)
                return;
            DestroyImmediate(previewAnimator);
            previewAnimator = Instantiate(animator);
            Preview.AddSingleGO(previewAnimator.gameObject);
            if (graph.IsValid())
                graph.Destroy();
            mixerPlayable = AnimationPlayableUtilities.PlayLayerMixer(previewAnimator, 0, out graph);
            UpdatePlaying();
        }

        void ReloadPlayable()
        {
            if (animator == null || !mixerPlayable.IsValid())
                return;
            parameters.Clear();
            for (var i = 0; i < mixerPlayable.GetInputCount(); ++i)
            {
                mixerPlayable.GetInput(i).Destroy();
            }
            mixerPlayable.SetInputCount(0);
            foreach (var info in animatorControllers)
            {
                if (info == null || info.AnimatorController == null)
                    continue;
                var controllerPlayable = AnimatorControllerPlayable.Create(graph, info.AnimatorController);
                var index = mixerPlayable.AddInput(controllerPlayable, 0, 1f);
                mixerPlayable.SetLayerAdditive((uint)index, info.IsAdditive);
                if (info.Mask != null)
                    mixerPlayable.SetLayerMaskFromAvatarMask((uint)index, info.Mask);
                var parameterCount = controllerPlayable.GetParameterCount();
                for (var i = 0; i < parameterCount; ++i)
                {
                    var parameter = controllerPlayable.GetParameter(i);
                    if (!parameters.TryGetValue(parameter, out var indices))
                    {
                        indices = new List<int>();
                        parameters.Add(parameter, indices);
                    }
                    indices.Add(index);
                }
            }
        }

        void UpdatePlaying()
        {
            if (graph.IsValid())
            {
                if (isPlaying)
                {
                    graph.Play();
                    graph.Evaluate(0f);
                }
                else
                {
                    graph.Stop();
                }
            }
        }
    }
}
