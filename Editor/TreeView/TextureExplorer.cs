using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MomomaAssets
{

    class TextureExplorer : EditorWindow
    {
        const string searchStringStateKey = "TextureExplorerTreeViewWindow_SearchString";
        const string sortedColumnIndexStaticStateKey = "TextureExplorerTreeViewWindow_sortedColumnIndex";

        SearchField m_SearchField;
        UnityObjectTreeViewBase m_TreeView;
        TextureImporter[] m_Importers;

        [MenuItem("MomomaTools/TextureExplorer", false, 110)]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<TextureExplorer>("TextureExplorer");
        }

        void OnEnable()
        {
            minSize = new Vector2(500f, 250f);
            m_SearchField = new SearchField();
        }

        void OnHierarchyChange()
        {
            m_TreeView?.OnHierarchyChange();
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
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.VerticalScope())
                {
                    if (m_TreeView == null)
                        Initialize();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Apply"))
                        {
                            try
                            {
                                AssetDatabase.StartAssetEditing();
                                foreach (var i in m_Importers)
                                {
                                    i.SaveAndReimport();
                                }
                            }
                            catch
                            {
                                throw;
                            }
                            finally
                            {
                                AssetDatabase.StopAssetEditing();
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
                    var rect = GUILayoutUtility.GetRect(0, float.MaxValue, 0, float.MaxValue);
                    m_TreeView.OnGUI(rect);
                }
                EditorGUILayout.Space();
            }
            EditorGUILayout.Space();
        }

        void Initialize()
        {
            var header = new MultiColumnHeaderMaker<TextureTreeViewItem>();
            header.AddtoList("Name", 100, item => item.displayName);
            header.AddtoList("Width", 50, item => item.width.intValue);
            header.AddtoList("Height", 50, item => item.height.intValue);
            header.AddtoList("Use Mip Map", 30, item => System.Convert.ToBoolean(item.m_EnableMipMap.intValue), (item, value) => item.m_EnableMipMap.intValue = System.Convert.ToInt32(value), item => item.m_EnableMipMap);
            header.AddtoList("Is Readable", 30, item => System.Convert.ToBoolean(item.m_IsReadable.intValue), (item, value) => item.m_IsReadable.intValue = System.Convert.ToInt32(value), item => item.m_IsReadable);
            header.AddtoList("Max Texture Size", 50, item => item.m_MaxTextureSize);
            m_TreeView = new UnityObjectTreeView<TextureTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexStaticStateKey, () => GetTreeViewItems());
            m_TreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        }

        IEnumerable<UnityObjectTreeViewItem> GetTreeViewItems()
        {
            var scene = SceneManager.GetActiveScene();
            var importers = EditorUtility.CollectDependencies(new Object[] { AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) }).Where(o => o is Texture && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o))).Select(tex => (tex, AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter)).Where(i => i.Item2 != null);
            m_Importers = importers.Select(i => i.Item2).ToArray();
            return importers.Select(i => new TextureTreeViewItem(i.tex.GetInstanceID(), i.Item2)).ToArray();
        }

        class TextureTreeViewItem : UnityObjectTreeViewItem
        {
            override public SerializedObject serializedObject { get; }

            readonly internal Texture targetTexture;
            readonly internal TextureImporter importer;
            readonly internal SerializedProperty m_EnableMipMap;
            readonly internal SerializedProperty m_IsReadable;
            readonly internal SerializedProperty m_MaxTextureSize;
            readonly internal SerializedProperty width;
            readonly internal SerializedProperty height;

            internal TextureTreeViewItem(int id, TextureImporter obj) : base(id)
            {
                serializedObject = new SerializedObject(obj);
                importer = obj;
                targetTexture = AssetDatabase.LoadAssetAtPath<Texture>(obj.assetPath);
                displayName = targetTexture.name;

                m_EnableMipMap = serializedObject.FindProperty("m_EnableMipMap");
                m_IsReadable = serializedObject.FindProperty("m_IsReadable");
                m_MaxTextureSize = serializedObject.FindProperty("m_PlatformSettings.Array.data[0].m_MaxTextureSize");
                width = serializedObject.FindProperty("m_Output.textureImportInstructions.width");
                height = serializedObject.FindProperty("m_Output.textureImportInstructions.height");
            }
        }
    }

}// namespace MomomaAssets
