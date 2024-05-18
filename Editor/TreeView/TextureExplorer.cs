
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        static readonly Type s_TextureUtilType = Type.GetType("UnityEditor.TextureUtil, UnityEditor.dll");
        static readonly Dictionary<string, MethodInfo> s_TextureUtilInfos = new();

        [SerializeField]
        TreeViewState m_ViewState = new();
        [SerializeField]
        MultiColumnHeaderState headerState = GetHeaderState();

        SearchField m_SearchField;
        UnityObjectTreeViewBase m_TreeView;

        [MenuItem("MomomaTools/Texture Explorer", false, 110)]
        static void ShowWindow()
        {
            GetWindow<TextureExplorer>("Texture Explorer");
        }

        static MethodInfo GetMethod(string methodName)
        {
            if (s_TextureUtilInfos.TryGetValue(methodName, out var info))
                return info;
            info = s_TextureUtilType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            s_TextureUtilInfos[methodName] = info;
            return info;
        }

        static bool IsEnabled(UnityObject assetObject)
        {
            if (assetObject == null)
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
                    m_TreeView ??= new UnityObjectTreeView<TextureTreeViewItem>(m_ViewState, headerState, GetTreeViewItems, item => item.ImportAsset(), false);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        var searchString = m_SearchField.OnToolbarGUI(m_TreeView.searchString);
                        if (EditorGUI.EndChangeCheck())
                        {
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

        static MultiColumnHeaderState GetHeaderState()
        {
            var columns = new ColumnArray<TextureTreeViewItem>();
            columns.Add("Name", 200, item => item.displayName);
            columns.Add("Width", 50, item => item.width, TextAlignment.Right);
            columns.Add("Height", 50, item => item.height, TextAlignment.Right);
            columns.Add("Memory Size", 80, item => item.memorySize, TextAlignment.Right);
            columns.AddIntAsEnum("Max Texture Size", 60, item => (MaxTextureSize)item.m_MaxTextureSize.intValue, item => item.m_MaxTextureSize);
            columns.AddIntAsEnum("Texture Type", 80, item => (TextureImporterType)item.m_TextureType.intValue, item => item.m_TextureType);
            columns.AddIntAsToggle("sRGB", 50, item => item.m_sRGBTexture, item => item.m_TextureType.intValue == 0);
            columns.AddIntAsEnum("Alpha Source", 80, item => (TextureImporterAlphaSource)item.m_AlphaUsage.intValue, item => item.m_AlphaUsage);
            columns.AddIntAsToggle("Transparency", 50, item => item.m_AlphaIsTransparency, item => item.m_AlphaUsage.intValue > 0);
            columns.AddIntAsToggle("Mip Map", 50, item => item.m_EnableMipMap);
            columns.AddIntAsToggle("Preserve Coverage", 50, item => item.m_MipMapsPreserveCoverage, item => item.m_EnableMipMap.intValue == 1);
            columns.Add("Alpha Cutoff Value", 60, item => item.m_AlphaTestReferenceValue.floatValue, item => item.m_AlphaTestReferenceValue, item => item.m_EnableMipMap.intValue == 1 && item.m_MipMapsPreserveCoverage.intValue == 1);
            columns.AddIntAsToggle("Readable", 50, item => item.m_IsReadable);
            columns.Add("Crunched Compression", 50, item => item.m_CrunchedCompression.boolValue, item => item.m_CrunchedCompression);
            columns.Add("Compression Quality", 60, item => item.m_CompressionQuality.intValue, item => item.m_CompressionQuality, item => item.m_CrunchedCompression.boolValue == true);
            return columns.GetHeaderState();
        }

        IEnumerable<UnityObjectTreeViewItem> GetTreeViewItems()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var srcPath = prefabStage?.assetPath ?? SceneManager.GetActiveScene().path;
            var dependencies = AssetDatabase.GetDependencies(srcPath, true);
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
            internal MemorySize memorySize => new((long)GetMethod("GetStorageMemorySizeLong").Invoke(null, new object[] { targetTexture }));

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

        readonly struct MemorySize : IComparable
        {
            readonly long value;

            internal MemorySize(long value) => this.value = value;

            public int CompareTo(object other)
            {
                if (other is null or not MemorySize)
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
