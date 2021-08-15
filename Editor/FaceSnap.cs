using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    [InitializeOnLoad]
    sealed class FaceSnap
    {
        enum PivotMode
        {
            Center, Origin
        }

        const string k_PrefKey = "FaceSnap_Enabled";
        const string k_PrefKeyPivotMode = "FaceSnap_PivotMode";

        static FaceSnap()
        {
            s_Enabled = EditorPrefs.GetBool(k_PrefKey, false);
            s_PivotMode = (PivotMode)EditorPrefs.GetInt(k_PrefKeyPivotMode, 0);
#if UNITY_2019_1_OR_NEWER
            SceneView.duringSceneGui += OnSceneGUI;
#else
            SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
        }

        static readonly Dictionary<Transform, Bounds> s_BoundsCache = new Dictionary<Transform, Bounds>();

        static bool s_Enabled;
        static PivotMode s_PivotMode;

        static void OnSceneGUI(SceneView sceneView)
        {
            try
            {
                Handles.BeginGUI();
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    s_Enabled = GUILayout.Toggle(s_Enabled, "Face snap", GUI.skin.button);
                    if (EditorGUI.EndChangeCheck())
                        EditorPrefs.SetBool(k_PrefKey, s_Enabled);
                    EditorGUI.BeginChangeCheck();
                    s_PivotMode = (PivotMode)EditorGUILayout.EnumPopup(s_PivotMode);
                    if (EditorGUI.EndChangeCheck())
                        EditorPrefs.SetInt(k_PrefKeyPivotMode, (int)s_PivotMode);
                    GUILayout.FlexibleSpace();
                }
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        static void OnSelectionChanged()
        {
            s_BoundsCache.Clear();
        }

        static void Update()
        {
            if (!s_Enabled)
                return;
            if (Selection.transforms != null && Selection.transforms.Length > 0)
            {
                foreach (var t in Selection.transforms)
                {
                    if (!t.hasChanged)
                        continue;
                    var results = new RaycastHit[1];
                    var ray = new Ray() { direction = Vector3.down };
                    switch (s_PivotMode)
                    {
                        case PivotMode.Center:
                            if (!s_BoundsCache.TryGetValue(t, out var bounds))
                            {
                                var renderers = t.GetComponentsInChildren<Renderer>(false);
                                if (renderers.Length > 0)
                                {
                                    bounds = new Bounds();
                                    foreach (var r in renderers)
                                    {
                                        if (bounds.extents == Vector3.zero)
                                        {
                                            bounds = r.bounds;
                                        }
                                        else
                                        {
                                            bounds.Encapsulate(r.bounds);
                                        }
                                    }
                                }
                            }
                            ray.origin = bounds.center + (Physics.defaultContactOffset - bounds.extents.y) * Vector3.up;
                            break;
                        case PivotMode.Origin:
                            ray.origin = t.position + Physics.defaultContactOffset * Vector3.up;
                            break;
                        default: throw new System.ArgumentOutOfRangeException(nameof(s_PivotMode));
                    }
                    if (Physics.RaycastNonAlloc(ray, results, 1f, Physics.AllLayers) > 0)
                    {
                        t.position += results[0].distance * Vector3.down;
                    }
                }
            }
        }
    }
}
