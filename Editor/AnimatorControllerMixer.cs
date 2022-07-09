using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
#if VRC_SDK_VRCSDK3 && !VRC_SDK_WORLD
using VRC.SDK3.Avatars.Components;
#endif
using ReorderableList = UnityEditorInternal.ReorderableList;

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
            [Range(0f, 1f)]
            public float Weight = 1f;
        }

        PreviewRenderUtility Preview
        {
            get
            {
                if (preview == null)
                {
                    preview = new PreviewRenderUtility();
                    preview.camera.backgroundColor = Color.gray;
                    cameraTransform = preview.camera.transform;
                    UpdateCamera();
                }
                return preview;
            }
        }

        [SerializeField]
        Animator animator;
        [SerializeField]
        List<AnimatorControllerInfo> animatorControllers = new List<AnimatorControllerInfo>() { new AnimatorControllerInfo() };
        [SerializeField]
        List<RuntimeAnimatorController> currentAnimatorControllers = new List<RuntimeAnimatorController>() { null };
        [SerializeField]
        bool isPlaying;
        [SerializeField]
        Vector2 parametersScrollPos;
        [SerializeField]
        Vector3 cameraRotation = new Vector3(0f, 180f, 0f);
        [SerializeField]
        Vector3 cameraPosition = new Vector3(0f, 0f, -8f);

        ReorderableList animatorControllersList;
        PlayableGraph graph;
        AnimationLayerMixerPlayable mixerPlayable;
        Dictionary<AnimatorControllerParameter, List<int>> parameters = new Dictionary<AnimatorControllerParameter, List<int>>();
        PreviewRenderUtility preview;
        Transform cameraTransform;
        Animator previewAnimator;
        SerializedObject serializedObject;
        SerializedProperty animatorControllersProperty;

        readonly int previewHint = "Animixer Preview".GetHashCode();

        [MenuItem("MomomaTools/Animator Controlle rMixer", false, 700)]
        static void ShowWindow()
        {
            GetWindow<AnimatorControllerMixer>(ObjectNames.NicifyVariableName(nameof(AnimatorControllerMixer)));
        }

        void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            animatorControllersProperty = serializedObject.FindProperty(nameof(animatorControllers));
            animatorControllersList = new ReorderableList(serializedObject, animatorControllersProperty, true, true, true, true);
            animatorControllersList.drawElementCallback = DrawElement;
            animatorControllersList.elementHeightCallback = index => animatorControllersProperty.GetArrayElementAtIndex(index).isExpanded ? animatorControllersList.elementHeight * 5f : animatorControllersList.elementHeight;
            animatorControllersList.onReorderCallback = list => ReloadPlayable();
            ReloadPlayableGraph();
            Undo.undoRedoPerformed -= UpdatePlayable;
            Undo.undoRedoPerformed += UpdatePlayable;
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
            currentAnimatorControllers.Clear();
            Undo.undoRedoPerformed -= UpdatePlayable;
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                var animatorControllerInfoProperty = animatorControllersProperty.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, animatorControllerInfoProperty, true);
                var currentController = animatorControllerInfoProperty.FindPropertyRelative(nameof(AnimatorControllerInfo.AnimatorController)).objectReferenceValue;
                if (currentAnimatorControllers[index] != currentController)
                {
                    ReloadPlayable();
                }
                else if (change.changed)
                {
                    UpdatePlayable();
                }
            }
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            animator = EditorGUILayout.ObjectField(Styles.AnimatorText, animator, typeof(Animator), true) as Animator;
            if (EditorGUI.EndChangeCheck())
                ReloadPlayableGraph();
            serializedObject.Update();
            animatorControllersList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
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

                    var controlID = EditorGUIUtility.GetControlID(previewHint, FocusType.Passive);
                    var evt = Event.current;
                    var eventType = evt.GetTypeForControl(controlID);
                    switch (eventType)
                    {
                        case EventType.MouseDown:
                            if (rect.Contains(evt.mousePosition))
                            {
                                EditorGUIUtility.SetWantsMouseJumping(1);
                                evt.Use();
                                GUIUtility.hotControl = controlID;
                            }
                            break;
                        case EventType.MouseUp:
                            if (GUIUtility.hotControl == controlID)
                            {
                                GUIUtility.hotControl = 0;
                                EditorGUIUtility.SetWantsMouseJumping(0);
                                evt.Use();
                            }
                            break;
                        case EventType.MouseDrag:
                            if (GUIUtility.hotControl == controlID)
                            {
                                if (evt.button == 0)
                                {
                                    cameraPosition += new Vector3(-evt.delta.x, evt.delta.y, 0) * 0.001f * -cameraPosition.z;
                                    UpdateCamera();
                                    evt.Use();
                                }
                                else if (evt.button == 1)
                                {
                                    cameraRotation.y += evt.delta.x;
                                    cameraRotation.x = Mathf.Clamp(cameraRotation.x + evt.delta.y, -90f, 90f);
                                    UpdateCamera();
                                    evt.Use();
                                }
                                else if (evt.button == 2)
                                {
                                    cameraPosition.z -= evt.delta.y * 0.01f;
                                    UpdateCamera();
                                    evt.Use();
                                }
                            }
                            break;
                        case EventType.ScrollWheel:
                            cameraPosition.z = Mathf.Clamp(cameraPosition.z + HandleUtility.niceMouseDeltaZoom * 0.5f, -20f, -0.1f);
                            UpdateCamera();
                            evt.Use();
                            break;
                    }

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

        void UpdateCamera()
        {
            var rot = Quaternion.Euler(cameraRotation);
            cameraTransform.rotation = rot;
            cameraTransform.position = rot * cameraPosition;
            preview.camera.nearClipPlane = Mathf.Max(-cameraPosition.z - 2f, 0.1f);
            preview.camera.farClipPlane = -cameraPosition.z + 2f;
        }

        AnimatorControllerPlayable GetAnimatorControllerPlayable(int index)
        {
            return (AnimatorControllerPlayable)mixerPlayable.GetInput(index);
        }

        void ReloadPlayableGraph()
        {
            if (animator == null)
                return;
            if (previewAnimator != null)
                DestroyImmediate(previewAnimator.gameObject);
            previewAnimator = Instantiate(animator);
            Preview.AddSingleGO(previewAnimator.gameObject);
            var renderers = previewAnimator.GetComponentsInChildren<Renderer>();
            var range = new Vector2(100f, -100f);
            foreach (var r in renderers)
            {
                var bounds = r.bounds;
                if (bounds.max.y > range.y)
                    range.y = bounds.max.y;
                if (bounds.min.y < range.x)
                    range.x = bounds.min.y;
            }
            previewAnimator.transform.localPosition = new Vector3(0, -(range.x + range.y) * 0.5f, 0);
            previewAnimator.transform.localRotation = Quaternion.identity;
            if (graph.IsValid())
                graph.Destroy();
            mixerPlayable = AnimationPlayableUtilities.PlayLayerMixer(previewAnimator, 0, out graph);
#if VRC_SDK_VRCSDK3 && !VRC_SDK_WORLD
            var avatarDescriptor = previewAnimator.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor != null)
            {
                animatorControllers.Clear();
                foreach (var layer in avatarDescriptor.baseAnimationLayers)
                {
                    animatorControllers.Add(new AnimatorControllerInfo()
                    {
                        AnimatorController = layer.isDefault ? GetDefaultController(layer.type) : layer.animatorController,
                        Mask = layer.isDefault && layer.type == VRCAvatarDescriptor.AnimLayerType.Gesture ? GetDefaultHandsOnlyMask() : layer.mask,
                        IsAdditive = layer.type == VRCAvatarDescriptor.AnimLayerType.Additive,
                        Weight = layer.type == VRCAvatarDescriptor.AnimLayerType.Action ? 0f : 1f,
                    });
                }
            }
