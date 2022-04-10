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

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty)
            => Add(name, width, getValue, null, getProperty, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible)
            => Add(name, width, getValue, null, getProperty, isVisible, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue)
            => Add(name, width, getValue, null, null, null, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, TextAlignment fieldAlignment)
            => Add(name, width, getValue, null, null, null, fieldAlignment);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<Rect, T> onGUI, Func<T, SerializedProperty> getProperty)
            => Add(name, width, getValue, onGUI, getProperty, null, TextAlignment.Left);

        public void AddIntAsEnum<TValue>(string name, float width, Func<T, TValue> getValue, Func<T, SerializedProperty> getProperty) where TValue : Enum
            => Add(name, width, getValue, (r, item) => getProperty(item).EnumFieldFromInt<TValue>(r), getProperty, null, TextAlignment.Left);

        public void AddIntAsLayerMask(string name, float width, Func<T, LayerMask> getValue, Func<T, SerializedProperty> getProperty)
            => Add(name, width, getValue, (r, item) => getProperty(item).LayerFieldFromInt(r), getProperty, null, TextAlignment.Left);

        public void AddIntAsToggle(string name, float width, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible = null)
            => Add(name, width, x => Convert.ToBoolean(getProperty(x).intValue), (r, item) => getProperty(item).ToggleFieldFromInt(r), getProperty, isVisible, TextAlignment.Left);

        public void Add<TValue>(string name, float width, Func<T, TValue> getValue, Action<Rect, T> onGUI, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible, TextAlignment fieldAlignment)
        {
            var column = new MultiColumn<T, TValue>(getValue, onGUI, getProperty, isVisible, fieldAlignment)
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
        static class EnumCache<T> where T : Enum
        {
            public static string[] Names { get; }

            static EnumCache()
            {
                Names = Enum.GetNames(typeof(T));
            }
        }

        public static void EnumFieldFromInt<T>(this SerializedProperty property, Rect rect) where T : Enum
        {
            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    var newValue = EditorGUI.Popup(rect, property.intValue, EnumCache<T>.Names);
                    if (check.changed)
                        property.intValue = newValue;
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
        public Func<T, TValue> GetValue { get; }
        public Func<T, SerializedProperty> GetProperty { get; }
        public Func<T, bool> IsVisible { get; }
        public TextAnchor FieldAlignment { get; }
        public Comparison<T> Comparison { get; }

        readonly Action<Rect, T> onGUI;

        public MultiColumn(Func<T, TValue> getValue, Action<Rect, T> onGUI, Func<T, SerializedProperty> getProperty, Func<T, bool> isVisible, TextAlignment fieldAlignment)
        {
            GetValue = getValue ?? throw new ArgumentNullException(nameof(getValue));
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
            if (typeof(TValue) == typeof(LayerMask))
                Comparison = (x, y) => (Comparer<LayerMask>.Create((z, w) => z.value.CompareTo(w.value)) as IComparer<TValue>).Compare(GetValue(x), GetValue(y));
            else
                Comparison = (x, y) => Comparer<TValue>.Default.Compare(GetValue(x), GetValue(y));
            if (getProperty != null)
            {
                if (onGUI != null)
                {
                    this.onGUI = onGUI;
                }
                else
                {
                    this.onGUI = OnPropertyGUI;
                }
            }
        }

        public void OnGUI(Rect rect, T item) => onGUI(rect, item);

        void OnPropertyGUI(Rect rect, T item)
        {
            EditorGUI.PropertyField(rect, GetProperty(item), GUIContent.none);
        }

        public string GetLabel(T item) => GetValue(item).ToString();
    }

    interface IMultiColumn<T> where T : UnityObjectTreeViewItem
    {
        Func<T, SerializedProperty> GetProperty { get; }
        Func<T, bool> IsVisible { get; }
        TextAnchor FieldAlignment { get; }
        Comparison<T> Comparison { get; }
        string GetLabel(T item);
        void OnGUI(Rect rect, T item);
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

                if (column.GetProperty == null)
                {
                    labelStyle.alignment = column.FieldAlignment;
                    EditorGUI.LabelField(rect, column.GetLabel(item), labelStyle);
                }
                else
                {
                    var sp = column.GetProperty(item);
                    if (column.IsVisible == null || column.IsVisible(item))
                    {
                        using (var check = new EditorGUI.ChangeCheckScope())
                        {
                            column.OnGUI(rect, item);
                            if (check.changed)
                                CopyToSelection(item.id, sp);
                        }
                    }
                }
            }
            if (canUndo ? item.serializedObject.ApplyModifiedProperties() : item.serializedObject.ApplyModifiedPropertiesWithoutUndo())
                ModifiedItem?.Invoke(item);
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
                    if (so.CopyFromSerializedPropertyIfDifferent(sp))
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
