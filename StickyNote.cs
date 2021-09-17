#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    [ExecuteInEditMode]
    public sealed class StickyNote : MonoBehaviour
    {
        static class Styles
        {
            public static readonly GUIContent s_StickyNote = new GUIContent();
        }

        [SerializeField]
        string m_Note = string.Empty;

        void Reset()
        {
            hideFlags |= HideFlags.DontSaveInBuild;
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        void OnSceneGUI(SceneView sceneView)
        {
            Handles.BeginGUI();
            try
            {
                var position = HandleUtility.WorldToGUIPoint(transform.position);
                position += new Vector2(EditorGUIUtility.singleLineHeight, EditorGUIUtility.singleLineHeight);
                Styles.s_StickyNote.text = m_Note;
                var size = EditorStyles.label.CalcSize(Styles.s_StickyNote);
                var rect = new Rect(position, size);
                EditorGUI.DrawRect(rect, Color.black);
                EditorGUI.SelectableLabel(rect, m_Note);
            }
            finally
            {
                Handles.EndGUI();
            }
        }
    }
}
#endif
