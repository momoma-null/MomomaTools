using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace MomomaAssets
{
    abstract public class UnityObjectTreeViewItem : TreeViewItem
    {
        abstract public SerializedObject serializedObject { get; }
        protected UnityObjectTreeViewItem(int id) : base(id) { }
        ~UnityObjectTreeViewItem() => serializedObject?.Dispose();
    }

    public class MultiColumnHeaderMaker<T> where T : UnityObjectTreeViewItem
    {
        List<MultiColumn<T>> columns = new List<MultiColumn<T>>();

        public void Add(string name, float width, Func<T, SerializedProperty> GetProperty)
        {
            Add(name, width, null, null, GetProperty, null, 0);
        }

        public void Add(string name, float width, Func<T, SerializedProperty> GetProperty, Func<T, bool> IsVisible)
        {
            Add(name, width, null, null, GetProperty, IsVisible, 0);
        }

        public void Add(string name, float width, Func<T, object> GetValue)
        {
            Add(name, width, GetValue, null, null, null, 0);
        }

        public void Add(string name, float width, Func<T, object> GetValue, TextAlignment fieldAlignment)
        {
            Add(name, width, GetValue, null, null, null, fieldAlignment);
        }

        public void Add(string name, float width, Func<T, object> GetValue, Action<T, object> SetValue, Func<T, SerializedProperty> GetProperty)
        {
            Add(name, width, GetValue, SetValue, GetProperty, null, 0);
        }

        public void Add(string name, float width, Func<T, object> GetValue, Action<T, object> SetValue, Func<T, SerializedProperty> GetProperty, Func<T, bool> IsVisible)
        {
            Add(name, width, GetValue, SetValue, GetProperty, IsVisible, 0);
        }

        public void Add(string name, float width, Func<T, object> GetValue, Action<T, object> SetValue, Func<T, SerializedProperty> GetProperty, Func<T, bool> IsVisible, TextAlignment fieldAlignment)
        {
            var column = new MultiColumn<T>(GetValue, SetValue, GetProperty, IsVisible, fieldAlignment)
            {
                width = width,
                minWidth = width * 0.5f,
                headerContent = new GUIContent(name),
                autoResize = false,
                allowToggleVisibility = true
            };
            columns.Add(column);
        }

        public MultiColumnHeader GetHeader()
        {
            var header = new MultiColumnHeader(new MultiColumnHeaderState(columns.ToArray()));
            columns.Clear();
            return header;
        }
    }

    public class MultiColumn<T> : MultiColumnHeaderState.Column where T : UnityObjectTreeViewItem
    {
        public readonly Func<T, object> GetValue;
        public readonly Action<T, object> SetValue;
        public readonly Func<T, SerializedProperty> GetProperty;
        public readonly Func<T, bool> IsVisible;
        public readonly TextAlignment fieldAlignment;

        public MultiColumn(Func<T, object> GetValue, Action<T, object> SetValue, Func<T, SerializedProperty> GetProperty, Func<T, bool> IsVisible, TextAlignment fieldAlignment)
        {
            this.GetValue = GetValue;
            this.SetValue = SetValue;
            this.GetProperty = GetProperty;
            this.IsVisible = IsVisible;
            this.fieldAlignment = fieldAlignment;
        }
    }

    public abstract class UnityObjectTreeViewBase : TreeView
    {
        protected UnityObjectTreeViewBase(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader) { }

        public abstract void OnHierarchyChange();
    }

    public class UnityObjectTreeView<T> : UnityObjectTreeViewBase where T : UnityObjectTreeViewItem
    {
        public UnityObjectTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader, string sortedColumnIndexStateKey, Func<IEnumerable<TreeViewItem>> GetItems, Action<T> ModifiedItem = null, bool canUndo = true) : base(state, multiColumnHeader)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = EditorGUIUtility.singleLineHeight;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
            multiColumnHeader.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, -1);
            this.sortedColumnIndexStateKey = sortedColumnIndexStateKey;
            this.GetItems = GetItems;
            this.ModifiedItem = ModifiedItem;
            this.canUndo = canUndo;
            m_Items = null;
            Reload();
        }

        readonly string sortedColumnIndexStateKey;
        readonly Func<IEnumerable<TreeViewItem>> GetItems;
        readonly Action<T> ModifiedItem;
        readonly bool canUndo;

        List<TreeViewItem> m_Items;

        public override void OnHierarchyChange()
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
                if (GetItems == null)
                    return m_Items;
                m_Items.AddRange(GetItems());
            }
            var rows = hasSearch ? m_Items.FindAll(item => DoesItemMatchSearch(item, searchString)) : new List<TreeViewItem>(m_Items);
            Sort(rows);
            Repaint();
            return rows;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item as T;
            if (!item.serializedObject.targetObject)
                return;

            item.serializedObject.Update();

            var labelStyle = new GUIStyle(args.selected ? EditorStyles.whiteLabel : EditorStyles.label);

            var visibleColumnCount = args.GetNumVisibleColumns();
            for (var visibleColumnIndex = 0; visibleColumnIndex < visibleColumnCount; ++visibleColumnIndex)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                CenterRectUsingSingleLineHeight(ref rect);
                var columnIndex = args.GetColumn(visibleColumnIndex);
                var column = multiColumnHeader.GetColumn(columnIndex) as MultiColumn<T>;

                if (column.GetProperty == null)
                {
                    var content = new GUIContent(column.GetValue(item).ToString());
                    switch (column.fieldAlignment)
                    {
                        case TextAlignment.Left:
                            labelStyle.alignment = TextAnchor.MiddleLeft;
                            break;
                        case TextAlignment.Center:
                            labelStyle.alignment = TextAnchor.MiddleCenter;
                            break;
                        case TextAlignment.Right:
                            labelStyle.alignment = TextAnchor.MiddleRight;
                            break;
                    }
                    EditorGUI.LabelField(rect, content, labelStyle);
                }
                else
                {
                    var sp = column.GetProperty(item);
                    if (column.IsVisible == null || column.IsVisible(item))
                    {
                        if (column.SetValue == null)
                        {
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                EditorGUI.PropertyField(rect, sp, GUIContent.none);
                                if (check.changed)
                                {
                                    CopyToSelection(item.id, sp);
                                }
                            }
                        }
                        else
                        {
                            using (new EditorGUI.PropertyScope(rect, GUIContent.none, sp))
                            using (var check = new EditorGUI.ChangeCheckScope())
                            {
                                object newValue = null;
                                var currentValue = column.GetValue(item);
                                switch (currentValue)
                                {
                                    case bool b:
                                        newValue = EditorGUI.Toggle(rect, b);
                                        break;
                                    case Enum e:
                                        newValue = EditorGUI.EnumPopup(rect, e);
                                        break;
                                    case LayerMask layer:
                                        newValue = EditorGUI.LayerField(rect, layer.value);
                                        break;
                                    default:
                                        throw new InvalidOperationException("column value is unknown type");
                                }
                                if (check.changed)
                                {
                                    column.SetValue(item, newValue);
                                    CopyToSelection(item.id, sp);
                                }
                            }
                        }
                    }
                }
            }
            var isModified = ModifiedItem != null && item.serializedObject.hasModifiedProperties;
            if (canUndo)
                item.serializedObject.ApplyModifiedProperties();
            else
                item.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (isModified)
                ModifiedItem(item);
        }

        void CopyToSelection(int id, SerializedProperty sp)
        {
            var ids = GetSelection();
            if (ids.Contains(id))
            {
                var rows = FindRows(ids);
                foreach (T r in rows)
                {
                    if (r.id == id)
                        continue;
                    var so = r.serializedObject;
                    so.Update();
                    so.CopyFromSerializedPropertyIfDifferent(sp);
                    var isModified = ModifiedItem != null && so.hasModifiedProperties;
                    if (canUndo)
                        so.ApplyModifiedProperties();
                    else
                        so.ApplyModifiedPropertiesWithoutUndo();
                    if (isModified)
                        ModifiedItem(r);
                }
            }
        }

        protected override void SelectionChanged(IList<int> selectedIDs)
        {
            Selection.instanceIDs = selectedIDs.ToArray();
        }

        protected override void SearchChanged(string newSearch)
        {
            Reload();
        }

        void OnVisibleColumnChanged(MultiColumnHeader header)
        {
            Reload();
        }

        void OnSortingChanged(MultiColumnHeader header)
        {
            Reload();
        }

        void Sort(List<TreeViewItem> rows)
        {
            var index = multiColumnHeader.sortedColumnIndex;
            if (index < 0)
                return;
            SessionState.SetInt(sortedColumnIndexStateKey, index);
            if (rows == null || rows.Count == 0)
                return;
            var column = multiColumnHeader.GetColumn(index) as MultiColumn<T>;
            do
            {
                var row = rows[0] as T;
                if (column.GetValue != null)
                {
                    var value = column.GetValue(row);
                    if (value is IComparable)
                    {
                        rows.Sort((x, y) => (column.GetValue(x as T) as IComparable).CompareTo(column.GetValue(y as T)));
                        break;
                    }
                }
                var sp = column.GetProperty(row);
                switch (sp.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        rows.Sort((x, y) => column.GetProperty(x as T).boolValue.CompareTo(column.GetProperty(y as T).boolValue));
                        break;
                    case SerializedPropertyType.Float:
                        rows.Sort((x, y) => column.GetProperty(x as T).floatValue.CompareTo(column.GetProperty(y as T).floatValue));
                        break;
                    case SerializedPropertyType.Integer:
                        rows.Sort((x, y) => column.GetProperty(x as T).intValue.CompareTo(column.GetProperty(y as T).intValue));
                        break;
                    case SerializedPropertyType.ObjectReference:
                        rows.Sort((x, y) => (column.GetProperty(x as T).objectReferenceValue?.name ?? string.Empty).CompareTo((column.GetProperty(y as T).objectReferenceValue?.name ?? string.Empty)));
                        break;
                    case SerializedPropertyType.Enum:
                        rows.Sort((x, y) => column.GetProperty(x as T).enumValueIndex.CompareTo(column.GetProperty(y as T).enumValueIndex));
                        break;
                    default:
                        throw new InvalidOperationException("column property is unknown type");
                }
            }
            while (false);
            if (!multiColumnHeader.IsSortedAscending(index))
                rows.Reverse();
        }
    }

}// namespace MomomaAssets
