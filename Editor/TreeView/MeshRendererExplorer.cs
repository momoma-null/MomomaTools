using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MomomaAssets
{

    class MeshRendererExplorer : EditorWindow
    {
        const string searchStringStateKey = "MeshRendererTreeViewWindow_SearchString";
        const string sortedColumnIndexStaticStateKey = "MeshRendererTreeViewWindow_Static_sortedColumnIndex";
        const string sortedColumnIndexLightingStateKey = "MeshRendererTreeViewWindow_Lighting_sortedColumnIndex";
        const string sortedColumnIndexLightmapStateKey = "MeshRendererTreeViewWindow_Lightmap_sortedColumnIndex";

        static readonly string[] s_TabNames = new string[] { "Count", "Static", "Lighting", "Lightmap" };

        int meshrendererNum;
        int materialsNum;
        int originalMaterialsNum;
        int triangleNum;
        int textureNum;
        int textureSize;

        int m_SelectedTabIndex;
        GameObject m_RootGameObject;
        bool m_IncludeInactive;
        SearchField m_SearchField;
        UnityObjectTreeViewBase m_TreeView;

        [MenuItem("MomomaTools/Mesh Renderer Explorer", false, 105)]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<MeshRendererExplorer>("Mesh Renderer Explorer");
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
                m_SelectedTabIndex = GUILayout.Toolbar(m_SelectedTabIndex, s_TabNames, "LargeButton", GUILayout.MaxWidth(500));
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
                        m_RootGameObject = EditorGUILayout.ObjectField("Root GameObject", m_RootGameObject, typeof(GameObject), true) as GameObject;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            m_IncludeInactive = GUILayout.Toggle(m_IncludeInactive, "Include Inactive GameObject");
                            if (check.changed)
                                OnHierarchyChange();
                            if (m_SelectedTabIndex > 0)
                            {
                                if (changedTab || m_TreeView == null)
                                {
                                    switch (m_SelectedTabIndex)
                                    {
                                        case 1:
                                            InitializeStaticTab();
                                            break;
                                        case 2:
                                            InitializeLightingTab();
                                            break;
                                        case 3:
                                            InitializeLightmapTab();
                                            break;
                                        default:
                                            throw new System.InvalidOperationException("tab number is invalid");
                                    }
                                }
                                EditorGUI.BeginChangeCheck();
                                var searchString = m_SearchField.OnToolbarGUI(m_TreeView.searchString);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    SessionState.SetString(searchStringStateKey, searchString);
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

        void InitializeStaticTab()
        {
            var header = new MultiColumnHeaderMaker<GameObjectTreeViewItem>();
            header.Add("Name", 200, item => item.displayName);
            header.Add("Layer", 80, item => (LayerMask)item.m_Layer.intValue, (item, value) => item.m_Layer.intValue = (int)value, item => item.m_Layer);
            header.Add("Lightmap", 50, item => item.LightmapStatic, (item, value) => item.LightmapStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.Add("Occluder", 50, item => item.OccluderStatic, (item, value) => item.OccluderStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.Add("Occludee", 50, item => item.OccludeeStatic, (item, value) => item.OccludeeStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.Add("Batching", 50, item => item.BatchingStatic, (item, value) => item.BatchingStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.Add("Navigation", 50, item => item.NavigationStatic, (item, value) => item.NavigationStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.Add("OffMeshLink", 50, item => item.OffMeshLinkGeneration, (item, value) => item.OffMeshLinkGeneration = (bool)value, item => item.m_StaticEditorFlags);
            header.Add("Reflection", 50, item => item.ReflectionProbeStatic, (item, value) => item.ReflectionProbeStatic = (bool)value, item => item.m_StaticEditorFlags);
            m_TreeView = new UnityObjectTreeView<GameObjectTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexStaticStateKey, () => GetTreeViewItems(isGameObject: true));
            m_TreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        }

        void InitializeLightingTab()
        {
            var header = new MultiColumnHeaderMaker<MeshRendererTreeViewItem>();
            header.Add("Name", 200, item => item.displayName);
            header.Add("LightProbe", 80, item => (LightProbeUsage)item.m_LightProbeUsage.intValue, (item, value) => item.m_LightProbeUsage.intValue = (int)value, item => item.m_LightProbeUsage);
            header.Add("ReflectionProbe", 80, item => (ReflectionProbeUsage)item.m_ReflectionProbeUsage.intValue, (item, value) => item.m_ReflectionProbeUsage.intValue = (int)value, item => item.m_ReflectionProbeUsage);
            header.Add("ProbeAnchor", 80, item => item.m_ProbeAnchor);
            header.Add("CastShadows", 60, item => item.m_CastShadows);
            header.Add("ReceiveShadows", 50, item => item.m_ReceiveShadows);
            m_TreeView = new UnityObjectTreeView<MeshRendererTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexLightingStateKey, () => GetTreeViewItems());
            m_TreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        }

        void InitializeLightmapTab()
        {
            var header = new MultiColumnHeaderMaker<MeshRendererTreeViewItem>();
            header.Add("Name", 200, item => item.displayName);
            header.Add("ScaleInLightmap", 60, item => item.m_ScaleInLightmap);
            header.Add("PrioritizeIllumination", 50, item => item.m_ImportantGI);
            header.Add("StitchSeams", 50, item => item.m_StitchLightmapSeams);
            m_TreeView = new UnityObjectTreeView<MeshRendererTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexLightmapStateKey, () => GetTreeViewItems(isLightmapStatic: true));
            m_TreeView.searchString = SessionState.GetString(searchStringStateKey, "");
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
            if (m_RootGameObject)
            {
                if (m_IncludeInactive || m_RootGameObject.activeInHierarchy)
                {
                    meshRenderers.UnionWith(m_RootGameObject.GetComponentsInChildren<MeshRenderer>());
                }
            }
            else
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.IsValid())
                {
                    var rootObjs = scene.GetRootGameObjects();
                    foreach (var rootObj in rootObjs)
                    {
                        if (m_IncludeInactive || rootObj.activeInHierarchy)
                            meshRenderers.UnionWith(rootObj.GetComponentsInChildren<MeshRenderer>());
                    }

                }
            }
            if (!m_IncludeInactive)
                meshRenderers.RemoveWhere(mr => !mr.enabled);
            if (isLightmapStatic)
                meshRenderers.RemoveWhere(mr => !GameObjectUtility.AreStaticEditorFlagsSet(mr.gameObject, StaticEditorFlags.LightmapStatic));
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
            var meshRendererList = GetMeshRenderers();
            foreach (var mr in meshRendererList)
            {
                triangleNum += mr.GetComponent<MeshFilter>()?.sharedMesh?.triangles.Length / 3 ?? 0;
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
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.LightmapStatic); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.LightmapStatic); }
            }
            internal bool OccluderStatic
            {
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.OccluderStatic); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.OccluderStatic); }
            }
            internal bool OccludeeStatic
            {
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.OccludeeStatic); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.OccludeeStatic); }
            }
            internal bool BatchingStatic
            {
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.BatchingStatic); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.BatchingStatic); }
            }
            internal bool NavigationStatic
            {
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.NavigationStatic); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.NavigationStatic); }
            }
            internal bool OffMeshLinkGeneration
            {
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.OffMeshLinkGeneration); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.OffMeshLinkGeneration); }
            }
            internal bool ReflectionProbeStatic
            {
                get { return 0 < ((StaticEditorFlags)m_StaticEditorFlags.intValue & StaticEditorFlags.ReflectionProbeStatic); }
                set { SetStaticFlag(value, (int)StaticEditorFlags.ReflectionProbeStatic); }
            }

            readonly internal SerializedProperty m_Layer;
            readonly internal SerializedProperty m_StaticEditorFlags;

            internal GameObjectTreeViewItem(int id, GameObject obj) : base(id)
            {
                serializedObject = new SerializedObject(obj);
                displayName = obj.name;

                m_Layer = serializedObject.FindProperty("m_Layer");
                m_StaticEditorFlags = serializedObject.FindProperty("m_StaticEditorFlags");
            }

            void SetStaticFlag(bool active, int targetFlag)
            {
                var flags = m_StaticEditorFlags.intValue;
                if (flags < 0)
                {
                    var allPossibleValues = 0;
                    var values = System.Enum.GetValues(typeof(StaticEditorFlags));
                    foreach (var value in values)
                    {
                        allPossibleValues |= (int)value;
                    }
                    flags = flags & allPossibleValues;
                }
                flags = active ? (flags | targetFlag) : (flags & ~targetFlag);
                m_StaticEditorFlags.intValue = flags;
            }
        }

        class MeshRendererTreeViewItem : UnityObjectTreeViewItem
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

            internal MeshRendererTreeViewItem(int id, MeshRenderer obj) : base(id)
            {
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
            }
        }
    }

}// namespace MomomaAssets
