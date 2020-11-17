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

        public void AddtoList(string name, float width, Func<T, SerializedProperty> GetProperty)
        {
            AddtoList(name, width, null, null, GetProperty);
        }

        public void AddtoList(string name, float width, Func<T, object> GetValue, Action<T, object> SetValue = null, Func<T, SerializedProperty> GetProperty = null)
        {
            var column = new MultiColumn<T>(GetValue, SetValue, GetProperty)
            {
                width = width,
                headerContent = new GUIContent(name)
            };
            columns.Add(column);
        }

        public MultiColumnHeader GetHeader()
        {
            return new MultiColumnHeader(new MultiColumnHeaderState(columns.ToArray()));
        }
    }

    public class MultiColumn<T> : MultiColumnHeaderState.Column where T : UnityObjectTreeViewItem
    {
        public readonly Func<T, object> GetValue;
        public readonly Action<T, object> SetValue;
        public readonly Func<T, SerializedProperty> GetProperty;

        public MultiColumn(Func<T, object> GetValue, Action<T, object> SetValue, Func<T, SerializedProperty> GetProperty)
        {
            this.GetValue = GetValue;
            this.SetValue = SetValue;
            this.GetProperty = GetProperty;
        }
    }

    interface IFullReloadTreeView
    {
        void FullReload();
    }

    public class UnityObjectTreeView<T> : TreeView, IFullReloadTreeView where T : UnityObjectTreeViewItem
    {
        public UnityObjectTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader, string sortedColumnIndexStateKey, Func<IEnumerable<TreeViewItem>> GetItems) : base(state, multiColumnHeader)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = EditorGUIUtility.singleLineHeight;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
            multiColumnHeader.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, -1);
            this.sortedColumnIndexStateKey = sortedColumnIndexStateKey;
            this.GetItems = GetItems;
            m_Items = null;

            multiColumnHeader.ResizeToFit();
            Reload();
        }

        readonly string sortedColumnIndexStateKey;
        readonly Func<IEnumerable<TreeViewItem>> GetItems;

        List<TreeViewItem> m_Items;

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
                if (GetItems == null)
                    return m_Items;
                m_Items.AddRange(GetItems());
            }
            IEnumerable<TreeViewItem> tempItems = m_Items;
            if (hasSearch)
                tempItems = m_Items.Where(item => DoesItemMatchSearch(item, searchString));
            var rows = tempItems.ToList();
            Sort(rows, multiColumnHeader);
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
            labelStyle.alignment = TextAnchor.MiddleLeft;

            var visibleColumnCount = args.GetNumVisibleColumns();
            for (var visibleColumnIndex = 0; visibleColumnIndex < visibleColumnCount; ++visibleColumnIndex)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                CenterRectUsingSingleLineHeight(ref rect);
                var columnIndex = args.GetColumn(visibleColumnIndex);
                var column = multiColumnHeader.GetColumn(columnIndex) as MultiColumn<T>;

                if (column.GetProperty == null)
                    EditorGUI.LabelField(rect, column.GetValue(item).ToString(), labelStyle);
                else
                {
                    var sp = column.GetProperty(item);
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
            item.serializedObject.ApplyModifiedProperties();
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
                    so.ApplyModifiedProperties();
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

        void Sort(List<TreeViewItem> rows, MultiColumnHeader multiColumnHeader)
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
