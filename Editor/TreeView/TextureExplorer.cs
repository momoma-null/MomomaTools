using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityObject = UnityEngine.Object;

namespace MomomaAssets
{

    class TextureExplorer : EditorWindow
    {
        enum MaxTextureSize
        {
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192
        }

        const string searchStringStateKey = "TextureExplorerTreeViewWindow_SearchString";
        const string sortedColumnIndexStateKey = "TextureExplorerTreeViewWindow_sortedColumnIndex";

        static readonly Type s_TextureUtilType = Type.GetType("UnityEditor.TextureUtil, UnityEditor.dll");
        static readonly Dictionary<string, MethodInfo> s_TextureUtilInfos = new Dictionary<string, MethodInfo>();

        SearchField m_SearchField;
        UnityObjectTreeViewBase m_TreeView;

        [MenuItem("MomomaTools/Texture Explorer", false, 110)]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<TextureExplorer>("Texture Explorer");
        }

        static MethodInfo GetMethod(string methodName)
        {
            MethodInfo info;
            if (s_TextureUtilInfos.TryGetValue(methodName, out info))
                return info;
            info = s_TextureUtilType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            s_TextureUtilInfos[methodName] = info;
            return info;
        }

        static bool IsEnabled(UnityObject assetObject)
        {
            if (assetObject == null || (assetObject.hideFlags & HideFlags.NotEditable) != 0)
                return false;
            if (!EditorUtility.IsPersistent(assetObject))
                return true;
            var instanceID = assetObject.GetInstanceID();
            var opts = EditorUserSettings.allowAsyncStatusUpdate ? StatusQueryOptions.UseCachedAsync : StatusQueryOptions.UseCachedIfPossible;
            if (AssetDatabase.IsNativeAsset(instanceID))
            {
                var assetPath = AssetDatabase.GetAssetPath(instanceID);
                if (!AssetDatabase.IsOpenForEdit(assetPath, opts))
                    return false;
            }
            else if (AssetDatabase.IsForeignAsset(instanceID))
            {
                if (!AssetDatabase.IsMetaFileOpenForEdit(assetObject, opts))
                    return false;
            }
            return true;
        }

        void OnEnable()
        {
            minSize = new Vector2(500f, 250f);
            m_SearchField = new SearchField();
        }

        void OnDisable()
        {
            m_TreeView = null;
            m_SearchField = null;
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
            header.Add("Name", 200, item => item.displayName);
            header.Add("Width", 50, item => item.width, TextAlignment.Right);
            header.Add("Height", 50, item => item.height, TextAlignment.Right);
            header.Add("Memory Size", 80, item => item.memorySize, TextAlignment.Right);
            header.Add("Max Texture Size", 60, item => (MaxTextureSize)item.m_MaxTextureSize.intValue, (item, value) => item.m_MaxTextureSize.intValue = (int)value, item => item.m_MaxTextureSize);
            header.Add("Texture Type", 80, item => (TextureImporterType)item.m_TextureType.intValue, (item, value) => item.m_TextureType.intValue = (int)value, item => item.m_TextureType);
            header.Add("sRGB", 50, item => Convert.ToBoolean(item.m_sRGBTexture.intValue), (item, value) => item.m_sRGBTexture.intValue = Convert.ToInt32(value), item => item.m_sRGBTexture, item => item.m_TextureType.intValue == 0);
            header.Add("Alpha Source", 80, item => (TextureImporterAlphaSource)item.m_AlphaUsage.intValue, (item, value) => item.m_AlphaUsage.intValue = (int)value, item => item.m_AlphaUsage);
            header.Add("Transparency", 50, item => Convert.ToBoolean(item.m_AlphaUsage.intValue), (item, value) => item.m_AlphaIsTransparency.intValue = Convert.ToInt32(value), item => item.m_AlphaIsTransparency, item => item.m_AlphaUsage.intValue > 0);
            header.Add("Mip Map", 50, item => Convert.ToBoolean(item.m_EnableMipMap.intValue), (item, value) => item.m_EnableMipMap.intValue = Convert.ToInt32(value), item => item.m_EnableMipMap);
            header.Add("Preserve Coverage", 50, item => Convert.ToBoolean(item.m_MipMapsPreserveCoverage.intValue), (item, value) => item.m_MipMapsPreserveCoverage.intValue = Convert.ToInt32(value), item => item.m_MipMapsPreserveCoverage, item => item.m_EnableMipMap.intValue == 1);
            header.Add("Alpha Cutoff Value", 60, item => item.m_AlphaTestReferenceValue, item => item.m_EnableMipMap.intValue == 1 && item.m_MipMapsPreserveCoverage.intValue == 1);
            header.Add("Readable", 50, item => Convert.ToBoolean(item.m_IsReadable.intValue), (item, value) => item.m_IsReadable.intValue = Convert.ToInt32(value), item => item.m_IsReadable);
            header.Add("Crunched Compression", 50, item => item.m_CrunchedCompression);
            header.Add("Compression Quality", 60, item => item.m_CompressionQuality, item => item.m_CrunchedCompression.boolValue == true);
            m_TreeView = new UnityObjectTreeView<TextureTreeViewItem>(new TreeViewState(), header.GetHeader(), sortedColumnIndexStateKey, () => GetTreeViewItems(), item => item.ImportAsset(), false);
            m_TreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        }

