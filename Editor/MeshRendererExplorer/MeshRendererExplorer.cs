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
        private GameObject countRootObj;
        private bool includeDeactive;

        private GameObject rootGO;
        private SearchField searchField;
        private TreeView treeView;
        private const string searchStringStateKey = "MeshRendererTreeViewWindow_SearchString";
        private const string sortedColumnIndexStaticStateKey = "MeshRendererTreeViewWindow_Static_sortedColumnIndex";
        private const string sortedColumnIndexLightingStateKey = "MeshRendererTreeViewWindow_Lighting_sortedColumnIndex";
        private const string sortedColumnIndexLightmapStateKey = "MeshRendererTreeViewWindow_Lightmap_sortedColumnIndex";


        // Generate menu tab
        [MenuItem("MomomaTools/MeshRendererExplorer")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<MeshRendererExplorer>("MeshRendererExplorer");
        }

        public void OnHierarchyChange()
        {
            if (treeView != null)
                ((IFullReloadTreeView)treeView).FullReload();
            SetMeshRendererList(countRootObj);
            CountReset();
        }

        private void InitializeStaticTab()
        {
            var header = new MultiColumnHeaderMaker<GameObjectTreeViewItem, GameObjectElemnt>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("Layer", 50, item => (LayerMask)item.element.m_Layer.intValue, (item, value) => item.element.m_Layer.intValue = (int)value, item => item.element.m_Layer);
            header.AddtoList("Lightmap", 30, item => item.element.LightmapStatic, (item, value) => item.element.LightmapStatic = (bool)value, item => item.element.m_StaticEditorFlags);
            header.AddtoList("Occluder", 30, item => item.element.OccluderStatic, (item, value) => item.element.OccluderStatic = (bool)value, item => item.element.m_StaticEditorFlags);
            header.AddtoList("Occludee", 30, item => item.element.OccludeeStatic, (item, value) => item.element.OccludeeStatic = (bool)value, item => item.element.m_StaticEditorFlags);
            header.AddtoList("Batching", 30, item => item.element.BatchingStatic, (item, value) => item.element.BatchingStatic = (bool)value, item => item.element.m_StaticEditorFlags);
            header.AddtoList("Navigation", 30, item => item.element.NavigationStatic, (item, value) => item.element.NavigationStatic = (bool)value, item => item.element.m_StaticEditorFlags);
            header.AddtoList("OffMeshLink", 30, item => item.element.OffMeshLinkGeneration, (item, value) => item.element.OffMeshLinkGeneration = (bool)value, item => item.element.m_StaticEditorFlags);
            header.AddtoList("Reflection", 30, item => item.element.ReflectionProbeStatic, (item, value) => item.element.ReflectionProbeStatic = (bool)value, item => item.element.m_StaticEditorFlags);
            treeView = new UnityObjectTreeView<GameObjectTreeViewItem, GameObjectElemnt>(new TreeViewState(), header.GetHeader(), sortedColumnIndexStaticStateKey, () => GetMeshRenderers(false));
            treeView.searchString = SessionState.GetString(searchStringStateKey, "");
            searchField = new SearchField();
            searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
        }

        private void InitializeLightingTab()
        {
            var header = new MultiColumnHeaderMaker<MeshRendererTreeViewItem, MeshRendererElement>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("LightProbe", 50, item => (LightProbeUsage)item.element.m_LightProbeUsage.intValue, (item, value) => item.element.m_LightProbeUsage.intValue = (int)value, item => item.element.m_LightProbeUsage);
            header.AddtoList("ReflectionProbe", 50, item => (ReflectionProbeUsage)item.element.m_ReflectionProbeUsage.intValue, (item, value) => item.element.m_ReflectionProbeUsage.intValue = (int)value, item => item.element.m_ReflectionProbeUsage);
            header.AddtoList("ProbeAnchor", 50, item => item.element.m_ProbeAnchor);
            header.AddtoList("CastShadows", 50, item => item.element.m_CastShadows);
            header.AddtoList("ReceiveShadows", 30, item => item.element.m_ReceiveShadows);
            treeView = new UnityObjectTreeView<MeshRendererTreeViewItem, MeshRendererElement>(new TreeViewState(), header.GetHeader(), sortedColumnIndexLightingStateKey, () => GetMeshRenderers(false));
            treeView.searchString = SessionState.GetString(searchStringStateKey, "");
            searchField = new SearchField();
            searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
        }

        private void InitializeLightmapTab()
        {
            var header = new MultiColumnHeaderMaker<MeshRendererTreeViewItem, MeshRendererElement>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("ScaleInLightmap", 50, item => item.element.m_ScaleInLightmap);
            header.AddtoList("PrioritizeIllumination", 30, item => item.element.m_ImportantGI);
            header.AddtoList("StitchSeams", 30, item => item.element.m_StitchLightmapSeams);
            treeView = new UnityObjectTreeView<MeshRendererTreeViewItem, MeshRendererElement>(new TreeViewState(), header.GetHeader(), sortedColumnIndexLightmapStateKey, () => GetMeshRenderers(true));
            treeView.searchString = SessionState.GetString(searchStringStateKey, "");
            searchField = new SearchField();
            searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            selectTab = GUILayout.Toolbar(selectTab, new string[] { "Count", "Static", "Lighting", "Lightmap" }, GUILayout.MaxWidth(500));
            bool changedTab = EditorGUI.EndChangeCheck();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Count Mode
            if (selectTab == 0)
            {
                EditorGUI.BeginChangeCheck();

                // root object
                countRootObj = EditorGUILayout.ObjectField("Root Object", countRootObj, typeof(GameObject), true) as GameObject;
                // option
                includeDeactive = GUILayout.Toggle(includeDeactive, "Include Deactive Object");

                if (EditorGUI.EndChangeCheck())
                {
                    CountReset();
                }

                // copy to clipboard button
                if (GUILayout.Button("Copy to Clipboard", GUILayout.ExpandWidth(false)))
                {
                    CopyCountToClipBoard();
                }

                // count button
                if (GUILayout.Button("Count", GUILayout.Height(36f), GUILayout.Width(108f)))
                {
                    SetMeshRendererList(countRootObj);
                    CountMesh();
                }

                // Display Numbers
                EditorGUILayout.LabelField("Mesh Renderer", meshrendererNum.ToString());
                EditorGUILayout.LabelField("Materials", materialsNum.ToString());
                EditorGUILayout.LabelField("Original Materials", originalMaterialsNum.ToString());
                EditorGUILayout.LabelField("Triangles", triangleNum.ToString());
                EditorGUILayout.LabelField("Textures", textureNum.ToString());
                float textureSize1k = textureSize / 1024f;// * 32 * 32 / 1204
                EditorGUILayout.LabelField("TextureSize", "1024 *1024 * " + textureSize1k.ToString("N"));
            }
            else
            // Static Mode
            if (selectTab == 1)
            {
                if (changedTab || treeView == null)
                    InitializeStaticTab();
                DrawTreeView();
            }
            else
            // Lighting
            if (selectTab == 2)
            {
                if (changedTab || treeView == null)
                    InitializeLightingTab();
                DrawTreeView();
            }
            else
            // Lightmap
            if (selectTab == 3)
            {
                if (changedTab || treeView == null)
                    InitializeLightmapTab();
                DrawTreeView();
            }
        }

        private void DrawTreeView()
        {
            EditorGUI.BeginChangeCheck();
            rootGO = (GameObject)EditorGUILayout.ObjectField("Root GameObject", rootGO, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                ((IFullReloadTreeView)treeView).FullReload();
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (var checkScope = new EditorGUI.ChangeCheckScope())
                {
                    var searchString = searchField.OnToolbarGUI(treeView.searchString);

                    if (checkScope.changed)
                    {
                        SessionState.SetString(searchStringStateKey, searchString);
                        treeView.searchString = searchString;
                    }
                }
            }
            Rect rect = new Rect(20, 60, position.width - 40, position.height - 60);
            treeView.OnGUI(rect);
        }

        private List<MeshRenderer> GetMeshRenderers(bool isLightmapStatic)
        {
            var meshRendererList = new List<MeshRenderer>();
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                if (rootGO)
                {
                    if (rootGO.activeInHierarchy)
                    {
                        meshRendererList.AddRange(rootGO.GetComponentsInChildren<MeshRenderer>());
                    }
                }
                else
                {
                    var rootObjs = scene.GetRootGameObjects();
                    foreach (var rootObj in rootObjs)
                    {
                        if (!rootObj.activeInHierarchy)
                            continue;
                        meshRendererList.AddRange(rootObj.GetComponentsInChildren<MeshRenderer>());
                    }
                }
                if (isLightmapStatic)
                    meshRendererList.RemoveAll(mr => { return 0 == (GameObjectUtility.GetStaticEditorFlags(mr.gameObject) & StaticEditorFlags.LightmapStatic); });
            }
            return meshRendererList;
        }

        private List<MeshRenderer> meshRendererList;
        private List<Material> originalMaterials;
        private List<Texture> originalTextures;

        private void SetMeshRendererList(GameObject rootObj)
        {
            meshRendererList = new List<MeshRenderer>();

            // One Object
            if (rootObj)
            {
                AddMeshRendererList(rootObj);
                return;
            }

            // All Object in Scene
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] rootObjs = scene.GetRootGameObjects();

            foreach (var child in rootObjs)
            {
                AddMeshRendererList(child);
            }
        }

        private void AddMeshRendererList(GameObject rootObj)
        {
            // if not Include Deactive Mesh Renderer
            if (!includeDeactive && !rootObj.activeInHierarchy) return;
            meshRendererList.AddRange(rootObj.GetComponentsInChildren<MeshRenderer>());
            return;
        }

        private void CountReset()
        {
            meshrendererNum = 0;
            materialsNum = 0;
            originalMaterialsNum = 0;
            triangleNum = 0;
            textureNum = 0;
            textureSize = 0;
            originalMaterials = new List<Material>();
            originalTextures = new List<Texture>();
        }

        private void CountMesh()
        {
            // Initialize
            CountReset();

            // Count
            foreach (MeshRenderer child in meshRendererList)
            {
                if (includeDeactive || child.enabled)
                {
                    // Mesh Renderer
                    meshrendererNum += 1;

                    // Materials
                    Material[] materialArray = child.sharedMaterials;
                    materialsNum += materialArray.Length;

                    // Original Materials
                    foreach (Material mat in materialArray)
                    {
                        // Check if Original Material
                        if (!originalMaterials.Contains(mat) && mat)
                        {
                            originalMaterials.Add(mat);
                            originalMaterialsNum += 1;

                            // Textures
                            CountTexture(mat);
                        }
                    }

                    // vertex
                    MeshFilter childMeshFilter = child.gameObject.GetComponent<MeshFilter>();
                    if (childMeshFilter != null)
                    {
                        triangleNum += childMeshFilter.sharedMesh.triangles.Length / 3;
                    }
                }
            }
        }

        private void CountTexture(Material mat)
        {
            var serializedObject = new SerializedObject(mat);
            var m_SavedProperties = serializedObject.FindProperty("m_SavedProperties");
            var m_TexEnvs = m_SavedProperties.FindPropertyRelative("m_TexEnvs");
            for (int k = 0; k < m_TexEnvs.arraySize; k++)
            {
                Texture tex = m_TexEnvs.GetArrayElementAtIndex(k).FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if (!originalTextures.Contains(tex) && tex)
                {
                    originalTextures.Add(tex);
                    textureNum += 1;
                    textureSize += tex.height / 32 * tex.width / 32;
                }
            }
        }

        private void CopyCountToClipBoard()
        {
            EditorGUIUtility.systemCopyBuffer = meshrendererNum.ToString();
            EditorGUIUtility.systemCopyBuffer += ",";
            EditorGUIUtility.systemCopyBuffer += materialsNum.ToString();
            EditorGUIUtility.systemCopyBuffer += ",";
            EditorGUIUtility.systemCopyBuffer += originalMaterialsNum.ToString();
            EditorGUIUtility.systemCopyBuffer += ",";
            EditorGUIUtility.systemCopyBuffer += triangleNum.ToString();
            EditorGUIUtility.systemCopyBuffer += ",";
            EditorGUIUtility.systemCopyBuffer += textureNum.ToString();
            EditorGUIUtility.systemCopyBuffer += ",";
            float textureSize1k = textureSize / 1024f;// * 32 * 32 / 1204
            EditorGUIUtility.systemCopyBuffer += textureSize1k.ToString();
        }

        class GameObjectElemnt
        {
            private bool lightmapStatic;
            internal bool LightmapStatic
            {
                get { return lightmapStatic; }
                set
                {
                    lightmapStatic = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.LightmapStatic);
                }
            }
            private bool occluderStatic;
            internal bool OccluderStatic
            {
                get { return occluderStatic; }
                set
                {
                    occluderStatic = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.OccluderStatic);
                }
            }
            private bool occludeeStatic;
            internal bool OccludeeStatic
            {
                get { return occludeeStatic; }
                set
                {
                    occludeeStatic = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.OccludeeStatic);
                }
            }
            private bool batchingStatic;
            internal bool BatchingStatic
            {
                get { return batchingStatic; }
                set
                {
                    batchingStatic = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.BatchingStatic);
                }
            }
            private bool navigationStatic;
            internal bool NavigationStatic
            {
                get { return navigationStatic; }
                set
                {
                    navigationStatic = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.NavigationStatic);
                }
            }
            private bool offMeshLinkGeneration;
            internal bool OffMeshLinkGeneration
            {
                get { return offMeshLinkGeneration; }
                set
                {
                    offMeshLinkGeneration = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.OffMeshLinkGeneration);
                }
            }
            private bool reflectionProbeStatic;
            internal bool ReflectionProbeStatic
            {
                get { return reflectionProbeStatic; }
                set
                {
                    reflectionProbeStatic = value;
                    SetStaticFlag(value, (int)StaticEditorFlags.ReflectionProbeStatic);
                }
            }

            readonly internal SerializedProperty m_Layer;
            readonly internal SerializedProperty m_StaticEditorFlags;

            public GameObjectElemnt(SerializedObject serializedObject)
            {
                m_Layer = serializedObject.FindProperty("m_Layer");
                m_StaticEditorFlags = serializedObject.FindProperty("m_StaticEditorFlags");
                var flag = (StaticEditorFlags)m_StaticEditorFlags.intValue;
                lightmapStatic = 0 < (flag & StaticEditorFlags.LightmapStatic);
                occluderStatic = 0 < (flag & StaticEditorFlags.OccluderStatic);
                occludeeStatic = 0 < (flag & StaticEditorFlags.OccludeeStatic);
                batchingStatic = 0 < (flag & StaticEditorFlags.BatchingStatic);
                navigationStatic = 0 < (flag & StaticEditorFlags.NavigationStatic);
                offMeshLinkGeneration = 0 < (flag & StaticEditorFlags.OffMeshLinkGeneration);
                reflectionProbeStatic = 0 < (flag & StaticEditorFlags.ReflectionProbeStatic);
            }

            private void SetStaticFlag(bool active, int targetFlag)
            {
                int flags = m_StaticEditorFlags.intValue;
                if (flags < 0)
                {
                    int allPossibleValues = 0;
                    var values = System.Enum.GetValues(typeof(StaticEditorFlags));
                    foreach (var value in values)
                    {
                        allPossibleValues |= (int)value;
                    }
                    flags = flags & allPossibleValues;
                }
                if (active)
                {
                    flags = flags | targetFlag;
                }
                else
                {
                    flags = flags & ~targetFlag;
                }
                m_StaticEditorFlags.intValue = flags;
            }
        }

        class GameObjectTreeViewItem : UnityObjectTreeViewItem<GameObjectElemnt>
        {
            override public GameObjectElemnt element { get; set; }
            override public SerializedObject serializedObject { get; }

            public GameObjectTreeViewItem(int id, Object obj) : base(id, obj)
            {
                serializedObject = new SerializedObject(((MeshRenderer)obj).gameObject);
                element = new GameObjectElemnt(serializedObject);
                displayName = obj.name;
            }
        }

        class MeshRendererElement
        {
            readonly internal SerializedProperty m_LightProbeUsage;
            readonly internal SerializedProperty m_ReflectionProbeUsage;
            readonly internal SerializedProperty m_ProbeAnchor;
            readonly internal SerializedProperty m_CastShadows;
            readonly internal SerializedProperty m_ReceiveShadows;
            readonly internal SerializedProperty m_ScaleInLightmap;
            readonly internal SerializedProperty m_ImportantGI;
            readonly internal SerializedProperty m_StitchLightmapSeams;

            public MeshRendererElement(SerializedObject serializedObject)
            {
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

        class MeshRendererTreeViewItem : UnityObjectTreeViewItem<MeshRendererElement>
        {
            override public MeshRendererElement element { get; set; }
            override public SerializedObject serializedObject { get; }

            public MeshRendererTreeViewItem(int id, Object obj) : base(id, obj)
            {
                serializedObject = new SerializedObject(obj);
                element = new MeshRendererElement(serializedObject);
                displayName = obj.name;
            }
        }
    }
}// namespace MomomaAssets
