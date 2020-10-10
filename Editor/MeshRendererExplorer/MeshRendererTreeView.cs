using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MomomaAssets
{

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
	public SerializedProperty m_StaticEditorFlags;
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

	public void AddtoList(string name, float width, MeshRendererColumn.Property propertyDelegate)
	{
		var column = new MeshRendererColumn
		{
			width = width,
    	    headerContent = new GUIContent(name),
			propertyDelegate = propertyDelegate
		};
		columns.Add(column);
	}

	public void AddtoList(string name, float width, MeshRendererColumn.GetValue getValueDelegate, MeshRendererColumn.SetValue setValueDelegate = null, MeshRendererColumn.Property propertyDelegate = null)
	{
		var column = new MeshRendererColumn
		{
			width = width,
    	    headerContent = new GUIContent(name),
			setValueDelegate = setValueDelegate,
			getValueDelegate = getValueDelegate,
			propertyDelegate = propertyDelegate
		};
		columns.Add(column);
	}

	public void SetList()
	{
		state = new MultiColumnHeaderState(columns.ToArray());
	}
}

public class MeshRendererColumn : MultiColumnHeaderState.Column
{
	public delegate SerializedProperty Property(MeshRendererTableItem mrItem);
	public delegate void SetValue(MeshRendererTableItem mrItem, object setObj);
	public delegate object GetValue(MeshRendererTableItem mrItem);
	public Property propertyDelegate;
	public SetValue setValueDelegate;
	public GetValue getValueDelegate;
}

public class MeshRendererTreeView : TreeView
{
    public MeshRendererTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader, string sortedColumnIndexStateKey, bool lightmapStaticOnly = false, GameObject rootObj = null) : base(state, multiColumnHeader)
    {
        rowHeight = 16;
        showAlternatingRowBackgrounds = true;
		multiColumnHeader.sortingChanged += SortItems;
		multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
		multiColumnHeader.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, -1);
		this.sortedColumnIndexStateKey = sortedColumnIndexStateKey;
		this.lightmapStaticOnly = lightmapStaticOnly;
        this.m_RootGO = rootObj;

        multiColumnHeader.ResizeToFit();
        Reload();
    }

	private List<TreeViewItem> m_Items;
	private List<TreeViewItem> m_SearchedItems;
	private GameObject m_RootGO;
	private readonly bool lightmapStaticOnly;
	private readonly string sortedColumnIndexStateKey;

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
			
			var scene = SceneManager.GetActiveScene();
			if (scene.IsValid())
			{
        		var meshRendererArray = new List<MeshRenderer>();

				if (m_RootGO)
				{
					if (m_RootGO.activeInHierarchy)
					{
						meshRendererArray.AddRange(m_RootGO.GetComponentsInChildren<MeshRenderer>());
					}
				}else
				{
        			var rootObjs = scene.GetRootGameObjects();
		
        			foreach (var rootObj in rootObjs)
        			{
            			if(!rootObj.activeInHierarchy) continue;
						meshRendererArray.AddRange(rootObj.GetComponentsInChildren<MeshRenderer>());
        			}
				}
        
				foreach (var child in meshRendererArray)
        		{
	       			if(lightmapStaticOnly
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
            var columnIndex = args.GetColumn(visibleColumnIndex);
			var column = (MeshRendererColumn)this.multiColumnHeader.GetColumn(columnIndex);

			if (column.propertyDelegate == null && column.setValueDelegate == null)
				EditorGUI.LabelField(rect, column.getValueDelegate(item).ToString(), labelStyle);
			else
			{
				if (column.setValueDelegate == null)
				{
					EditorGUI.BeginChangeCheck();
					var sp = column.propertyDelegate(item);
					EditorGUI.PropertyField(rect, sp, GUIContent.none);
					if (EditorGUI.EndChangeCheck())
					{
						var ids = GetSelection();
						if (ids.Contains(item.id))
						{
							var rows = FindRows(ids);
							foreach (MeshRendererTableItem r in rows)
							{
								if (r.id == item.id)
									continue;
								
								if (sp.serializedObject.targetObject is MeshRenderer)
								{
									r.element.soMR.Update();
									r.element.soMR.CopyFromSerializedProperty(sp);
									r.element.soMR.ApplyModifiedProperties();
								}else
								{
									r.element.soGO.Update();
									r.element.soGO.CopyFromSerializedProperty(sp);
									r.element.soGO.ApplyModifiedProperties();
								}
							}
						}
					}
				}
				else
				{
					EditorGUI.BeginProperty(rect, GUIContent.none, column.propertyDelegate(item));
					EditorGUI.BeginChangeCheck();
					object newValue = null;
					var currentValue = column.getValueDelegate(item);
					if (currentValue is bool)
						newValue = EditorGUI.Toggle(rect, (bool)currentValue);
					else if (currentValue is Enum)
						newValue = EditorGUI.EnumPopup(rect, (Enum)currentValue);
					if (EditorGUI.EndChangeCheck())
					{
						column.setValueDelegate(item, newValue);
						var ids = GetSelection();
						if (ids.Contains(item.id))
						{
							var rows = FindRows(ids);
							foreach (MeshRendererTableItem r in rows)
							{
								if (r.id == item.id)
									continue;

								r.element.soGO.Update();
								r.element.soMR.Update();
								column.setValueDelegate(r, newValue);
								r.element.soGO.ApplyModifiedProperties();
								r.element.soMR.ApplyModifiedProperties();
							}
						}
					}
					EditorGUI.EndProperty();
				}
				
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
        SessionState.SetInt(sortedColumnIndexStateKey, index);
        var ascending = multiColumnHeader.IsSortedAscending(index);

        if (m_Items == null) return;
		var items = m_Items.Cast<MeshRendererTableItem>();
		var column = (MeshRendererColumn)multiColumnHeader.GetColumn(index);
		
        IOrderedEnumerable<MeshRendererTableItem> orderedEnumerable;

		if (column == null) return;
		orderedEnumerable = items.OrderBy(item => 
		{
			if (column.getValueDelegate != null)
				return column.getValueDelegate(item);
			switch (column.propertyDelegate(item).propertyType)
			{
				case SerializedPropertyType.Boolean :
					return column.propertyDelegate(item).boolValue;
				case SerializedPropertyType.Float :
					return column.propertyDelegate(item).floatValue;
				case SerializedPropertyType.ObjectReference :
					return column.propertyDelegate(item).objectReferenceValue ? column.propertyDelegate(item).objectReferenceValue.name : string.Empty;
				case SerializedPropertyType.Enum :
					return column.propertyDelegate(item).enumValueIndex;
				default:
                    throw new ArgumentOutOfRangeException("columnIndex", item, null);
			}
		});
        
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
