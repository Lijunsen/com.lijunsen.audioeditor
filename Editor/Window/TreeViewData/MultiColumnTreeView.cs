using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;


namespace ypzxAudioEditor.Utility
{
    public class MultiColumnTreeView : TreeViewWithTreeModel<MyTreeElement>
    {
        const float kRowHeights = 20f;
        const float kToggleWidth = 18f;
        public bool showControls = true;
        public bool disableToggle = false;
        public bool disableRename = false;
        private  List<int> DraggedItemIDs = new List<int>();
        public event Action<int, string> TreeViewItemRenameEnd;
        public event Action TreeViewItemSelectionChange;
        public event Func<List<int>, TreeViewItem, bool> CheckDragItems; 
        public event Action<List<int>, int, int> TreeViewItemDrag;
        public event Action<List<string>, int, int> OutsideResourceDragIn;
        public event Action TreeViewNameChange;
        public event Action<MyTreeElement> TreeElementEnableChange;
        public event Action TreeViewContextClickedItem;

        public event Action DoubleSelectedItem;
        // All columns
        enum MyColumns
        {
            Toggle,
            Name,
        }


        public static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
        {
            if (root == null)
                throw new NullReferenceException("root");
            if (result == null)
                throw new NullReferenceException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                result.Add(current);

                if (current.hasChildren && current.children[0] != null)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.children[i]);
                    }
                }
            }
        }

        public MultiColumnTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<MyTreeElement> model) : base(state, multicolumnHeader, model)
        {
            //		Assert.AreEqual(m_SortOptions.Length , Enum.GetValues(typeof(MyColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

            // Custom setup
            rowHeight = kRowHeights;
            columnIndexForTreeFoldouts = 1;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (kRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
            extraSpaceBeforeIconAndLabel = kToggleWidth;
            //multicolumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }


        // Note we We only build the visible rows, only the backend has the full tree information. 
        // The treeview only creates info for the row list.
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            var rows = base.BuildRows(root);
            //SortIfNeeded (root, rows);
            return rows;
        }

        //void OnSortingChanged (MultiColumnHeader multiColumnHeader)
        //{
        //	SortIfNeeded (rootItem, GetRows());
        //}

        //void SortIfNeeded (TreeViewItem root, IList<TreeViewItem> rows)
        //{
        //	if (rows.Count <= 1)
        //		return;

        //	if (multiColumnHeader.sortedColumnIndex == -1)
        //	{
        //		return; // No column to sort for (just use the order the data are in)
        //	}

        //	// Sort the roots of the existing tree items
        //	SortByMultipleColumns ();
        //	TreeToList(root, rows);
        //	Repaint();
        //}

        //void SortByMultipleColumns ()
        //{
        //	var sortedColumns = multiColumnHeader.state.sortedColumns;

        //	if (sortedColumns.Length == 0)
        //		return;

        //	var myTypes = rootItem.children.Cast<TreeViewItem<MyTreeElement> >();
        //	var orderedQuery = InitialOrder (myTypes, sortedColumns);
        //	for (int i=1; i<sortedColumns.Length; i++)
        //	{
        //		SortOption sortOption = m_SortOptions[sortedColumns[i]];
        //		bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

        //		switch (sortOption)
        //		{
        //			case SortOption.Name:
        //				orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
        //				break;
        //			case SortOption.Value1:
        //				orderedQuery = orderedQuery.ThenBy(l => l.data.floatValue1, ascending);
        //				break;
        //			case SortOption.Value2:
        //				orderedQuery = orderedQuery.ThenBy(l => l.data.floatValue2, ascending);
        //				break;
        //			case SortOption.Value3:
        //				orderedQuery = orderedQuery.ThenBy(l => l.data.floatValue3, ascending);
        //				break;
        //		}
        //	}

        //	rootItem.children = orderedQuery.Cast<TreeViewItem> ().ToList ();
        //}

        //IOrderedEnumerable<TreeViewItem<MyTreeElement>> InitialOrder(IEnumerable<TreeViewItem<MyTreeElement>> myTypes, int[] history)
        //{
        //	SortOption sortOption = m_SortOptions[history[0]];
        //	bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
        //	switch (sortOption)
        //	{
        //		case SortOption.Name:
        //			return myTypes.Order(l => l.data.name, ascending);
        //		case SortOption.Value1:
        //			return myTypes.Order(l => l.data.floatValue1, ascending);
        //		case SortOption.Value2:
        //			return myTypes.Order(l => l.data.floatValue2, ascending);
        //		case SortOption.Value3:
        //			return myTypes.Order(l => l.data.floatValue3, ascending);
        //		default:
        //			Assert.IsTrue(false, "Unhandled enum");
        //			break;
        //	}

        //	// default
        //	return myTypes.Order(l => l.data.name, ascending);
        //}

        //int GetIcon1Index(TreeViewItem<MyTreeElement> item)
        //{
        //	return (int)(Mathf.Min(0.99f, item.data.floatValue1) * s_TestIcons.Length);
        //}

        //int GetIcon2Index (TreeViewItem<MyTreeElement> item)
        //{
        //	return Mathf.Min(item.data.text.Length, s_TestIcons.Length-1);
        //}

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (TreeViewItem<MyTreeElement>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItem<MyTreeElement> item, MyColumns column, ref RowGUIArgs args)
        {
            // 使用 EditorGUIUtility.singleLineHeight 垂直居中单元格。
            // 这样可以更轻松地在单元格中放置控件和图标。
            CenterRectUsingSingleLineHeight(ref cellRect);

            switch (column)
            {
                case MyColumns.Toggle:
                    {
                        EditorGUI.BeginDisabledGroup(disableToggle);
                        var enabled = EditorGUI.Toggle(cellRect, item.data.enabled); // hide when outside cell rect
                        if (enabled != item.data.enabled)
                        {
                            TreeElementEnableChange?.Invoke(item.data);
                            item.data.enabled = enabled;
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    break;

                case MyColumns.Name:
                    {
                        // Do toggle
                        Rect iconRect = cellRect;
                        iconRect.x += GetContentIndent(item);
                        iconRect.width = kToggleWidth;
                        if (iconRect.xMax < cellRect.xMax)
                            GUI.DrawTexture(iconRect, IconUtility.GetIconTexture(item.data.Type), ScaleMode.ScaleToFit);
                        // Default icon and label
                        args.rowRect = cellRect;
                        base.RowGUI(args);
                    }
                    break;
            }
        }

        // Rename
        //--------


        protected override bool CanRename(TreeViewItem item)
        {
            if (disableRename)
            {
                return false;
            }
            // Only allow rename if we can show the rename overlay with a certain width (label might be clipped by other columns)
            Rect renameRect = GetRenameRect(treeViewRect, 0, item);
            return renameRect.width > 30;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            // Set the backend name and reload the tree to reflect the new model
            if (args.acceptedRename)
            {
                if (args.newName != string.Empty && args.newName != args.originalName)
                {
                    TreeViewNameChange?.Invoke();
                    var element = treeModel.Find(args.itemID);
                    element.name = args.newName;
                    TreeViewItemRenameEnd?.Invoke(args.itemID,args.newName);
                }
                Reload();
            }
        }

        protected override Rect GetRenameRect(Rect rowRect, int row, TreeViewItem item)
        {
            Rect cellRect = GetCellRectForTreeFoldouts(rowRect);
            CenterRectUsingSingleLineHeight(ref cellRect);
            return base.GetRenameRect(cellRect, row, item);
        }

        // Misc
        //--------



        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState(float treeViewWidth = 0)
        {
            var columns = new[]
            {

                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent(EditorGUIUtility.FindTexture("FilterByType")),
                    contextMenuText = "Type",
                    headerTextAlignment = TextAlignment.Center,
                    canSort =  false,
                    width = 20,
                    minWidth = 20,
                    maxWidth = 40,
                    autoResize = false,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name"),
                    headerTextAlignment = TextAlignment.Center,
                    canSort =  false,
                    width = 200,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                }
            };

            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            var state = new MultiColumnHeaderState(columns);
            return state;
        }

        #region 自定义


        protected override bool CanMultiSelect(TreeViewItem item)
        {
            // if ((item as TreeViewItem<MyTreeElement>).data.Type == AEComponentType.Switch ||
            //     (item as TreeViewItem<MyTreeElement>).data.Type == AEComponentType.State)
            // {
            //     return false;
            // }
            return true;
        }

        protected override void SelectionChanged(IList<int> selectedIds)
        {
            base.SelectionChanged(selectedIds);
           // Debug.Log("SelectionChanged");
            TreeViewItemSelectionChange?.Invoke();
            Event.current.Use();
        }

        protected override void ContextClickedItem(int id)
        {
            base.ContextClickedItem(id);
            TreeViewContextClickedItem?.Invoke();
        }

        protected override void KeyEvent()
        {
            base.KeyEvent();
        }

        protected override void DoubleClickedItem(int id)
        {
            DoubleSelectedItem?.Invoke();
            base.DoubleClickedItem(id);
        }
        

        protected override bool CanBeParent(TreeViewItem item)
        {
            var element = item as TreeViewItem<MyTreeElement>;
            if (element.data.Type == AEComponentType.SoundSFX
                || element.data.Type == AEComponentType.Event
                || element.data.Type == AEComponentType.SwitchGroup
                || element.data.Type == AEComponentType.StateGroup
                || element.data.Type == AEComponentType.GameParameter
                || element.data.Type == AEComponentType.Switch
                || element.data.Type == AEComponentType.State)
            {
                return false;
            }
            return true;
        }

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            var data = (args.draggedItem as TreeViewItem<MyTreeElement>).data;
            if (data.Type == AEComponentType.Switch || data.Type == AEComponentType.State)
            {
                return false;
            }
            return base.CanStartDrag(args);
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            if (hasSearch)
                return;
            DraggedItemIDs.Clear();
            DraggedItemIDs.AddRange(args.draggedItemIDs as List<int> ?? throw new InvalidOperationException());
            base.SetupDragAndDrop(args);
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (CheckDragItems?.Invoke(DraggedItemIDs, args.parentItem) == false)
            {
                return DragAndDropVisualMode.Rejected;
            }
            DragAndDropVisualMode visualMode;
            if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0)
            {
                //  Debug.Log("HandleDragAndDropPaths");
                if (hasSearch) visualMode = DragAndDropVisualMode.Rejected;
                else visualMode = HandleDragAndDropPaths(args);
            }
            else
            {
                //Debug.Log("HandleDragAndDropItems");
                visualMode = HandleDragAndDropItems(args);
            }
            return visualMode;
        }
        /// <summary>
        /// 对外部拖动进来的数据进行处理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private DragAndDropVisualMode HandleDragAndDropPaths(DragAndDropArgs args)
        {
            DragAndDropVisualMode visualMode = DragAndDropVisualMode.None;
            var resourcePaths = new List<string>();

            foreach (var p in DragAndDrop.paths)
            {
                //  Debug.Log(AssetDatabase.GetMainAssetTypeAtPath(p).ToString());
                if (AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(AudioClip))
                {
                    resourcePaths.Add(p);
                }
            }
            if (resourcePaths.Count == 0)
            {
                visualMode = DragAndDropVisualMode.Rejected;
            }
            else
            {
                visualMode = DragAndDropVisualMode.Copy;
            }

            if (args.performDrop)
            {
              //  Debug.Log("拖入的数量为："+resourcePaths.Count);
                OutsideResourceDragIn?.Invoke(resourcePaths, args.parentItem?.id ?? rootItem.id, args.insertAtIndex);
                //DragAndDrop.AcceptDrag();
            }
            return visualMode;
        }
        /// <summary>
        /// 对TreeViewItem的拖动行为进行处理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private DragAndDropVisualMode HandleDragAndDropItems(DragAndDropArgs args)
        {
            if (args.performDrop)
            {

                int insertPosition = 0;
                if (args.dragAndDropPosition == DragAndDropPosition.BetweenItems)
                {
                    insertPosition = args.insertAtIndex;
                }
                var id = args.parentItem == null ? 0 : args.parentItem.id;
                TreeViewItemDrag?.Invoke(DraggedItemIDs, id, insertPosition);
            }
            return base.HandleDragAndDrop(args);
        }

        #endregion
    }







    static class MyExtensionMethods
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            else
            {
                return source.OrderByDescending(selector);
            }
        }

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            else
            {
                return source.ThenByDescending(selector);
            }
        }
    }
}
