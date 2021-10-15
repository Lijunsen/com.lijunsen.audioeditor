using AudioEditor.Editor.Utility;
using AudioEditor.Runtime;
using AudioEditor.Runtime.Utility;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AudioEditor.Editor
{

    internal class SelectPopupWindow : PopupWindowContent
    {
        private AudioEditorData data;
        private WindowOpenFor windowOpenForWhat;

        private MultiColumnHeaderState multiColumnHeaderState;
        private TreeModel<MyTreeElement> treeModel;
        private TreeViewState treeViewState;
        private MultiColumnTreeView treeView;

        private SearchField searchField;
        private bool doubleClickItem;

        private event System.Action<int> CallBackFunction;

        Rect TreeViewRect => new Rect(5, 20, editorWindow.position.width - 10, editorWindow.position.height - 45);

        internal SelectPopupWindow(WindowOpenFor whatTypeOpenToDo, System.Action<int> callback)
        {
            CallBackFunction = callback;
            windowOpenForWhat = whatTypeOpenToDo;
            //data = AssetDatabase.LoadAssetAtPath(AudioEditorManager.DataPath, typeof(AudioEditorData)) as AudioEditorData;
            data = AudioEditorManager.Data;
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(300, 500);
        }

        public override void OnGUI(Rect rect)
        {
            Init();

            treeView.searchString = searchField.OnGUI(treeView.searchString);
            treeView.OnGUI(TreeViewRect);
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("确定", GUILayout.Width(80)) || doubleClickItem)
            {
                if (CheckSelectID())
                {
                    CallBackFunction?.Invoke(treeViewState.lastClickedID);
                    Debug.Log("[AudioEditor]:selected ID:" + treeViewState.lastClickedID);
                    editorWindow.Close();
                }
                else
                {
                    doubleClickItem = false;
                }
            }

            if (GUILayout.Button("取消", GUILayout.Width(80)))
            {
                editorWindow.Close();
            }
            EditorGUILayout.EndHorizontal();

        }

        void Init()
        {
            //直接放在构造函数中TreeViewRect会无法获取position数据而报错
            bool firstInit = multiColumnHeaderState == null;
            if (firstInit)
            {
                var headerState = MultiColumnTreeView.CreateDefaultMultiColumnHeaderState(TreeViewRect.width);
                if (MultiColumnHeaderState.CanOverwriteSerializedFields(multiColumnHeaderState, headerState))
                {
                    MultiColumnHeaderState.OverwriteSerializedFields(multiColumnHeaderState, headerState);
                }

                multiColumnHeaderState = headerState;
                var myMultiColumnHeader = new MultiColumnHeader(headerState) { height = 20 };
                treeViewState = new TreeViewState();
                switch (windowOpenForWhat)
                {
                    case WindowOpenFor.SelectAudio:
                        treeModel = new TreeModel<MyTreeElement>(data.myTreeLists[0]);
                        break;
                    case WindowOpenFor.SelectEvent:
                        treeModel = new TreeModel<MyTreeElement>(data.myTreeLists[1]);
                        break;
                    case WindowOpenFor.SelectSwitch:
                    case WindowOpenFor.SelectState:
                    case WindowOpenFor.SelectGameParameter:
                    case WindowOpenFor.SelectSwichGroup:
                        treeModel = new TreeModel<MyTreeElement>(data.myTreeLists[2]);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                treeView = new MultiColumnTreeView(treeViewState, myMultiColumnHeader, treeModel);

                searchField = new SearchField();
                searchField.downOrUpArrowKeyPressed += treeView.SetFocusAndEnsureSelectedItem;
                treeView.DoubleSelectedItem += DoubleClick;
                treeView.CheckDragItems += DragItems;
                treeView.ExpandAll();

                treeView.disableToggle = true;
                treeView.disableRename = true;
            }
        }

        /// <summary>
        /// 确认当前选择是否符合条件
        /// </summary>
        /// <returns></returns>
        private bool CheckSelectID()
        {
            //排除根id(当什么都不选就确定时就会默认为根)以及排除work unit的id
            if (treeViewState.lastClickedID == 0) return false;
            switch (windowOpenForWhat)
            {
                case WindowOpenFor.SelectAudio:
                    if (AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(treeViewState.lastClickedID) != null) return true;
                    break;
                case WindowOpenFor.SelectEvent:
                    if (AudioEditorManager.GetAEComponentDataByID<AEEvent>(treeViewState.lastClickedID) != null) return true;
                    break;
                case WindowOpenFor.SelectSwitch:
                    if (AudioEditorManager.GetAEComponentDataByID<Switch>(treeViewState.lastClickedID) != null) return true;
                    break;
                case WindowOpenFor.SelectState:
                    if (AudioEditorManager.GetAEComponentDataByID<State>(treeViewState.lastClickedID) != null) return true;
                    break;
                case WindowOpenFor.SelectGameParameter:
                    if (AudioEditorManager.GetAEComponentDataByID<GameParameter>(treeViewState.lastClickedID) != null) return true;
                    break;
                case WindowOpenFor.SelectSwichGroup:
                    if (AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(treeViewState.lastClickedID) != null) return true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return false;
        }

        private void DoubleClick()
        {
            doubleClickItem = true;
        }

        private bool DragItems(List<int> daragItems, TreeViewItem parentItem)
        {
            return false;
        }

        public override void OnClose()
        {
            treeView.DoubleSelectedItem -= DoubleClick;
            treeView.CheckDragItems -= DragItems;

            data = null;
            multiColumnHeaderState = null;
            treeModel = null;
            treeViewState = null;
            treeView = null;
            searchField = null;
        }

    }

    internal enum WindowOpenFor
    {
        SelectAudio,
        SelectEvent,
        SelectSwichGroup,
        SelectSwitch,
        SelectState,
        SelectGameParameter
    }
}
