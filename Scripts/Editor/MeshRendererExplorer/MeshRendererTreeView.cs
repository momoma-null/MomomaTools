using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MomomaAssets
{

public enum GUIType
{
	Text,
	Bool,
	Int,
	Float,
	Color,
	Custom
}

public class MeshRendererElement
{
    public GameObject gameObject { get; private set; }
	public string name { get; private set; }
	private bool lightmapStatic;
	public bool LightmapStatic
	{ 
		get{ return lightmapStatic;}
		set
		{
			lightmapStatic = value;
			SetStaticFlag(value, (int)StaticEditorFlags.LightmapStatic);
		}
	}
    private bool occluderStatic;
	public bool OccluderStatic
	{ 
		get{ return occluderStatic;}
		set
		{
			occluderStatic = value;
			SetStaticFlag(value, (int)StaticEditorFlags.OccluderStatic);
		}
	}
    private bool occludeeStatic;
	public bool OccludeeStatic
	{ 
		get{ return occludeeStatic;}
		set
		{
			occludeeStatic = value;
			SetStaticFlag(value, (int)StaticEditorFlags.OccludeeStatic);
		}
	}
    private bool batchingStatic;
	public bool BatchingStatic
	{ 
		get{ return batchingStatic;}
		set
		{
			batchingStatic = value;
			SetStaticFlag(value, (int)StaticEditorFlags.BatchingStatic);
		}
	}
    private bool navigationStatic;
	public bool NavigationStatic
	{ 
		get{ return navigationStatic;}
		set
		{
			navigationStatic = value;
			SetStaticFlag(value, (int)StaticEditorFlags.NavigationStatic);
		}
	}
    private bool offMeshLinkGeneration;
	public bool OffMeshLinkGeneration
	{ 
		get{ return offMeshLinkGeneration;}
		set
		{
			offMeshLinkGeneration = value;
			SetStaticFlag(value, (int)StaticEditorFlags.OffMeshLinkGeneration);
		}
	}
    private bool reflectionProbeStatic;
	public bool ReflectionProbeStatic
	{ 
		get{ return reflectionProbeStatic;}
		set
		{
			reflectionProbeStatic = value;
			SetStaticFlag(value, (int)StaticEditorFlags.ReflectionProbeStatic);
		}
	}
	public SerializedObject soGO { get; private set; } 
	private SerializedProperty m_StaticEditorFlags;
	public SerializedObject soMR { get; private set; }
	public SerializedProperty m_LightProbeUsage { get; private set; }
	public SerializedProperty m_ReflectionProbeUsage { get; private set; }
	public SerializedProperty m_ProbeAnchor { get; private set; }
	public SerializedProperty m_CastShadows { get; private set; }
	public SerializedProperty m_ReceiveShadows { get; private set; }
	public SerializedProperty m_ScaleInLightmap { get; private set; }
	public SerializedProperty m_ImportantGI { get; private set; }
	public SerializedProperty m_StitchLightmapSeams { get; private set; }
	public SerializedProperty m_LightmapParameters { get; private set; }

	private void SetStaticFlag(bool active, int targetFlag)
	{
		if (!gameObject) return;
		int flags = m_StaticEditorFlags.intValue;
		if (flags < 0)
        {
            int allPossibleValues = 0;
            var values = Enum.GetValues(typeof(StaticEditorFlags));
            foreach (var value in values)
            {
                allPossibleValues |= (int)value;
            }
            flags = flags & allPossibleValues;
        }
		if (active)
		{
			flags = flags | targetFlag;
		}else
		{
			flags = flags & ~targetFlag;
		}
		m_StaticEditorFlags.intValue = flags;
	}
	public MeshRendererElement(MeshRenderer mr)
    {
        name = mr.name;
		gameObject = mr.gameObject;
		soGO = new SerializedObject(gameObject);
		soGO.Update();
		m_StaticEditorFlags = soGO.FindProperty("m_StaticEditorFlags");
		int flag = m_StaticEditorFlags.intValue;
		lightmapStatic = 0 < (flag & (int)StaticEditorFlags.LightmapStatic);
        occluderStatic = 0 < (flag & (int)StaticEditorFlags.OccluderStatic);
        occludeeStatic = 0 < (flag & (int)StaticEditorFlags.OccludeeStatic);
        batchingStatic = 0 < (flag & (int)StaticEditorFlags.BatchingStatic);
        navigationStatic = 0 < (flag & (int)StaticEditorFlags.NavigationStatic);
        offMeshLinkGeneration = 0 < (flag & (int)StaticEditorFlags.OffMeshLinkGeneration);
        reflectionProbeStatic = 0 < (flag & (int)StaticEditorFlags.ReflectionProbeStatic);
		soMR = new SerializedObject(mr);
		soMR.Update();
		m_LightProbeUsage = soMR.FindProperty("m_LightProbeUsage");
		m_ReflectionProbeUsage = soMR.FindProperty("m_ReflectionProbeUsage");
		m_ProbeAnchor = soMR.FindProperty("m_ProbeAnchor");
		m_CastShadows = soMR.FindProperty("m_CastShadows");
		m_ReceiveShadows = soMR.FindProperty("m_ReceiveShadows");
		m_ScaleInLightmap = soMR.FindProperty("m_ScaleInLightmap");
		m_ImportantGI = soMR.FindProperty("m_ImportantGI");
		m_StitchLightmapSeams = soMR.FindProperty("m_StitchLightmapSeams");
    }
}

public class MeshRendererTableItem : TreeViewItem
{
    public MeshRendererElement element { get; set; }

    public MeshRendererTableItem(int id, MeshRenderer mr) : base(id)
    {
        element = new MeshRendererElement(mr);
    }
}

public class MeshRendererTableHeader : MultiColumnHeader
{
	private List<MultiColumnHeaderState.Column> columns;

    public MeshRendererTableHeader(MultiColumnHeaderState state) : base(state)
    {
        columns = new List<MultiColumnHeaderState.Column>();
    }

	public void AddtoList(string name, float width, GUIType guiType, MRColumn.Property propertyDelegate, MRColumn.CustomGUI customGUIDelegate = null)
	{
		MRColumn column = new MRColumn
		{
			width = width,
    	    headerContent = new GUIContent(name)
		};
		column.guiType = guiType;
		column.propertyDelegate = propertyDelegate;
		column.customGUIDelegate = customGUIDelegate;
		columns.Add(column);
	}

	public void SetList()
	{
		state = new MultiColumnHeaderState(columns.ToArray());
	}
}

public class MRColumn : MultiColumnHeaderState.Column
{
	public delegate object Property(MeshRendererTableItem mrItem, object setObj);
	public delegate void CustomGUI(MeshRendererTableItem mrItem, Rect rect);
	public Property propertyDelegate;
	public CustomGUI customGUIDelegate;
	public GUIType guiType;
}

public class MeshRendererTreeView : TreeView
{
	//private const string sortedColumnIndexStateKey = "MeshRendererViewWindow_sortedColumnIndex";
    
    public MeshRendererTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader, bool lightmapStaticOnly = false, GameObject rootObj = null) : base(state, multiColumnHeader)
    {
        rowHeight = 16;
        showAlternatingRowBackgrounds = true;
		m_LightmapStaticOnly = lightmapStaticOnly;
        multiColumnHeader.sortingChanged += SortItems;
		multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
		m_RootGO = rootObj;

        multiColumnHeader.ResizeToFit();
        Reload();
        
		// Setup
        //multiColumnHeader.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, -1);
    }

	private List<TreeViewItem> m_Items;
	private List<TreeViewItem> m_SearchedItems;
	private GameObject m_RootGO;
	private bool m_LightmapStaticOnly;

	public GameObject RootGO
	{
		get{ return m_RootGO; }
		set{ m_RootGO = value; }
	}

	public void FullReload()
    {
         m_Items = null;
         Reload();
    }
	
	protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
	{
		// create Item List
		if (m_Items == null)
		{
			m_Items = new List<TreeViewItem>();
			
			Scene scene = SceneManager.GetActiveScene();
			if (scene.IsValid())
			{
        		List<MeshRenderer> meshRendererArray = new List<MeshRenderer>();

				if (m_RootGO)
				{
					if (m_RootGO.activeInHierarchy)
					{
						meshRendererArray.AddRange(m_RootGO.GetComponentsInChildren<MeshRenderer>());
					}
				}else
				{
        			GameObject[] rootObjs = scene.GetRootGameObjects();
		
        			foreach (var rootObj in rootObjs)
        			{
            			if(!rootObj.activeInHierarchy) continue;
						meshRendererArray.AddRange(rootObj.GetComponentsInChildren<MeshRenderer>());
        			}
				}
        
				foreach (var child in meshRendererArray)
        		{
	       			if(m_LightmapStaticOnly
					 && 0 == (GameObjectUtility.GetStaticEditorFlags(child.gameObject) & StaticEditorFlags.LightmapStatic)) continue;
					m_Items.Add(new MeshRendererTableItem(child.gameObject.GetInstanceID(), child));
        		}
	        }
			SortItems(multiColumnHeader);
		}

		SearchFullTree();
		Repaint();
		
		return m_SearchedItems;
	}

	private void SearchFullTree()
	{
		if (!hasSearch)
		{
			m_SearchedItems = m_Items;
			return;
		}
		m_SearchedItems = new List<TreeViewItem>();
		foreach (var child in m_Items)
		{
			if (DoesItemMatchSearch(child, searchString))
			{
				m_SearchedItems.Add(child);
			}
		}
	}

    protected override TreeViewItem BuildRoot()
    {
        return new TreeViewItem(-1, -1, null);
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        var item = (MeshRendererTableItem) args.item;
		if (item == null) return;
		if (!item.element.gameObject) return;

        item.element.soGO.Update();
		item.element.soMR.Update();
		
		var labelStyle = args.selected ? EditorStyles.whiteLabel : EditorStyles.label;
        labelStyle.alignment = TextAnchor.MiddleLeft;
            
		for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); visibleColumnIndex++)
        {
            var rect = args.GetCellRect(visibleColumnIndex);
			CenterRectUsingSingleLineHeight(ref rect);
            int columnIndex = args.GetColumn(visibleColumnIndex);
			MRColumn column = (MRColumn)this.multiColumnHeader.GetColumn(columnIndex);
    
			switch (column.guiType)
            {
                case GUIType.Text:
                    EditorGUI.LabelField(rect, (string)column.propertyDelegate(item, null), labelStyle);
                    break;
                case GUIType.Bool:
					rect.width = rect.height;
					EditorGUI.BeginChangeCheck();
					var newBool = EditorGUI.Toggle(rect, (bool)column.propertyDelegate(item, null));
					if (EditorGUI.EndChangeCheck())
					{
						column.propertyDelegate(item, newBool);
					}
                    break;
				case GUIType.Float:
					EditorGUIUtility.labelWidth = rect.width * .2f;
					column.propertyDelegate(item, EditorGUI.FloatField(rect, " ", (float)column.propertyDelegate(item, null)));
					EditorGUIUtility.labelWidth = 0f;
					break;
				case GUIType.Custom:
					column.customGUIDelegate(item, rect);
					break;
                default:
                    throw new ArgumentOutOfRangeException("columnIndex", columnIndex, null);
            }
        }

		item.element.soGO.ApplyModifiedProperties();
		item.element.soMR.ApplyModifiedProperties();
    }

	protected override void SelectionChanged(IList<int> selectedIDs)
	{
		Selection.instanceIDs = selectedIDs.ToArray();
	}

    protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
    {
        var mrItem = (MeshRendererTableItem) item;

        return mrItem.element.name.Contains(search);
    }
    
	private void OnVisibleColumnChanged(MultiColumnHeader header)
    {
        Reload();
    }
	
    private void SortItems(MultiColumnHeader multiColumnHeader)
    {
        int index = multiColumnHeader.sortedColumnIndex;
		if (index < 0) return;
        //SessionState.SetInt(sortedColumnIndexStateKey, index);
        var ascending = multiColumnHeader.IsSortedAscending(index);

        if (m_Items == null) return;
		var items = m_Items.Cast<MeshRendererTableItem>();
		MRColumn column = (MRColumn)multiColumnHeader.GetColumn(index);
		
        IOrderedEnumerable<MeshRendererTableItem> orderedEnumerable;

		if (column == null) return;
		orderedEnumerable = items.OrderBy(item => column.propertyDelegate(item, null));
        
        items = orderedEnumerable.AsEnumerable();

        if (!ascending)
        {
            items = items.Reverse();
        }

        m_Items = items.Cast<TreeViewItem>().ToList();
		Reload();
	}
}

}// namespace MomomaAssets