        IEnumerable<UnityObjectTreeViewItem> GetTreeViewItems()
        {
            var scene = SceneManager.GetActiveScene();
            var dependencies = AssetDatabase.GetDependencies(scene.path, true);
            var importers = dependencies.Select(path => (AssetDatabase.LoadAssetAtPath<Texture>(path), AssetImporter.GetAtPath(path) as TextureImporter)).Where(i => i.Item2 != null && IsEnabled(i.Item1));
            return importers.Select(i => new TextureTreeViewItem(i.Item1.GetInstanceID(), i.Item1, i.Item2)).ToArray();
        }

        class TextureTreeViewItem : UnityObjectTreeViewItem
        {
            override public SerializedObject serializedObject { get; }

            readonly internal Texture targetTexture;
            readonly internal TextureImporter importer;
            readonly internal SerializedProperty m_MaxTextureSize;
            readonly internal SerializedProperty m_TextureType;
            readonly internal SerializedProperty m_sRGBTexture;
            readonly internal SerializedProperty m_AlphaUsage;
            readonly internal SerializedProperty m_AlphaIsTransparency;
            readonly internal SerializedProperty m_EnableMipMap;
            readonly internal SerializedProperty m_MipMapsPreserveCoverage;
            readonly internal SerializedProperty m_AlphaTestReferenceValue;
            readonly internal SerializedProperty m_IsReadable;
            readonly internal SerializedProperty m_CrunchedCompression;
            readonly internal SerializedProperty m_CompressionQuality;

            internal int width => (int)GetMethod("GetGPUWidth").Invoke(null, new object[] { targetTexture });
            internal int height => (int)GetMethod("GetGPUHeight").Invoke(null, new object[] { targetTexture });
            internal MemorySize memorySize => new MemorySize((long)GetMethod("GetStorageMemorySizeLong").Invoke(null, new object[] { targetTexture }));

            internal TextureTreeViewItem(int id, Texture texture, TextureImporter obj) : base(id)
            {
                serializedObject = new SerializedObject(obj);
                importer = obj;
                targetTexture = texture;
                displayName = texture.name;

                m_MaxTextureSize = serializedObject.FindProperty("m_PlatformSettings.Array.data[0].m_MaxTextureSize");
                m_TextureType = serializedObject.FindProperty("m_TextureType");
                m_sRGBTexture = serializedObject.FindProperty("m_sRGBTexture");
                m_AlphaUsage = serializedObject.FindProperty("m_AlphaUsage");
                m_AlphaIsTransparency = serializedObject.FindProperty("m_AlphaIsTransparency");
                m_EnableMipMap = serializedObject.FindProperty("m_EnableMipMap");
                m_MipMapsPreserveCoverage = serializedObject.FindProperty("m_MipMapsPreserveCoverage");
                m_AlphaTestReferenceValue = serializedObject.FindProperty("m_AlphaTestReferenceValue");
                m_IsReadable = serializedObject.FindProperty("m_IsReadable");
                m_CrunchedCompression = serializedObject.FindProperty("m_PlatformSettings.Array.data[0].m_CrunchedCompression");
                m_CompressionQuality = serializedObject.FindProperty("m_PlatformSettings.Array.data[0].m_CompressionQuality");
            }

            internal void ImportAsset()
            {
                importer.SaveAndReimport();
            }
        }

        struct MemorySize : IComparable
        {
            readonly long value;

            internal MemorySize(long value) => this.value = value;

            public int CompareTo(object other)
            {
                if (other == null || !(other is MemorySize))
                    return 1;
                return value.CompareTo(((MemorySize)other).value);
            }

            public override string ToString()
            {
                return EditorUtility.FormatBytes(value);
            }
        }
    }

}// namespace MomomaAssets
