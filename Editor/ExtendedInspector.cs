using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    class ExtendedInspector : EditorWindow
    {
        static readonly Type s_InspectorWindowType = Type.GetType("UnityEditor.InspectorWindow, UnityEditor.dll");
        static readonly MethodInfo s_OnGUIInfo = s_InspectorWindowType.GetMethod("OnGUI", BindingFlags.NonPublic | BindingFlags.Instance);

        List<EditorWindow> m_InspectorWindows = new List<EditorWindow>();
        Vector2 m_ScrollPos;

        [MenuItem("MomomaTools/ExtendedInspector")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<ExtendedInspector>("ExtendedInspector");
        }

        void OnDisable()
        {
            foreach (var win in m_InspectorWindows)
            {
                if (win != null)
                    DestroyImmediate(win);
            }
        }

        void OnGUI()
        {
            if (m_InspectorWindows.Count < 2)
            {
                m_InspectorWindows.Add(ScriptableObject.CreateInstance(s_InspectorWindowType) as EditorWindow);
                m_InspectorWindows.Add(ScriptableObject.CreateInstance(s_InspectorWindowType) as EditorWindow);
            }

            using (var scroll = new GUILayout.ScrollViewScope(m_ScrollPos, true, false))
            using (new EditorGUILayout.HorizontalScope(GUILayout.MinWidth(position.width * 2f)))
            {
                GUILayout.FlexibleSpace();
                var area = position;
                area.position = Vector2.zero;
                using (new GUILayout.AreaScope(area))
                {
                    s_OnGUIInfo.Invoke(m_InspectorWindows[0], new object[] { });
                }
                area.x += position.width;
                using (new GUILayout.AreaScope(area))
                {
                    s_OnGUIInfo.Invoke(m_InspectorWindows[1], new object[] { });
                }
                GUILayout.FlexibleSpace();
                m_ScrollPos = scroll.scrollPosition;
            }
        }
    }

}// namespace MomomaAssets
