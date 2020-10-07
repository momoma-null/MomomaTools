using System;
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
    private MeshRendererTreeView meshRendererTreeView;
	private const string searchStringStateKey = "MeshRendererTreeViewWindow_SearchString";

    
	// Generate menu tab
	[MenuItem("MomomaTools/MeshRendererExplorer")]
    static void ShowWindow()
    {
        EditorWindow.GetWindow<MeshRendererExplorer>("MeshRendererExplorer");
    }

	public void OnHierarchyChange()
	{
		if (meshRendererTreeView != null) meshRendererTreeView.FullReload();
		SetMeshRendererList(countRootObj);
		CountReset();
	}

	private MeshRendererTableHeader MRStaticHeader()
	{
		var header = new MeshRendererTableHeader(null);
		header.AddtoList("Name", 100,(item) => {return item.element.name;}, null);
		header.AddtoList("Lightmap", 30, (item) => {return item.element.LightmapStatic;}, (item, value) => {item.element.LightmapStatic = (bool)value;});
		header.AddtoList("Occluder", 30, (item) => {return item.element.OccluderStatic;}, (item, value) => {item.element.OccluderStatic = (bool)value;});
		header.AddtoList("Occludee", 30, (item) => {return item.element.OccludeeStatic;}, (item, value) => {item.element.OccludeeStatic = (bool)value;});
		header.AddtoList("Batching", 30, (item) => {return item.element.BatchingStatic;}, (item, value) => {item.element.BatchingStatic = (bool)value;});
		header.AddtoList("Navigation", 30, (item) => {return item.element.NavigationStatic;}, (item, value) => {item.element.NavigationStatic = (bool)value;});
		header.AddtoList("OffMeshLink", 30, (item) => {return item.element.OffMeshLinkGeneration;}, (item, value) => {item.element.OffMeshLinkGeneration = (bool)value;});
		header.AddtoList("Reflection", 30, (item) => {return item.element.ReflectionProbeStatic;}, (item, value) => {item.element.ReflectionProbeStatic = (bool)value;});
		header.SetList();

		return header;
	}

	private MeshRendererTableHeader MRLightingHeader()
	{
		var header = new MeshRendererTableHeader(null);
		header.AddtoList("Name", 100, (item) => {return item.element.name;}, null);
		header.AddtoList("LightProbe", 50, (item) => {return (LightProbeUsage)item.element.m_LightProbeUsage.intValue;}, (item, value) => {item.element.m_LightProbeUsage.intValue = (int)value;});
		header.AddtoList("ReflectionProbe", 50, (item) => {return (ReflectionProbeUsage)item.element.m_ReflectionProbeUsage.intValue;}, (item, value) => {item.element.m_ReflectionProbeUsage.intValue = (int)value;});
		header.AddtoList("ProbeAnchor", 50, (item) => {return item.element.m_ProbeAnchor;});
		header.AddtoList("CastShadows", 50, (item) => {return item.element.m_CastShadows;});
		header.AddtoList("ReceiveShadows", 30, (item) => {return item.element.m_ReceiveShadows;});
		header.SetList();

		return header;
	}

	private MeshRendererTableHeader MRLightmapHeader()
	{
		var header = new MeshRendererTableHeader(null);
		header.AddtoList("Name", 100, (item) => {return item.element.name;}, null);
		header.AddtoList("ScaleInLightmap", 50, (item) => {return item.element.m_ScaleInLightmap;});
		header.AddtoList("PrioritizeIllumination", 30, (item) => {return item.element.m_ImportantGI;});
		header.AddtoList("StitchSeams", 30, (item) => {return item.element.m_StitchLightmapSeams;});
		header.SetList();

		return header;
	}

	private void InitializeStaticTab()
    {
        var state = new TreeViewState();
        var header = MRStaticHeader();
        meshRendererTreeView = new MeshRendererTreeView(state, header, false, rootGO);
		meshRendererTreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        searchField = new SearchField();
        searchField.downOrUpArrowKeyPressed += meshRendererTreeView.SetFocusAndEnsureSelectedItem;
    }

	private void InitializeLightingTab()
    {
        var state = new TreeViewState();
        var header = MRLightingHeader();
        meshRendererTreeView = new MeshRendererTreeView(state, header, false, rootGO);
		meshRendererTreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        searchField = new SearchField();
        searchField.downOrUpArrowKeyPressed += meshRendererTreeView.SetFocusAndEnsureSelectedItem;
    }

	private void InitializeLightmapTab()
    {
        var state = new TreeViewState();
        var header = MRLightmapHeader();
        meshRendererTreeView = new MeshRendererTreeView(state, header, true, rootGO);
        meshRendererTreeView.searchString = SessionState.GetString(searchStringStateKey, "");
        searchField = new SearchField();
        searchField.downOrUpArrowKeyPressed += meshRendererTreeView.SetFocusAndEnsureSelectedItem;
    }

	private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		EditorGUI.BeginChangeCheck();
		selectTab = GUILayout.Toolbar(selectTab, new string[]{ "Count", "Static", "Lighting", "Lightmap" }, GUILayout.MaxWidth(500));
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
		}else
		// Static Mode
		if (selectTab == 1)
		{
			if (changedTab) InitializeStaticTab();
			DrawTreeView();
		}else
		// Lighting
		if (selectTab == 2)
		{
			if (changedTab) InitializeLightingTab();
			DrawTreeView();
		}else
		// Lightmap
		if (selectTab == 3)
		{
			if (changedTab) InitializeLightmapTab();
			DrawTreeView();
		}
	}

	private void DrawTreeView()
	{
		EditorGUI.BeginChangeCheck();
		rootGO = (GameObject)EditorGUILayout.ObjectField("Root GameObject", rootGO, typeof(GameObject), true);
		if (EditorGUI.EndChangeCheck())
		{
			meshRendererTreeView.RootGO = rootGO;
			meshRendererTreeView.FullReload();
		}

		if (meshRendererTreeView == null) return;
		using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
           	using (var checkScope = new EditorGUI.ChangeCheckScope())
           	{
	            var searchString = searchField.OnToolbarGUI(meshRendererTreeView.searchString);

    	        if (checkScope.changed)
                {
           	        SessionState.SetString(searchStringStateKey, searchString);
               	    meshRendererTreeView.searchString = searchString;
               	}
           	}
        }
		Rect rect = new Rect(20, 60, position.width - 40, position.height - 60);
		meshRendererTreeView.OnGUI(rect);
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
						originalMaterialsNum +=1;

						// Textures
						CountTexture(mat);
					}
				}

				// vertex
				MeshFilter childMeshFilter = child.gameObject.GetComponent<MeshFilter>();
				if(childMeshFilter != null)
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
}

}// namespace MomomaAssets
