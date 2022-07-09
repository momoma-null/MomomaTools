using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Column = UnityEditor.IMGUI.Controls.MultiColumnHeaderState.Column;

namespace MomomaAssets
{
    abstract public class UnityObjectTreeViewItem : TreeViewItem
    {
        abstract public SerializedObject serializedObject { get; }
        protected UnityObjectTreeViewItem(int id) : base(id) { }
        ~UnityObjectTreeViewItem() => serializedObject?.Dispose();
    }

    public sealed class ColumnArray<T> where T : UnityObjectTreeViewItem
    {
        readonly List<Column> columns = new List<Column>();
        readonly static Func<SerializedProperty, T, bool> defaultCopyProperty = (from, to) => to.serializedObject.CopyFromSerializedPropertyIfDifferent(from);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty)
            => Add(name, width, getValue, null, getProperty, defaultCopyProperty, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible)
            => Add(name, width, getValue, null, getProperty, defaultCopyProperty, isVisible, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue)
            => Add(name, width, getValue, null, null, null, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, TextAlignment fieldAlignment)
            => Add(name, width, getValue, null, null, null, null, fieldAlignment);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<Rect, T> onGUI, Func<T, SerializedProperty> getProperty, Func<SerializedProperty, T, bool> copyProperty)
            => Add(name, width, getValue, onGUI, getProperty, copyProperty, null, TextAlignment.Left);

        public void AddIntAsEnum<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty) where TValue : Enum
            => Add(name, width, getValue, (r, item) => getProperty(item).EnumFieldFromInt<TValue>(r), getProperty, defaultCopyProperty, null, TextAlignment.Left);

        public void AddIntAsLayerMask(string name, float width, Func<T, SerializedProperty> getProperty)
            => Add(name, width, item => getProperty(item).intValue, (r, item) => getProperty(item).LayerFieldFromInt(r), getProperty, defaultCopyProperty, null, TextAlignment.Left);

        public void AddIntAsToggle(string name, float width, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible = null)
            => Add(name, width, item => Convert.ToBoolean(getProperty(item).intValue), (r, item) => getProperty(item).ToggleFieldFromInt(r), getProperty, defaultCopyProperty, isVisible, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<Rect, T> onGUI, Func<T, SerializedProperty> getProperty, Func<SerializedProperty, T, bool> copyProperty, Func<T, bool> isVisible, TextAlignment fieldAlignment)
        {
            var column = new MultiColumn<T, TValue>(getValue, onGUI, getProperty, copyProperty, isVisible, fieldAlignment)
            {
                width = width,
                minWidth = width * 0.5f,
                headerContent = new GUIContent(name),
                autoResize = false,
                allowToggleVisibility = true
            };
            columns.Add(column);
        }

        public MultiColumnHeaderState GetHeaderState() => new MultiColumnHeaderState(columns.ToArray());
    }

    public static class TreeViewItemExtensions
    {
        public static void EnumFieldFromInt<T>(this SerializedProperty property, Rect rect) where T : Enum, IConvertible
        {
            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var newValue = (T)EditorGUI.EnumPopup(rect, (T)(object)property.intValue);
                    if (check.changed)
                        property.intValue = newValue.ToInt32(null);
                }
            }
        }

        public static void LayerFieldFromInt(this SerializedProperty property, Rect rect)
        {
            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var newValue = EditorGUI.LayerField(rect, property.intValue);
                    if (check.changed)
                        property.intValue = newValue;
                }
            }
        }

        public static void ToggleFieldFromInt(this SerializedProperty property, Rect rect)
        {
            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var newValue = EditorGUI.Toggle(rect, Convert.ToBoolean(property.intValue));
                    if (check.changed)
                        property.intValue = Convert.ToInt32(newValue);
                }
            }
        }
    }

    sealed class MultiColumn<T, TValue> : Column, IMultiColumn<T> where T : UnityObjectTreeViewItem
    {
        public Comparison<T> Comparison { get; }

        readonly Func<T, TValue> getValue;
        readonly Func<T, SerializedProperty> getProperty;
        readonly Func<SerializedProperty, T, bool> copyProperty;
        readonly Action<Rect, T> onGUI;
        readonly Func<T, bool> isVisible;
        readonly TextAnchor fieldAlignment;

        public MultiColumn(Func<T, TValue> getValue, Action<Rect, T> onGUI, Func<T, SerializedProperty> getProperty, Func<SerializedProperty, T, bool> copyProperty, Func<T, bool> isVisible, TextAlignment fieldAlignment)
        {
            this.getValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
            this.getProperty = getProperty;
            this.copyProperty = copyProperty;
            this.isVisible = isVisible;
            this.onGUI = onGUI;
            switch (fieldAlignment)
            {
                case TextAlignment.Left:
                    this.fieldAlignment = TextAnchor.MiddleLeft; break;
                case TextAlignment.Center:
                    this.fieldAlignment = TextAnchor.MiddleCenter; break;
                case TextAlignment.Right:
                    this.fieldAlignment = TextAnchor.MiddleRight; break;
            }
            Comparison = (x, y) => Comparer<TValue>.Default.Compare(getValue(x), getValue(y));
        }

        public void DrawField(Rect rect, T item, GUIStyle style)
        {
            if (getProperty == null)
            {
                style.alignment = fieldAlignment;
                EditorGUI.LabelField(rect, getValue(item).ToString(), style);
            }
            else
            {
                if (isVisible == null || isVisible(item))
                {
                    if (onGUI != null)
                    {
                        onGUI(rect, item);
                    }
                    else
                    {
                        EditorGUI.PropertyField(rect, getProperty(item), GUIContent.none);
                    }
                }
            }
        }

        public SerializedProperty GetProperty(T item) => getProperty.Invoke(item);

        public bool CopyProperty(SerializedProperty from, T to) => copyProperty.Invoke(from, to);
    }

    interface IMultiColumn<T> where T : UnityObjectTreeViewItem
    {
        Comparison<T> Comparison { get; }
        void DrawField(Rect rect, T item, GUIStyle style);
        SerializedProperty GetProperty(T item);
        bool CopyProperty(SerializedProperty from, T to);
    }

    public abstract class UnityObjectTreeViewBase : TreeView
    {
        protected UnityObjectTreeViewBase(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader) { }

        public abstract void OnHierarchyChange();
    }

    public sealed class UnityObjectTreeView<T> : UnityObjectTreeViewBase where T : UnityObjectTreeViewItem
    {
        public UnityObjectTreeView(TreeViewState state, MultiColumnHeaderState multiColumnHeaderState, Func<IEnumerable<TreeViewItem>> GetItems, Action<T> ModifiedItem = null, bool canUndo = true) : base(state, new MultiColumnHeader(multiColumnHeaderState))
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = EditorGUIUtility.singleLineHeight;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
            this.GetItems = GetItems;
            this.ModifiedItem = ModifiedItem;
            this.canUndo = canUndo;
            m_Items = null;
            Reload();
        }

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
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    column.DrawField(rect, item, labelStyle);
                    if (check.changed)
                    {
                        var id = item.id;
                        var ids = GetSelection();
                        if (ids.Contains(id))
                        {
                            var sp = column.GetProperty(item);
                            var rows = FindRows(ids);
                            foreach (T r in rows)
                            {
                                if (r.id == id)
                                    continue;
                                var so = r.serializedObject;
                                so.Update();
                                if (column.CopyProperty(sp, r))
                                {
                                    if (canUndo)
                                        so.ApplyModifiedProperties();
                                    else
                                        so.ApplyModifiedPropertiesWithoutUndo();
                                    ModifiedItem?.Invoke(r);
                                }
                            }
                        }
                    }
                }
            }
            if (canUndo ? item.serializedObject.ApplyModifiedProperties() : item.serializedObject.ApplyModifiedPropertiesWithoutUndo())
                ModifiedItem?.Invoke(item);
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
            if (rows == null || rows.Count == 0)
                return;
            var column = multiColumnHeader.GetColumn(index) as IMultiColumn<T>;
            rows.Sort((x, y) => column.Comparison(x as T, y as T));
            if (!multiColumnHeader.IsSortedAscending(index))
                rows.Reverse();
        }
    }

}// namespace MomomaAssets
