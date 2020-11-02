using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MomomaAssets
{
    public class MeshRendererExplorer : EditorWindow
    {
        private int meshrendererNum;
        private int materialsNum;
        private int originalMaterialsNum;
        private int triangleNum;
        private int textureNum;
        private int textureSize;

        private int selectTab;
        private GameObject rootGO;
        private bool includeInactive;
        private SearchField searchField;
        private TreeView treeView;
        private const string searchStringStateKey = "MeshRendererTreeViewWindow_SearchString";
        private const string sortedColumnIndexStaticStateKey = "MeshRendererTreeViewWindow_Static_sortedColumnIndex";
        private const string sortedColumnIndexLightingStateKey = "MeshRendererTreeViewWindow_Lighting_sortedColumnIndex";
        private const string sortedColumnIndexLightmapStateKey = "MeshRendererTreeViewWindow_Lightmap_sortedColumnIndex";

        [MenuItem("MomomaTools/MeshRendererExplorer")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<MeshRendererExplorer>("MeshRendererExplorer");
        }

        public void OnHierarchyChange()
        {
            (treeView as IFullReloadTreeView)?.FullReload();
            CountReset();
        }

        private void InitializeStaticTab()
        {
            var header = new MultiColumnHeaderMaker<GameObjectTreeViewItem>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("Layer", 50, item => (LayerMask)item.m_Layer.intValue, (item, value) => item.m_Layer.intValue = (int)value, item => item.m_Layer);
            header.AddtoList("Lightmap", 30, item => item.LightmapStatic, (item, value) => item.LightmapStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.AddtoList("Occluder", 30, item => item.OccluderStatic, (item, value) => item.OccluderStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.AddtoList("Occludee", 30, item => item.OccludeeStatic, (item, value) => item.OccludeeStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.AddtoList("Batching", 30, item => item.BatchingStatic, (item, value) => item.BatchingStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.AddtoList("Navigation", 30, item => item.NavigationStatic, (item, value) => item.NavigationStatic = (bool)value, item => item.m_StaticEditorFlags);
            header.AddtoList("OffMeshLink", 30, item => item.OffMeshLinkGeneration, (item, value) => item.OffMeshLinkGeneration = (bool)value, item => item.m_StaticEditorFlags);
            header.AddtoList("Reflection", 30, item => item.ReflectionProbeStatic, (item, value) => item.ReflectionProbeStatic = (bool)value, item => item.m_StaticEditorFlags);
            treeView = new UnityObjectTreeView<GameObjectTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexStaticStateKey, () => GetMeshRenderers());
            treeView.searchString = SessionState.GetString(searchStringStateKey, "");
            searchField = new SearchField();
            searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
        }

        private void InitializeLightingTab()
        {
            var header = new MultiColumnHeaderMaker<MeshRendererTreeViewItem>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("LightProbe", 50, item => (LightProbeUsage)item.m_LightProbeUsage.intValue, (item, value) => item.m_LightProbeUsage.intValue = (int)value, item => item.m_LightProbeUsage);
            header.AddtoList("ReflectionProbe", 50, item => (ReflectionProbeUsage)item.m_ReflectionProbeUsage.intValue, (item, value) => item.m_ReflectionProbeUsage.intValue = (int)value, item => item.m_ReflectionProbeUsage);
            header.AddtoList("ProbeAnchor", 50, item => item.m_ProbeAnchor);
            header.AddtoList("CastShadows", 50, item => item.m_CastShadows);
            header.AddtoList("ReceiveShadows", 30, item => item.m_ReceiveShadows);
            treeView = new UnityObjectTreeView<MeshRendererTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexLightingStateKey, () => GetMeshRenderers());
            treeView.searchString = SessionState.GetString(searchStringStateKey, "");
            searchField = new SearchField();
            searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
        }

        private void InitializeLightmapTab()
        {
            var header = new MultiColumnHeaderMaker<MeshRendererTreeViewItem>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("ScaleInLightmap", 50, item => item.m_ScaleInLightmap);
            header.AddtoList("PrioritizeIllumination", 30, item => item.m_ImportantGI);
            header.AddtoList("StitchSeams", 30, item => item.m_StitchLightmapSeams);
            treeView = new UnityObjectTreeView<MeshRendererTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexLightmapStateKey, () => GetMeshRenderers(true));
            treeView.searchString = SessionState.GetString(searchStringStateKey, "");
            searchField = new SearchField();
            searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
        }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                selectTab = GUILayout.Toolbar(selectTab, new string[] { "Count", "Static", "Lighting", "Lightmap" }, GUILayout.MaxWidth(500));
                GUILayout.FlexibleSpace();
            }
            var changedTab = EditorGUI.EndChangeCheck();

            EditorGUI.BeginChangeCheck();
            rootGO = EditorGUILayout.ObjectField("Root GameObject", rootGO, typeof(GameObject), true) as GameObject;
            using (new EditorGUILayout.HorizontalScope())
            {
                includeInactive = GUILayout.Toggle(includeInactive, "Include Inactive GameObject");
                if (EditorGUI.EndChangeCheck())
                    OnHierarchyChange();
                if (selectTab > 0)
                {
                    switch (selectTab)
                    {
                        case 1:
                            if (changedTab || treeView == null)
                                InitializeStaticTab();
                            break;
                        case 2:
                            if (changedTab || treeView == null)
                                InitializeLightingTab();
                            break;
                        case 3:
                            if (changedTab || treeView == null)
                                InitializeLightmapTab();
                            break;
                        default:
                            throw new System.InvalidOperationException("column property is unknown type");
                    }
                    EditorGUI.BeginChangeCheck();
                    var searchString = searchField.OnToolbarGUI(treeView.searchString);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SessionState.SetString(searchStringStateKey, searchString);
                        treeView.searchString = searchString;
                    }
                }
            }

            // Count Mode
            if (selectTab == 0)
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
            }
            else
            {
                treeView.OnGUI(new Rect(20, 60, position.width - 40, position.height - 60));
            }
        }

        private List<MeshRenderer> GetMeshRenderers(bool isLightmapStatic = false)
        {
            var meshRendererList = new List<MeshRenderer>();
            if (rootGO)
            {
                if (includeInactive || rootGO.activeInHierarchy)
                {
                    meshRendererList.AddRange(rootGO.GetComponentsInChildren<MeshRenderer>());
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
                        if (includeInactive || rootObj.activeInHierarchy)
                            meshRendererList.AddRange(rootObj.GetComponentsInChildren<MeshRenderer>());
                    }

                }
            }
            if (!includeInactive)
                meshRendererList.RemoveAll(mr => !mr.enabled);
            if (isLightmapStatic)
                meshRendererList.RemoveAll(mr => 0 == (GameObjectUtility.GetStaticEditorFlags(mr.gameObject) & StaticEditorFlags.LightmapStatic));
            return meshRendererList;
        }

        private void CountReset()
        {
            meshrendererNum = 0;
            materialsNum = 0;
            originalMaterialsNum = 0;
            triangleNum = 0;
            textureNum = 0;
            textureSize = 0;
        }

        private void CountMesh()
        {
            CountReset();
            var originalMaterials = new HashSet<Material>();
            var originalTextures = new HashSet<Texture>();
            var meshRendererList = GetMeshRenderers();
            foreach (var mr in meshRendererList)
            {
                ++meshrendererNum;
                triangleNum += mr.GetComponent<MeshFilter>()?.sharedMesh.triangles.Length / 3 ?? 0;
                var materialArray = mr.sharedMaterials;
                materialsNum += materialArray.Length;
                foreach (var mat in materialArray)
                {
                    if (mat && originalMaterials.Add(mat))
                    {
                        var matSO = new SerializedObject(mat);
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
            originalMaterialsNum = originalMaterials.Count;
            textureNum = originalTextures.Count;
        }

        private void CopyCountToClipBoard()
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

            internal GameObjectTreeViewItem(int id, Object obj) : base(id, obj)
            {
                serializedObject = new SerializedObject(((MeshRenderer)obj).gameObject);
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

            internal MeshRendererTreeViewItem(int id, Object obj) : base(id, obj)
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
