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
        static readonly MethodInfo s_GetObjectsLockedInfo = s_InspectorWindowType.GetMethod("GetObjectsLocked", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_SetObjectsLockedInfo = s_InspectorWindowType.GetMethod("SetObjectsLocked", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_TrackerInfo = s_InspectorWindowType.GetField("m_Tracker", BindingFlags.NonPublic | BindingFlags.Instance);
        static GUIContent s_PrevArrow;
        static GUIContent s_NextArrow;

        List<EditorWindow> m_InspectorWindows;
        int m_SelectedTabIndex;
        Vector2 m_ScrollPos;

        [MenuItem("MomomaTools/ExtendedInspector")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<ExtendedInspector>("ExtendedInspector");
        }

        void OnEnable()
        {
            m_InspectorWindows = new List<EditorWindow>();
            m_SelectedTabIndex = 0;
            m_ScrollPos = Vector2.zero;
            autoRepaintOnSceneChange = true;
            if (s_PrevArrow == null)
            {
                s_PrevArrow = EditorGUIUtility.TrIconContent("Profiler.PrevFrame");
            }
            if (s_NextArrow == null)
            {
                s_NextArrow = EditorGUIUtility.TrIconContent("Profiler.NextFrame");
            }
        }

        void OnDisable()
        {
            Clear();
        }

        void OnHierarchyChange()
        {
            Repaint();
        }

        void OnSelectionChange()
        {
            Repaint();
        }

        void OnGUI()
        {
            if (EditorApplication.isCompiling)
            {
                Clear();
                return;
            }
            var removingWindows = m_InspectorWindows.Where((win, i) => win != null && i != 0 && !(s_TrackerInfo.GetValue(win) as ActiveEditorTracker).isLocked).ToArray();
            foreach (var win in removingWindows)
            {
                DestroyImmediate(win);
            }
            m_InspectorWindows.RemoveAll(win => !win);
            if (m_InspectorWindows.Count < 1)
            {
                m_InspectorWindows.Add(ScriptableObject.CreateInstance(s_InspectorWindowType) as EditorWindow);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                m_SelectedTabIndex = Mathf.Clamp(m_SelectedTabIndex, 0, m_InspectorWindows.Count - 1);
                var scrollPosMax = 0f;
                using (var horizontal = new EditorGUILayout.HorizontalScope())
                using (new EditorGUILayout.ScrollViewScope(m_ScrollPos, GUIStyle.none, GUIStyle.none))
                using (var innerHorizontal = new EditorGUILayout.HorizontalScope())
                {
                    scrollPosMax = innerHorizontal.rect.width - horizontal.rect.width;
                    m_SelectedTabIndex = GUILayout.Toolbar(m_SelectedTabIndex, GetObjectNames(), EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents);
                    if (Event.current.delta != Vector2.zero && horizontal.rect.Contains(Event.current.mousePosition - m_ScrollPos + horizontal.rect.min))
                    {
                        m_ScrollPos.x -= Event.current.delta.x;
                        m_ScrollPos.x = Math.Max(m_ScrollPos.x, 0f);
                        m_ScrollPos.x = Math.Min(m_ScrollPos.x, scrollPosMax);
                        Repaint();
                    }
                    GUILayout.FlexibleSpace();
                }
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    using (new EditorGUI.DisabledScope(m_SelectedTabIndex <= 0))
                    {
                        if (GUILayout.Button(s_PrevArrow, EditorStyles.toolbarButton))
                        {
                            --m_SelectedTabIndex;
                        }
                    }
                    using (new EditorGUI.DisabledScope(m_SelectedTabIndex >= m_InspectorWindows.Count - 1))
                    {
                        if (GUILayout.Button(s_NextArrow, EditorStyles.toolbarButton))
                        {
                            ++m_SelectedTabIndex;
                        }
                    }
                    if (check.changed)
                    {
                        var names = GetObjectNames();
                        m_ScrollPos.x = 0f;
                        for (var i = 0; i < m_SelectedTabIndex; ++i)
                        {
                            if (i > 0)
                                m_ScrollPos.x += Math.Max(EditorStyles.toolbarButton.margin.right, EditorStyles.toolbarButton.margin.left);
                            m_ScrollPos.x += EditorStyles.toolbarButton.CalcSize(new GUIContent(names[i])).x;
                        }
                        m_ScrollPos.x = Math.Min(m_ScrollPos.x, scrollPosMax);
                    }
                }
                using (new EditorGUI.DisabledScope(Selection.objects == null || Selection.objects.Length == 0))
                {
                    if (GUILayout.Button("+", EditorStyles.toolbarButton))
                    {
                        var newInspectorWindow = ScriptableObject.CreateInstance(s_InspectorWindowType) as EditorWindow;
                        s_SetObjectsLockedInfo.Invoke(newInspectorWindow, new object[] { Selection.objects.ToList() });
                        m_InspectorWindows.Add(newInspectorWindow);
                        m_SelectedTabIndex = m_InspectorWindows.Count - 1;
                    }
                }
                using (new EditorGUI.DisabledScope(m_SelectedTabIndex == 0))
                {
                    if (GUILayout.Button("-", EditorStyles.toolbarButton))
                    {
                        DestroyImmediate(m_InspectorWindows[m_SelectedTabIndex]);
                        Repaint();
                        return;
                    }
                }
            }

            using (var cellLayout = new EditorGUILayout.VerticalScope(GUILayout.Width(position.width)))
            {
                var window = m_InspectorWindows[m_SelectedTabIndex];
                if (Event.current.type == EventType.Repaint)
                {
                    window.minSize = Vector2.zero;
                    var posRect = cellLayout.rect;
                    posRect.height = position.height;
                    window.position = posRect;
                }
                s_OnGUIInfo.Invoke(window, new object[] { });
            }
        }

        string[] GetObjectNames()
        {
            var count = m_InspectorWindows.Count;
            var names = new string[count];
            var objects = new List<UnityEngine.Object>();
            names[0] = "Current";
            for (var i = 1; i < count; ++i)
            {
                s_GetObjectsLockedInfo.Invoke(m_InspectorWindows[i], new object[] { objects });
                names[i] = objects[0].name;
            }
            return names;
        }

        void Clear()
        {
            if (m_InspectorWindows != null)
            {
                foreach (var win in m_InspectorWindows)
                {
                    if (win != null)
                    {
                        DestroyImmediate(win);
                    }
                }
                m_InspectorWindows = null;
            }
        }
    }


}// namespace MomomaAssets
