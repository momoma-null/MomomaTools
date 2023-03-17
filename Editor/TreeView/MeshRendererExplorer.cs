using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MomomaAssets
{
    class MeshRendererExplorer : EditorWindow
    {
        static class Styles
        {
            public static GUIStyle largeButton = "LargeButton";
            public static GUIContent rootGameObject = EditorGUIUtility.TrTextContent("Root GameObject");
            public static GUIContent includeInactiveGameObject = EditorGUIUtility.TrTextContent("Include Inactive GameObject");
        }

        static readonly string[] s_TabNames = new string[] { "Count", "Static", "Lighting", "Lightmap" };

        int meshrendererNum;
        int materialsNum;
        int originalMaterialsNum;
        int triangleNum;
        int textureNum;
        int textureSize;

        [SerializeField]
        int m_SelectedTabIndex;
        [SerializeField]
        GameObject m_RootGameObject;
        [SerializeField]
        bool m_IncludeInactive;
        [SerializeField]
        TreeViewState m_ViewState = new TreeViewState();
        [SerializeField]
        MultiColumnHeaderState[] multiColumnHeaderStates = new MultiColumnHeaderState[]
        {
            GetStaticViewState(),
            GetLightingViewState(),
            GetLightmapViewState()
        };

        SearchField m_SearchField;
        UnityObjectTreeViewBase m_TreeView;

        [MenuItem("MomomaTools/Mesh Renderer Explorer", false, 105)]
        static void ShowWindow()
        {
            GetWindow<MeshRendererExplorer>(ObjectNames.NicifyVariableName(nameof(MeshRendererExplorer)));
        }

        void OnEnable()
        {
            minSize = new Vector2(500f, 250f);
            m_SearchField = new SearchField();
        }

        void OnHierarchyChange()
        {
            m_TreeView?.OnHierarchyChange();
            CountReset();
            Repaint();
        }

        void OnSelectionChange()
        {
            m_TreeView?.SetSelection(Selection.instanceIDs);
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                m_SelectedTabIndex = GUILayout.Toolbar(m_SelectedTabIndex, s_TabNames, Styles.largeButton, GUILayout.MaxWidth(500));
                GUILayout.FlexibleSpace();
            }
            var changedTab = EditorGUI.EndChangeCheck();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.VerticalScope())
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        m_RootGameObject = EditorGUILayout.ObjectField(Styles.rootGameObject, m_RootGameObject, typeof(GameObject), true) as GameObject;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            m_IncludeInactive = GUILayout.Toggle(m_IncludeInactive, Styles.includeInactiveGameObject);
                            if (check.changed)
                                OnHierarchyChange();
                            if (m_SelectedTabIndex > 0)
                            {
                                if (changedTab || m_TreeView == null)
                                {
                                    m_TreeView = GetCurrentTreeView();
                                }
                                EditorGUI.BeginChangeCheck();
                                var searchString = m_SearchField.OnToolbarGUI(m_TreeView.searchString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    m_TreeView.searchString = searchString;
                                }
                            }
                        }
                    }
                    if (m_SelectedTabIndex == 0)
                    {
                        if (GUILayout.Button("Copy to Clipboard", GUILayout.ExpandWidth(false)))
                        {
                            CopyCountToClipBoard();
                        }

                        if (GUILayout.Button("Count", GUILayout.Height(36f), GUILayout.Width(108f)))
                        {
                            CountMesh();
                        }

                        EditorGUILayout.LabelField("Mesh Renderer", meshrendererNum.ToString());
                        EditorGUILayout.LabelField("Materials", materialsNum.ToString());
                        EditorGUILayout.LabelField("Original Materials", originalMaterialsNum.ToString());
                        EditorGUILayout.LabelField("Triangles", triangleNum.ToString());
                        EditorGUILayout.LabelField("Textures", textureNum.ToString());
                        EditorGUILayout.LabelField("TextureSize", "1024 *1024 * " + (textureSize / 1024f).ToString("N"));
                        GUILayoutUtility.GetRect(0, float.MaxValue, 0, float.MaxValue);
                    }
                    else
                    {
                        var rect = GUILayoutUtility.GetRect(0, float.MaxValue, 0, float.MaxValue);
                        m_TreeView.OnGUI(rect);
                    }
                }
                EditorGUILayout.Space();
            }
            EditorGUILayout.Space();
        }

        UnityObjectTreeViewBase GetCurrentTreeView()
        {
            switch (m_SelectedTabIndex)
            {
                case 1:
                    return new UnityObjectTreeView<GameObjectTreeViewItem>(m_ViewState, multiColumnHeaderStates[0], () => GetTreeViewItems(isGameObject: true));
                case 2:
                    return new UnityObjectTreeView<MeshRendererTreeViewItem>(m_ViewState, multiColumnHeaderStates[1], () => GetTreeViewItems());
                case 3:
                    return new UnityObjectTreeView<MeshRendererTreeViewItem>(m_ViewState, multiColumnHeaderStates[2], () => GetTreeViewItems(isLightmapStatic: true));
                default: throw new InvalidOperationException("tab number is invalid");
            }
        }

        static MultiColumnHeaderState GetStaticViewState()
        {
            var columns = new ColumnArray<GameObjectTreeViewItem>();
            columns.Add("Name", 200, item => item.displayName);
            columns.AddIntAsLayerMask("Layer", 80, item => item.m_Layer);
            columns.Add("GI", 50, item => item.LightmapStatic, (r, item) => item.DrawProperty(r, StaticEditorFlags.ContributeGI), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.ContributeGI, from));
            columns.Add("Occluder", 50, item => item.OccluderStatic, (r, item) => item.DrawProperty(r, StaticEditorFlags.OccluderStatic), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.OccluderStatic, from));
            columns.Add("Occludee", 50, item => item.OccludeeStatic, (r, item) => item.DrawProperty(r, StaticEditorFlags.OccludeeStatic), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.OccludeeStatic, from));
            columns.Add("Batching", 50, item => item.BatchingStatic, (r, item) => item.DrawProperty(r, StaticEditorFlags.BatchingStatic), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.BatchingStatic, from));
            columns.Add("Navigation", 50, item => item.NavigationStatic, (r, item) => item.DrawProperty(r, StaticEditorFlags.NavigationStatic), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.NavigationStatic, from));
            columns.Add("OffMeshLink", 50, item => item.OffMeshLinkGeneration, (r, item) => item.DrawProperty(r, StaticEditorFlags.OffMeshLinkGeneration), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.OffMeshLinkGeneration, from));
            columns.Add("Reflection", 50, item => item.ReflectionProbeStatic, (r, item) => item.DrawProperty(r, StaticEditorFlags.ReflectionProbeStatic), item => item.m_StaticEditorFlags, (from, to) => to.CopyFrom(StaticEditorFlags.ReflectionProbeStatic, from));
            return columns.GetHeaderState();
        }

        static MultiColumnHeaderState GetLightingViewState()
        {
            var columns = new ColumnArray<MeshRendererTreeViewItem>();
            columns.Add("Name", 200, item => item.displayName);
            columns.AddIntAsEnum("LightProbe", 80, item => (LightProbeUsage)item.m_LightProbeUsage.intValue, item => item.m_LightProbeUsage);
            columns.AddIntAsEnum("ReflectionProbe", 80, item => (ReflectionProbeUsage)item.m_ReflectionProbeUsage.intValue, item => item.m_ReflectionProbeUsage);
            columns.Add("ProbeAnchor", 80, item => item.m_ProbeAnchor.objectReferenceValue, item => item.m_ProbeAnchor);
            columns.Add("CastShadows", 60, item => item.m_CastShadows.enumValueIndex, item => item.m_CastShadows);
            columns.Add("ReceiveShadows", 50, item => item.m_ReceiveShadows.boolValue, item => item.m_ReceiveShadows);
            return columns.GetHeaderState();
        }

        static MultiColumnHeaderState GetLightmapViewState()
        {
            var columns = new ColumnArray<MeshRendererTreeViewItem>();
            columns.Add("Name", 200, item => item.displayName);
            columns.Add("ScaleInLightmap", 60, item => item.m_ScaleInLightmap.floatValue, item => item.m_ScaleInLightmap);
            columns.Add("PrioritizeIllumination", 50, item => item.m_ImportantGI.boolValue, item => item.m_ImportantGI);
            columns.Add("StitchSeams", 50, item => item.m_StitchLightmapSeams.boolValue, item => item.m_StitchLightmapSeams);
#if UNITY_2019_1_OR_NEWER
            columns.AddIntAsEnum("Receive GI", 80, item => (ReceiveGI)item.m_ReceiveGI.intValue, item => item.m_ReceiveGI);
#endif
            columns.Add("Lightmap Index", 80, item => item.LightmapIndex);
            return columns.GetHeaderState();
        }

        IEnumerable<UnityObjectTreeViewItem> GetTreeViewItems(bool isGameObject = false, bool isLightmapStatic = false)
        {
            var mrs = GetMeshRenderers(isLightmapStatic);
            if (isGameObject)
                return mrs.Select(mr => new GameObjectTreeViewItem(mr.gameObject.GetInstanceID(), mr.gameObject)).ToArray();
            else
                return mrs.Select(mr => new MeshRendererTreeViewItem(mr.gameObject.GetInstanceID(), mr)).ToArray();
        }

        ICollection<MeshRenderer> GetMeshRenderers(bool isLightmapStatic = false)
        {
            var meshRenderers = new HashSet<MeshRenderer>();
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var rootObjs = new GameObject[] { prefabStage?.prefabContentsRoot ?? m_RootGameObject };
            if (rootObjs[0] == null)
                rootObjs = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var o in rootObjs)
            {
                if (m_IncludeInactive || o.activeInHierarchy)
                    meshRenderers.UnionWith(o.GetComponentsInChildren<MeshRenderer>());
            }
            if (!m_IncludeInactive)
                meshRenderers.RemoveWhere(mr => !mr.enabled);
            if (isLightmapStatic)
                meshRenderers.RemoveWhere(mr => !GameObjectUtility.AreStaticEditorFlagsSet(mr.gameObject, StaticEditorFlags.ContributeGI));
            return meshRenderers;
        }

        void CountReset()
        {
            meshrendererNum = 0;
            materialsNum = 0;
            originalMaterialsNum = 0;
            triangleNum = 0;
            textureNum = 0;
            textureSize = 0;
        }

        void CountMesh()
        {
            CountReset();
            var originalMaterials = new HashSet<Material>();
            var originalTextures = new HashSet<Texture>();
            var batchedMeshes = new HashSet<Mesh>();
            var meshRendererList = GetMeshRenderers();
            foreach (var mr in meshRendererList)
            {
                var filter = mr.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    var mesh = filter.sharedMesh;
                    if (mesh != null && (!mr.isPartOfStaticBatch || batchedMeshes.Add(mesh)))
                    {
                        for (var i = 0; i < mesh.subMeshCount; ++i)
                        {
                            var subMesh = mesh.GetSubMesh(i);
                            if (subMesh.topology == MeshTopology.Triangles)
                                triangleNum += subMesh.indexCount / 3;
                        }
                    }
                }
                var materialArray = mr.sharedMaterials;
                materialsNum += materialArray.Length;
                foreach (var mat in materialArray)
                {
                    if (mat && originalMaterials.Add(mat))
                    {
                        using (var matSO = new SerializedObject(mat))
                        {
                            var m_TexEnvs = matSO.FindProperty("m_SavedProperties.m_TexEnvs");
                            for (var k = 0; k < m_TexEnvs.arraySize; ++k)
                            {
                                var tex = m_TexEnvs.GetArrayElementAtIndex(k).FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                                if (tex && originalTextures.Add(tex))
                                {
                                    textureSize += tex.height / 32 * tex.width / 32;
                                }
                            }
                        }
                    }
                }
            }
            meshrendererNum = meshRendererList.Count;
            originalMaterialsNum = originalMaterials.Count;
            textureNum = originalTextures.Count;
        }

        void CopyCountToClipBoard()
        {
            EditorGUIUtility.systemCopyBuffer =
            meshrendererNum.ToString() + "," +
            materialsNum.ToString() + "," +
            originalMaterialsNum.ToString() + "," +
            triangleNum.ToString() + "," +
            textureNum.ToString() + "," +
            (textureSize / 1024f).ToString();
        }

        class GameObjectTreeViewItem : UnityObjectTreeViewItem
        {
            override public SerializedObject serializedObject { get; }

            internal bool LightmapStatic
            {
                get => HasFlag(StaticEditorFlags.ContributeGI);
                set => SetFlag(value, StaticEditorFlags.ContributeGI);
            }
            internal bool OccluderStatic
            {
                get => HasFlag(StaticEditorFlags.OccluderStatic);
                set => SetFlag(value, StaticEditorFlags.OccluderStatic);
            }
            internal bool OccludeeStatic
            {
                get => HasFlag(StaticEditorFlags.OccludeeStatic);
                set => SetFlag(value, StaticEditorFlags.OccludeeStatic);
            }
            internal bool BatchingStatic
            {
                get => HasFlag(StaticEditorFlags.BatchingStatic);
                set => SetFlag(value, StaticEditorFlags.BatchingStatic);
            }
            internal bool NavigationStatic
            {
                get => HasFlag(StaticEditorFlags.NavigationStatic);
                set => SetFlag(value, StaticEditorFlags.NavigationStatic);
            }
            internal bool OffMeshLinkGeneration
            {
                get => HasFlag(StaticEditorFlags.OffMeshLinkGeneration);
                set => SetFlag(value, StaticEditorFlags.OffMeshLinkGeneration);
            }
            internal bool ReflectionProbeStatic
            {
                get => HasFlag(StaticEditorFlags.ReflectionProbeStatic);
                set => SetFlag(value, StaticEditorFlags.ReflectionProbeStatic);
            }

            static readonly int maxValue = Enum.GetValues(typeof(StaticEditorFlags)).Cast<int>().Aggregate((x, y) => x | y);

            readonly internal SerializedProperty m_Layer;
            readonly internal SerializedProperty m_StaticEditorFlags;

            internal GameObjectTreeViewItem(int id, GameObject obj) : base(id)
            {
                serializedObject = new SerializedObject(obj);
                displayName = obj.name;

                m_Layer = serializedObject.FindProperty("m_Layer");
                m_StaticEditorFlags = serializedObject.FindProperty("m_StaticEditorFlags");
            }

            public void DrawProperty(Rect rect, StaticEditorFlags flag)
            {
                using (new EditorGUI.PropertyScope(rect, GUIContent.none, m_StaticEditorFlags))
                {
                    var value = HasFlag(flag);
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        value = EditorGUI.Toggle(rect, value);
                        if (check.changed)
                            SetFlag(value, flag);
                    }
                }
            }

            public bool CopyFrom(StaticEditorFlags flag, SerializedProperty src)
            {
                if (src.intValue == m_StaticEditorFlags.intValue)
                    return false;
                SetFlag((((StaticEditorFlags)src.intValue & flag) != 0), flag);
                return true;
            }

            bool HasFlag(StaticEditorFlags flag)
                 => ((StaticEditorFlags)m_StaticEditorFlags.intValue & flag) != 0;

            void SetFlag(bool active, StaticEditorFlags flag)
            {
                var flagInt = (int)flag;
                var flags = m_StaticEditorFlags.intValue;
                if (flags < 0)
                    flags &= maxValue;
                flags = active ? (flags | flagInt) : (flags & ~flagInt);
                m_StaticEditorFlags.intValue = flags;
            }
        }

        sealed class MeshRendererTreeViewItem : UnityObjectTreeViewItem
        {
            override public SerializedObject serializedObject { get; }

            readonly internal SerializedProperty m_LightProbeUsage;
            readonly internal SerializedProperty m_ReflectionProbeUsage;
            readonly internal SerializedProperty m_ProbeAnchor;
            readonly internal SerializedProperty m_CastShadows;
            readonly internal SerializedProperty m_ReceiveShadows;
            readonly internal SerializedProperty m_ScaleInLightmap;
            readonly internal SerializedProperty m_ImportantGI;
            readonly internal SerializedProperty m_StitchLightmapSeams;
#if UNITY_2019_1_OR_NEWER
            readonly internal SerializedProperty m_ReceiveGI;
#endif
            readonly MeshRenderer m_MeshRenderer;

            internal int LightmapIndex => m_MeshRenderer.lightmapIndex;

            internal MeshRendererTreeViewItem(int id, MeshRenderer obj) : base(id)
            {
                m_MeshRenderer = obj;
                serializedObject = new SerializedObject(obj);
                displayName = obj.name;

                m_LightProbeUsage = serializedObject.FindProperty("m_LightProbeUsage");
                m_ReflectionProbeUsage = serializedObject.FindProperty("m_ReflectionProbeUsage");
                m_ProbeAnchor = serializedObject.FindProperty("m_ProbeAnchor");
                m_CastShadows = serializedObject.FindProperty("m_CastShadows");
                m_ReceiveShadows = serializedObject.FindProperty("m_ReceiveShadows");
                m_ScaleInLightmap = serializedObject.FindProperty("m_ScaleInLightmap");
                m_ImportantGI = serializedObject.FindProperty("m_ImportantGI");
                m_StitchLightmapSeams = serializedObject.FindProperty("m_StitchLightmapSeams");
#if UNITY_2019_1_OR_NEWER
                m_ReceiveGI = serializedObject.FindProperty("m_ReceiveGI");
#endif
            }
        }
    }
}// namespace MomomaAssets
