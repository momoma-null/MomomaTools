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

    public sealed class MultiColumnHeaderMaker<T> where T : UnityObjectTreeViewItem
    {
        List<MultiColumnHeaderState.Column> columns = new List<MultiColumnHeaderState.Column>();

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty)
            => Add(name, width, getValue, null, getProperty, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible)
            => Add(name, width, getValue, null, getProperty, isVisible, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue)
            => Add(name, width, getValue, null, null, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, TextAlignment fieldAlignment)
            => Add(name, width, getValue, null, null, null, fieldAlignment);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<T, TValue> setValue, Func<T, SerializedProperty> GetProperty)
            => Add(name, width, getValue, setValue, GetProperty, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<T, TValue> setValue, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible)
            => Add(name, width, getValue, setValue, getProperty, isVisible, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<T, TValue> setValue, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible, TextAlignment fieldAlignment)
        {
            var column = new MultiColumn<T, TValue>(getValue, setValue, getProperty, isVisible, fieldAlignment)
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

    sealed class MultiColumn<T, TValue> : MultiColumnHeaderState.Column, IMultiColumn<T> where T : UnityObjectTreeViewItem
    {
        public Func<T, TValue> GetValue { get; }
        public Action<T, TValue> SetValue { get; }
        public Func<T, SerializedProperty> GetProperty { get; }
        public Func<T, bool> IsVisible { get; }
        public TextAnchor FieldAlignment { get; }
        public bool HasCustomGUI { get; }
        public Comparison<T> Comparison { get; }

        public MultiColumn(Func<T, TValue> getValue, Action<T, TValue> setValue, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible, TextAlignment fieldAlignment)
        {
            if (getValue == null)
                throw new ArgumentNullException(nameof(getValue));
            GetValue = getValue;
            SetValue = setValue;
            GetProperty = getProperty;
            IsVisible = isVisible;
            switch (fieldAlignment)
            {
                case TextAlignment.Left:
                    FieldAlignment = TextAnchor.MiddleLeft; break;
                case TextAlignment.Center:
                    FieldAlignment = TextAnchor.MiddleCenter; break;
                case TextAlignment.Right:
                    FieldAlignment = TextAnchor.MiddleRight; break;
            }
            HasCustomGUI = SetValue != null && GetValue != null;
            if (typeof(TValue) == typeof(LayerMask))
                Comparison = (x, y) => (Comparer<LayerMask>.Create((z, w) => z.value.CompareTo(w.value)) as IComparer<TValue>).Compare(GetValue(x), GetValue(y));
            else
                Comparison = (x, y) => Comparer<TValue>.Default.Compare(GetValue(x), GetValue(y));
        }
    }

    interface IMultiColumn<T> where T : UnityObjectTreeViewItem
    {
        bool HasCustomGUI { get; }
        Func<T, SerializedProperty> GetProperty { get; }
        Func<T, bool> IsVisible { get; }
        TextAnchor FieldAlignment { get; }
        Comparison<T> Comparison { get; }
    }

    public abstract class UnityObjectTreeViewBase : TreeView
    {
        protected UnityObjectTreeViewBase(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader) { }

        public abstract void OnHierarchyChange();
    }

    public sealed class UnityObjectTreeView<T> : UnityObjectTreeViewBase where T : UnityObjectTreeViewItem
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
                var column = multiColumnHeader.GetColumn(columnIndex) as IMultiColumn<T>;

                if (column.GetProperty == null)
                {
                    if (column is MultiColumn<T, string> nameColumn)
                    {
                        labelStyle.alignment = column.FieldAlignment;
                        EditorGUI.LabelField(rect, nameColumn.GetValue(item), labelStyle);
                    }
                }
                else
                {
                    var sp = column.GetProperty(item);
                    if (column.IsVisible == null || column.IsVisible(item))
                    {
                        if (!column.HasCustomGUI)
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
                                switch (column)
                                {
                                    case MultiColumn<T, bool> boolColumn:
                                        var newBool = EditorGUI.Toggle(rect, boolColumn.GetValue(item));
                                        if (check.changed)
                                        {
                                            boolColumn.SetValue(item, newBool);
                                            CopyToSelection(item.id, sp);
                                        }
                                        break;
                                    case MultiColumn<T, Enum> enumColumn:
                                        var newEnum = EditorGUI.EnumPopup(rect, enumColumn.GetValue(item));
                                        if (check.changed)
                                        {
                                            enumColumn.SetValue(item, newEnum);
                                            CopyToSelection(item.id, sp);
                                        }
                                        break;
                                    case MultiColumn<T, LayerMask> layerColumn:
                                        var newLayer = EditorGUI.LayerField(rect, layerColumn.GetValue(item));
                                        if (check.changed)
                                        {
                                            layerColumn.SetValue(item, newLayer);
                                            CopyToSelection(item.id, sp);
                                        }
                                        break;
                                    default:
                                        throw new InvalidOperationException("column value is unknown type");
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
            var column = multiColumnHeader.GetColumn(index) as IMultiColumn<T>;
            rows.Sort((x, y) => column.Comparison(x as T, y as T));
            if (!multiColumnHeader.IsSortedAscending(index))
                rows.Reverse();
        }
    }

}// namespace MomomaAssets
