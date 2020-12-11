using System;
using System.Collections;
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
        static readonly FieldInfo s_TrackerInfo = s_InspectorWindowType.GetField("m_Tracker", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo s_ParentInfo = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly PropertyInfo s_actualViewInfo = s_ParentInfo.FieldType.GetProperty("actualView", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly Type s_CustomEditorAttributesType = Type.GetType("UnityEditor.CustomEditorAttributes, UnityEditor.dll");
        static readonly IDictionary s_kSCustomEditors = s_CustomEditorAttributesType.GetField("kSCustomEditors", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as IDictionary;
        static readonly IDictionary s_kSCustomMultiEditors = s_CustomEditorAttributesType.GetField("kSCustomMultiEditors", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null) as IDictionary;
        static readonly Type s_MonoEditorTypeType = s_CustomEditorAttributesType.GetNestedType("MonoEditorType", BindingFlags.NonPublic);
        static readonly FieldInfo[] s_MonoEditorTypeInfos = s_MonoEditorTypeType.GetFields();
        static readonly MethodInfo s_MemberwiseCloneInfo = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly Dictionary<string, MethodInfo> s_MethodInfos = new Dictionary<string, MethodInfo>();

        static GUIContent s_PrevArrow;
        static GUIContent s_NextArrow;
        static bool s_IsDeveloperMode = false;

        List<EditorWindow> m_InspectorWindows;
        int m_SelectedTabIndex;
        Vector2 m_ScrollPos;
        bool m_DoRebuild;
        object m_Parent;

        object parent => m_Parent ?? (m_Parent = s_ParentInfo.GetValue(this));

        [MenuItem("MomomaTools/ExtendedInspector")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<ExtendedInspector>("ExtendedInspector");
        }

        static MethodInfo GetMethodInfo(string methodName)
        {
            MethodInfo info;
            if (s_MethodInfos.TryGetValue(methodName, out info))
            {
                return info;
            }
            info = s_InspectorWindowType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            s_MethodInfos.Add(methodName, info);
            return info;
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
            s_actualViewInfo.SetValue(parent, this);
            Clear();
        }

        void OnGUI()
        {
            var removingWindows = m_InspectorWindows.Where((win, i) => win != null && i != 0 && !(s_TrackerInfo.GetValue(win) as ActiveEditorTracker).isLocked).ToArray();
            foreach (var win in removingWindows)
            {
                DestroyImmediate(win);
            }
            m_InspectorWindows.RemoveAll(win => !win);
            if (m_InspectorWindows.Count < 1)
            {
                m_InspectorWindows.Add(ScriptableObject.CreateInstance(s_InspectorWindowType) as EditorWindow);
                s_ParentInfo.SetValue(m_InspectorWindows[0], parent);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                m_SelectedTabIndex = Mathf.Clamp(m_SelectedTabIndex, 0, m_InspectorWindows.Count - 1);
                var names = GetObjectNames();
                var scrollPosMax = 0f;
                using (var horizontal = new EditorGUILayout.HorizontalScope())
                using (new EditorGUILayout.ScrollViewScope(m_ScrollPos, GUIStyle.none, GUIStyle.none))
                using (var innerHorizontal = new EditorGUILayout.HorizontalScope())
                {
                    scrollPosMax = innerHorizontal.rect.width - horizontal.rect.width;
                    EditorGUI.BeginChangeCheck();
                    var newSelectedTabIndex = GUILayout.Toolbar(m_SelectedTabIndex, names, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_SelectedTabIndex = newSelectedTabIndex;
                    }
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
                        GetMethodInfo("SetObjectsLocked").Invoke(newInspectorWindow, new object[] { Selection.objects.ToList() });
                        s_ParentInfo.SetValue(newInspectorWindow, parent);
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
                EditorGUI.BeginChangeCheck();
                s_IsDeveloperMode = GUILayout.Toggle(s_IsDeveloperMode, "Developer", EditorStyles.toolbarButton);
                m_DoRebuild |= EditorGUI.EndChangeCheck();
            }

            DoRebuild();

            using (var cellLayout = new EditorGUILayout.VerticalScope(GUILayout.Width(position.width)))
            {
                var window = m_InspectorWindows[m_SelectedTabIndex];
                s_actualViewInfo.SetValue(parent, window);
                if (Event.current.type == EventType.Repaint)
                {
                    s_ParentInfo.SetValue(window, null);
                    window.minSize = Vector2.zero;
                    var posRect = cellLayout.rect;
                    posRect.height = position.height;
                    window.position = posRect;
                    s_ParentInfo.SetValue(window, parent);
                }
                GetMethodInfo("OnGUI").Invoke(window, new object[] { });
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
                GetMethodInfo("GetObjectsLocked").Invoke(m_InspectorWindows[i], new object[] { objects });
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

        void DoRebuild()
        {
            if (s_IsDeveloperMode || m_DoRebuild)
            {
                try
                {
                    if (s_IsDeveloperMode)
                        m_DoRebuild = false;
                    foreach (var win in m_InspectorWindows)
                    {
                        var tracker = s_TrackerInfo.GetValue(win) as ActiveEditorTracker;
                        if (s_IsDeveloperMode)
                        {
                            var editors = tracker.activeEditors;
                            if (Array.Exists(editors, e => (e is ExtendedEditor)))
                                continue;
                            m_DoRebuild = true;
                            for (var i = 0; i < editors.Length; ++i)
                            {
                                var targetType = editors[i].target.GetType();
                                ReplaceEditor(targetType, s_kSCustomEditors);
                                ReplaceEditor(targetType, s_kSCustomMultiEditors);
                            }
                        }
                        tracker.ForceRebuild();
                    }
                }
                finally
                {
                    if (s_IsDeveloperMode && m_DoRebuild)
                    {
                        ResetEditor(s_kSCustomEditors);
                        ResetEditor(s_kSCustomMultiEditors);
                    }
                    m_DoRebuild = false;
                }
            }
        }

        static void ReplaceEditor(Type targetType, IDictionary dictionary)
        {
            if (dictionary.Contains(targetType))
            {
                var monoEditorTypes = dictionary[targetType] as IList;
                var extendedEditorIndex = -1;
                for (var j = 0; j < monoEditorTypes.Count; ++j)
                {
                    if (s_MonoEditorTypeInfos[1].GetValue(monoEditorTypes[j]) as Type == typeof(ExtendedEditor))
                    {
                        extendedEditorIndex = j;
                        break;
                    }
                }
                if (extendedEditorIndex < 0)
                {
                    var newMonoEditorType = s_MemberwiseCloneInfo.Invoke(monoEditorTypes[0], new object[] { });
                    s_MonoEditorTypeInfos[1].SetValue(newMonoEditorType, typeof(ExtendedEditor));
                    monoEditorTypes.Insert(0, newMonoEditorType);
                }
                else if (extendedEditorIndex > 0)
                {
                    var extended = monoEditorTypes[extendedEditorIndex];
                    monoEditorTypes.RemoveAt(extendedEditorIndex);
                    monoEditorTypes.Insert(0, extended);
                }
            }
            else
            {
                var enumerator = dictionary.GetEnumerator();
                enumerator.MoveNext();
                var sampleList = enumerator.Value as IList;
                var newList = s_MemberwiseCloneInfo.Invoke(sampleList, new object[] { }) as IList;
                newList.Clear();
                newList.Add(Activator.CreateInstance(s_MonoEditorTypeType));
                s_MonoEditorTypeInfos[0].SetValue(newList[0], targetType);
                s_MonoEditorTypeInfos[1].SetValue(newList[0], typeof(ExtendedEditor));
                dictionary[targetType] = newList;
            }
        }

        static void ResetEditor(IDictionary dictionary)
        {
            var removingKeys = new List<object>();
            foreach (DictionaryEntry entry in dictionary)
            {
                var val = entry.Value as IList;
                if (val == null || val.Count == 0)
                    continue;
                if (s_MonoEditorTypeInfos[1].GetValue(val[0]) as Type == typeof(ExtendedEditor))
                {
                    if (val.Count == 1)
                    {
                        removingKeys.Add(entry.Key);
                    }
                    else
                    {
                        val.RemoveAt(0);
                    }
                }
            }
            foreach (var key in removingKeys)
            {
                dictionary.Remove(key);
            }
        }

        class ExtendedEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                var isInitial = true;
                using (var sp = serializedObject.GetIterator())
                {
                    while (true)
                    {
                        using (var copy = sp.Copy())
                        {
                            if (!sp.Next(isInitial))
                                break;
                            copy.NextVisible(isInitial);
                            using (new EditorGUI.DisabledScope(!SerializedProperty.EqualContents(sp, copy)))
                            {
                                PropertyFieldRecursive(sp);
                            }
                        }
                        isInitial = false;
                    }
                }
                serializedObject.ApplyModifiedProperties();
            }

            void PropertyFieldRecursive(SerializedProperty sp)
            {
                if (sp.propertyType == SerializedPropertyType.Generic)
                {
                    if (sp.hasChildren)
                    {
                        sp.isExpanded = EditorGUILayout.Foldout(sp.isExpanded, sp.displayName, true);
                        if (sp.isExpanded)
                        {
                            using (var child = sp.Copy())
                            using (var end = child.GetEndProperty(true))
                            {
                                if (child.Next(true))
                                {
                                    using (new EditorGUI.IndentLevelScope(1))
                                    using (new EditorGUI.DisabledScope(!sp.hasVisibleChildren))
                                    {
                                        if (sp.isArray)
                                        {
                                            while(child.propertyType != SerializedPropertyType.ArraySize)
                                            {
                                                child.Next(true);
                                            }
                                            EditorGUILayout.PropertyField(child);
                                            child.Next(false);
                                        }
                                        var count = 0;
                                        while (!SerializedProperty.EqualContents(child, end))
                                        {
                                            PropertyFieldRecursive(child);
                                            if (!child.Next(false))
                                                break;
                                            if (++count > 100)
                                            {
                                                EditorGUILayout.HelpBox("The 100th and subsequent elements are omitted.", MessageType.Info);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(sp);
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(!sp.editable))
                    {
                        EditorGUILayout.PropertyField(sp, true);
                    }
                }
            }
        }
    }

}// namespace MomomaAssets