#endif
            ReloadPlayable();
            UpdatePlaying();
        }

#if VRC_SDK_VRCSDK3 && !VRC_SDK_WORLD
        static RuntimeAnimatorController GetDefaultController(VRCAvatarDescriptor.AnimLayerType animLayerType)
        {
            switch (animLayerType)
            {
                case VRCAvatarDescriptor.AnimLayerType.Base: return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3LocomotionLayer.controller");
                case VRCAvatarDescriptor.AnimLayerType.Additive: return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3IdleLayer.controller");
                case VRCAvatarDescriptor.AnimLayerType.Gesture: return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3HandsLayer.controller");
                case VRCAvatarDescriptor.AnimLayerType.Action: return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3ActionLayer.controller");
                case VRCAvatarDescriptor.AnimLayerType.FX: return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3FaceLayer.controller");
                default: throw new ArgumentOutOfRangeException(nameof(animLayerType));
            }
        }

        static AvatarMask GetDefaultHandsOnlyMask()
        {
            return AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/VRCSDK/Examples3/Animation/Masks/vrc_HandsOnly.mask");
        }
#endif

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
            currentAnimatorControllers.Clear();
            foreach (var info in animatorControllers)
            {
                if (info == null || info.AnimatorController == null)
                    continue;
                currentAnimatorControllers.Add(info.AnimatorController);
                var controllerPlayable = AnimatorControllerPlayable.Create(graph, info.AnimatorController);
                var index = mixerPlayable.AddInput(controllerPlayable, 0, 1f);
                mixerPlayable.SetLayerAdditive((uint)index, info.IsAdditive);
                if (info.Mask != null)
                    mixerPlayable.SetLayerMaskFromAvatarMask((uint)index, info.Mask);
                mixerPlayable.SetInputWeight(index, info.Weight);
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

        void UpdatePlayable()
        {
            if (!mixerPlayable.IsValid())
                return;
            for (var i = 0; i < animatorControllers.Count; ++i)
            {
                var info = animatorControllers[i];
                mixerPlayable.SetLayerAdditive((uint)i, info.IsAdditive);
                mixerPlayable.SetInputWeight(i, info.Weight);
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
