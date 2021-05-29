using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    [InitializeOnLoad]
    sealed class FaceSnap
    {
        const string k_PrefKey = "FaceSnap_Enabled";

        static FaceSnap()
        {
            s_Enabled = EditorPrefs.GetBool(k_PrefKey);
            SceneView.onSceneGUIDelegate += OnSceneGUI;
            EditorApplication.update += Update;
            Selection.selectionChanged += OnSelectionChanged;
        }

        static readonly Dictionary<Transform, Bounds> s_BoundsCache = new Dictionary<Transform, Bounds>();

        static bool s_Enabled = false;

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
                    if (bounds.extents != Vector3.zero)
                    {
                        var results = new RaycastHit[1];
                        if (Physics.RaycastNonAlloc(bounds.center, Vector3.down, results, bounds.size.y * 2f, Physics.AllLayers) > 0)
                        {
                            t.position += results[0].point - bounds.extents.y * Vector3.down - bounds.center;
                        }
                    }
                }
            }
        }
    }
}
