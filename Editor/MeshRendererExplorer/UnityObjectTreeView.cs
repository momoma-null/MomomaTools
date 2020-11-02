using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace MomomaAssets
{
    abstract public class UnityObjectTreeViewItem : TreeViewItem
    {
        abstract public SerializedObject serializedObject { get; }
        protected UnityObjectTreeViewItem(int id, UnityEngine.Object obj) : base(id) { }
    }

    public class MultiColumnHeaderMaker<T> where T : UnityObjectTreeViewItem
    {
        List<MultiColumn<T>> columns = new List<MultiColumn<T>>();

        public void AddtoList(string name, float width, Func<T, SerializedProperty> GetProperty)
        {
            var column = new MultiColumn<T>
            {
                width = width,
                headerContent = new GUIContent(name),
                GetProperty = GetProperty
            };
            columns.Add(column);
        }

        public void AddtoList(string name, float width, Func<T, object> GetValue, Action<T, object> SetValue = null, Func<T, SerializedProperty> GetProperty = null)
        {
            var column = new MultiColumn<T>
            {
                width = width,
                headerContent = new GUIContent(name),
                GetValue = GetValue,
                SetValue = SetValue,
                GetProperty = GetProperty
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
        public Func<T, object> GetValue;
        public Action<T, object> SetValue;
        public Func<T, SerializedProperty> GetProperty;
    }

    interface IFullReloadTreeView
    {
        void FullReload();
    }

    public class UnityObjectTreeView<T> : TreeView, IFullReloadTreeView where T : UnityObjectTreeViewItem
    {
        public UnityObjectTreeView(TreeViewState state, MultiColumnHeader multiColumnHeader, string sortedColumnIndexStateKey, Func<IEnumerable<UnityEngine.Object>> GetObjects) : base(state, multiColumnHeader)
        {
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = EditorGUIUtility.singleLineHeight;
            multiColumnHeader.sortingChanged += OnSortingChanged;
            multiColumnHeader.visibleColumnsChanged += OnVisibleColumnChanged;
            multiColumnHeader.sortedColumnIndex = SessionState.GetInt(sortedColumnIndexStateKey, -1);
            this.sortedColumnIndexStateKey = sortedColumnIndexStateKey;
            this.GetObjects = GetObjects;
            constructorInfo = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(UnityEngine.Object) }, null);

            multiColumnHeader.ResizeToFit();
            Reload();
        }

        readonly string sortedColumnIndexStateKey;
        readonly Func<IEnumerable<UnityEngine.Object>> GetObjects;
        readonly ConstructorInfo constructorInfo;
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
                if (GetObjects == null || constructorInfo == null)
                    return m_Items;
                foreach (var obj in GetObjects())
                    m_Items.Add((T)constructorInfo.Invoke(new object[] { obj is Component ? ((Component)obj).gameObject.GetInstanceID() : obj.GetInstanceID(), obj }));
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
            var item = (T)args.item;
            if (!item.serializedObject.targetObject)
                return;

            item.serializedObject.Update();

            var labelStyle = new GUIStyle(args.selected ? EditorStyles.whiteLabel : EditorStyles.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;

            for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); ++visibleColumnIndex)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                CenterRectUsingSingleLineHeight(ref rect);
                var columnIndex = args.GetColumn(visibleColumnIndex);
                var column = (MultiColumn<T>)this.multiColumnHeader.GetColumn(columnIndex);

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
                                foreach (T r in rows)
                                {
                                    if (r.id == item.id)
                                        continue;
                                    var so = r.serializedObject;
                                    so.Update();
                                    so.CopyFromSerializedPropertyIfDifferent(sp);
                                    so.ApplyModifiedProperties();
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
                        if (EditorGUI.EndChangeCheck())
                        {
                            column.SetValue(item, newValue);
                            var ids = GetSelection();
                            if (ids.Contains(item.id))
                            {
                                var rows = FindRows(ids);
                                foreach (T r in rows)
                                {
                                    if (r.id == item.id)
                                        continue;
                                    var so = r.serializedObject;
                                    so.Update();
                                    so.CopyFromSerializedPropertyIfDifferent(sp);
                                    so.ApplyModifiedProperties();
                                }
                            }
                        }
                        EditorGUI.EndProperty();
                    }

                }
            }
            item.serializedObject.ApplyModifiedProperties();
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

            var column = (MultiColumn<T>)multiColumnHeader.GetColumn(index);

            IEnumerable<TreeViewItem> items = rows.OrderBy(item =>
            {
                if (column.GetValue != null)
                {
                    var val = column.GetValue((T)item);
                    if (val is IComparable)
                        return val;
                }
                var sp = column.GetProperty((T)item);
                switch (sp.propertyType)
                {
                    case SerializedPropertyType.Boolean:
                        return sp.boolValue;
                    case SerializedPropertyType.Float:
                        return sp.floatValue;
                    case SerializedPropertyType.Integer:
                        return sp.intValue;
                    case SerializedPropertyType.ObjectReference:
                        return sp.objectReferenceValue ? sp.objectReferenceValue.name : string.Empty;
                    case SerializedPropertyType.Enum:
                        return sp.enumValueIndex;
                    default:
                        throw new InvalidOperationException("column property is unknown type");
                }
            });

            if (!multiColumnHeader.IsSortedAscending(index))
                items = items.Reverse();

            m_Items = items.ToList();
        }
    }

}// namespace MomomaAssets
