using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace MomomaAssets
{

public class MeshRendererElement
{
    public GameObject gameObject { get; private set; }
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
        displayName = mr.name;
    }
}

public class MeshRendererTableHeader : MultiColumnHeader
{
    private List<MultiColumnHeaderState.Column> columns;

    public MeshRendererTableHeader(MultiColumnHeaderState state) : base(state)
    {
        columns = new List<MultiColumnHeaderState.Column>();
    }

    public void AddtoList(string name, float width, Func<MeshRendererTableItem, SerializedProperty> GetProperty)
    {
        var column = new MeshRendererColumn
        {
            width = width,
            headerContent = new GUIContent(name),
            GetProperty = GetProperty
        };
        columns.Add(column);
    }

    public void AddtoList(string name, float width, Func<MeshRendererTableItem, object> GetValue, Action<MeshRendererTableItem, object> SetValue = null, Func<MeshRendererTableItem, SerializedProperty> GetProperty = null)
    {
        var column = new MeshRendererColumn
        {
            width = width,
            headerContent = new GUIContent(name),
            GetValue = GetValue,
            SetValue = SetValue,
            GetProperty = GetProperty
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
    public Func<MeshRendererTableItem, object> GetValue;
    public Action<MeshRendererTableItem, object> SetValue;
    public Func<MeshRendererTableItem, SerializedProperty> GetProperty;
}

public class MeshRendererTreeView : TreeView
{
    public MeshRendererTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader, string sortedColumnIndexStateKey, Func<IEnumerable<UnityEngine.Object>> GetObjects) : base(state, multiColumnHeader)
    {
        showAlternatingRowBackgrounds = true;
        showBorder = true;
        rowHeight = EditorGUIUtility.singleLineHeight;
        multiColumnHeader.sortingChanged += OnSortingChanged;
        multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
        multiColumnHeader.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, -1);
        this.sortedColumnIndexStateKey = sortedColumnIndexStateKey;
        this.GetObjects = GetObjects;

        multiColumnHeader.ResizeToFit();
        Reload();
    }

    private List<TreeViewItem> m_Items;
    private readonly Func<IEnumerable<UnityEngine.Object>> GetObjects;
    private readonly string sortedColumnIndexStateKey;

    public void FullReload()
    {
         m_Items = null;
         Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        return new TreeViewItem(-1, -1, null);
    }

    protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
    {
        if (m_Items == null)
        {
            m_Items = new List<TreeViewItem>();
            if (GetObjects == null)
                return m_Items;
            foreach (MeshRenderer mr in GetObjects())
            {
                m_Items.Add(new MeshRendererTableItem(mr.gameObject.GetInstanceID(), mr));
            }
        }
        SearchFullTree();
        Sort(m_Items, multiColumnHeader);
        Repaint();
        return m_Items;
    }

    private void SearchFullTree()
    {
        if (hasSearch)
            m_Items.RemoveAll(item => !DoesItemMatchSearch(item, searchString));
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        var item = (MeshRendererTableItem)args.item;
        if (!item.element.gameObject)
            return;

        item.element.soGO.Update();
        item.element.soMR.Update();

        var labelStyle = args.selected ? EditorStyles.whiteLabel : EditorStyles.label;
        labelStyle.alignment = TextAnchor.MiddleLeft;

        for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); ++visibleColumnIndex)
        {
            var rect = args.GetCellRect(visibleColumnIndex);
            CenterRectUsingSingleLineHeight(ref rect);
            var columnIndex = args.GetColumn(visibleColumnIndex);
            var column = (MeshRendererColumn)this.multiColumnHeader.GetColumn(columnIndex);

            if (column.GetProperty == null)
                EditorGUI.LabelField(rect, column.GetValue(item).ToString(), labelStyle);
            else
            {
                var sp = column.GetProperty(item);
                if (column.SetValue == null)
                {
                    EditorGUI.BeginChangeCheck();
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
                    EditorGUI.BeginProperty(rect, GUIContent.none, sp);
                    EditorGUI.BeginChangeCheck();
                    object newValue = null;
                    var currentValue = column.GetValue(item);
                    if (currentValue is bool)
                        newValue = EditorGUI.Toggle(rect, (bool)currentValue);
                    else if (currentValue is Enum)
                        newValue = EditorGUI.EnumPopup(rect, (Enum)currentValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        column.SetValue(item, newValue);
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

    protected override void SearchChanged(string newSearch)
    {
        FullReload();
    }

    private void OnVisibleColumnChanged(MultiColumnHeader header)
    {
        Reload();
    }

    void OnSortingChanged(MultiColumnHeader multiColumnHeader)
    {
        FullReload();
    }

    private void Sort(IList<TreeViewItem> rows, MultiColumnHeader multiColumnHeader)
    {
        var index = multiColumnHeader.sortedColumnIndex;
        if (index < 0 || rows == null)
            return;
        SessionState.SetInt(sortedColumnIndexStateKey, index);

        var column = (MeshRendererColumn)multiColumnHeader.GetColumn(index);

        IEnumerable<TreeViewItem> items = rows.OrderBy(item =>
        {
            if (column.GetValue != null)
                return column.GetValue((MeshRendererTableItem)item);
            var sp = column.GetProperty((MeshRendererTableItem)item);
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Boolean :
                    return sp.boolValue;
                case SerializedPropertyType.Float :
                    return sp.floatValue;
                case SerializedPropertyType.ObjectReference :
                    return sp.objectReferenceValue ? sp.objectReferenceValue.name : string.Empty;
                case SerializedPropertyType.Enum :
                    return sp.enumValueIndex;
                default:
                    throw new ArgumentOutOfRangeException("columnIndex", item, null);
            }
        });

        if (!multiColumnHeader.IsSortedAscending(index))
            items = items.Reverse();

        m_Items = items.ToList();
    }
}

}// namespace MomomaAssets
