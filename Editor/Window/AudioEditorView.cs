using AudioEditor.Editor.Utility;
using AudioEditor.Runtime;
using AudioEditor.Runtime.Utility;
using AudioEditor.Runtime.Utility.EasingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AudioEditor.Editor
{
    internal class AudioEditorView : EditorWindow
    {
        #region 参数设置
        //用于读写asset文件中的数据
        [SerializeField]
        private static EditorViewData editorViewData;
        /// <summary>
        /// 用于从特效组件中获取预设数据
        /// </summary>
        private static GameObject effectSettingObject;
        /// <summary>
        /// 预览播放时链接的Object
        /// </summary>
        private static GameObject previewObject;

        //分隔栏的参数设置
        private bool resizeHorizontalSpliter;
        private bool resizeVerticalSpliter;
        private Rect horizontalSpliterRect;
        private Rect verticalSpliterRect;

        #region 左上窗口(ProjectExplorer)的参数设置

        //private enum ProjectExplorerTab
        //{
        //    Audio, Events, GameSyncs
        //}

        private static readonly string[] projectExplorerTabNames = Enum.GetNames(typeof(AudioEditorDataType));

        //列表树的参数设置
        private SearchField mSearchField;
        private MultiColumnTreeView[] myTreeViews = new MultiColumnTreeView[MyTreeLists.TreeListCount];
        private MultiColumnHeaderState mMultiColumnHeaderState;
        [SerializeField]
        private TreeModel<MyTreeElement>[] myTreeModels = new TreeModel<MyTreeElement>[MyTreeLists.TreeListCount];
        #endregion

        #region 右上窗口（PropertyEditor）的参数设置
        private enum PropertyEditorTab
        {
            GeneralSettings, Effects, Attenuation, RTPC, States, OtherSetting
        }

        private static readonly string[] propertyEditorTabNames = { "General Settings", "Effects", "3D Setting", "RTPC", "States", "Other Settings" };

        private PropertyEditorTab propertyEditorTabIndex;

        private static readonly string[] eventTypeNames = Enum.GetNames(typeof(AEEventType));

        //记录Header的展开状态，每次选择新的列表树中的内容后刷新
        private List<bool> foldoutHeaderFlags = new List<bool>();
        private Vector2 scorllViewPosition = Vector2.zero;

        private int whichEventUnitSelect = 0;

        private int whichRTPCSelect = 0;

        //BlendContainer的UI参数
        private bool leftAnchorRectDragInBlendContainerGraph = false;
        private bool rightAnchorRectDragInBlendContainerGraph = false;
        /// <summary>
        /// 储存所有BlendTrack中ChildBlendInfo的深度信息，数值越小优先级越高，越先接受鼠标事件，数值为-1则表示被选中
        /// </summary>
        private List<List<int>> blendContainerTracksDepthInfoList = new List<List<int>>();

        #endregion

        #region 右下窗口(PreviewEditor)的参数设置

        private readonly float[] outputVolumeData = new float[4096];
        public enum PreviewModeState
        {
            Playing,
            Paused,
            Stoped
        }
        private readonly string[] previewWindowGridButtonName = { "States", "RTPCs", "Switches", "Triggers" };
        private int previewWindowGridSelected = 0;

        public PreviewModeState previewState = PreviewModeState.Stoped;
        #endregion

        //其他设置
        private AudioEditorManager manager;
        private static AudioEditorView window;
        private GUIStyle whiteLableStyleSkin;
        private static readonly Color backgroundBoxColor = Color.Lerp(Color.white, Color.black, 0.1f);
        private static readonly Color rectSelectedColorInLightTheme = new Color(0, 0, 1, 0.2f);
        private static Color preGUIColor = Color.white;
        //TODO 应增加越界检测
        public static readonly Color[] colorRankList =
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.cyan,
            Color.magenta,
            Color.yellow,
            new Color(65 / 255f, 105 / 255f, 225 / 255f),
            new Color(0, 191 / 255f, 1),
            new Color(0, 1, 1),
        };
        //获取和储存在EditorPrefs中数据的Key
        /// <summary>
        /// 指示第一次打开窗口的标志位，用于设置初始化窗口大小等，数据类型为bool
        /// </summary>
        private const string FirstInitKey = "XSJ.YPZS.AudioEditor.FirstInit";


        //委托
        public static event System.Action DestroyWindow;
        #endregion

        private static Color RectSelectedColor
        {
            get
            {
                if (EditorGUIUtility.isProSkin)
                {
                    return Color.black;
                }
                else
                {
                    return rectSelectedColorInLightTheme;
                }
            }
        }

        /// <summary>
        /// 水平分隔栏的位置
        /// </summary>
        private static float CurrentHorizontalSpliterHeight
        {
            get
            {
                if (editorViewData == null)
                {
                    AudioEditorDebugLog.LogError("editorViewData未加载");
                    return 0;
                }

                return editorViewData.currentHorizontalSpliterHeight;
            }
            set
            {
                if (value < 60)
                {
                    value = 50;
                }

                if (value > window.position.height - 60)
                {
                    value = window.position.height - 50;
                }
                editorViewData.currentHorizontalSpliterHeight = value;
            }
        }

        /// <summary>
        /// 竖直分隔栏的位置
        /// </summary>
        private static float CurrentVerticalSpliterWidth
        {
            get
            {
                if (editorViewData == null)
                {
                    AudioEditorDebugLog.LogError("editorViewData未加载");
                    return 0;
                }
                return editorViewData.currentVerticalSpliterWidth;
            }
            set
            {
                if (value < 60)
                {
                    value = 50;
                }

                if (value > window.position.width - 60)
                {
                    value = window.position.width - 50;
                }
                editorViewData.currentVerticalSpliterWidth = value;
            }
        }

        private PropertyEditorTab PropertyEditorTabIndex
        {
            get => propertyEditorTabIndex;
            set
            {
                propertyEditorTabIndex = value;
                //右边窗口标签刷新时候在此重置页面参数
                foldoutHeaderFlags = new List<bool>();
                scorllViewPosition = Vector2.zero;
            }
        }

        private AudioEditorData ManagerData
        {
            get => AudioEditorManager.Data;
            set => AudioEditorManager.Data = value;
        }

        [MenuItem("Window/Audio Editor/EditorManager", false, 1)]
        public static AudioEditorView GetWindow()
        {
            window = (AudioEditorView)EditorWindow.GetWindow(typeof(AudioEditorView), true, "Audio Editor Manager");
            if (EditorPrefs.HasKey(FirstInitKey) == false || EditorPrefs.GetBool(FirstInitKey) == false)
            {
                AudioEditorDebugLog.Log("First Init");
                window.position = new Rect(300, 200, 1050, 650);
                EditorPrefs.SetBool(FirstInitKey, true);
            }
            window.autoRepaintOnSceneChange = true;
            return window;
        }

        void Awake()
        {

        }

        void OnEnable()
        {
            Init();
        }

        private void Init()
        {
            if (window == null) window = this;

            manager = AudioEditorManager.FindManager();
            if (manager == null)
            {
                return;
            }

            //加载界面数据
            var editorViewDataCompletePath = AudioEditorManager.EditorViewDataPath;
            editorViewData = AssetDatabase.LoadAssetAtPath(editorViewDataCompletePath, typeof(EditorViewData)) as EditorViewData;
            if (editorViewData == null)
            {
                editorViewData = ScriptableObject.CreateInstance<EditorViewData>();
                if (System.IO.Directory.Exists(AudioEditorManager.EditorDataFolder) == false)
                {
                    System.IO.Directory.CreateDirectory(AudioEditorManager.EditorDataFolder);
                }

                AssetDatabase.CreateAsset(editorViewData, editorViewDataCompletePath);
                AudioEditorDebugLog.Log("创建界面数据");
            }

            if (ManagerData == null)
            {
                AudioEditorManager.LoadDataAsset();
            }
            //校验数据的完整性（root被删除等）
            var checkDataCompletely = true;
            if (ManagerData == null)
            {
                checkDataCompletely = false;
            }
            else
            {
                for (int i = 0; i < MyTreeLists.TreeListCount; i++)
                {
                    if (ManagerData.myTreeLists == null)
                    {
                        //没有赋值
                        checkDataCompletely = false;
                        break;
                    }
                    if (ManagerData.myTreeLists[i].Count == 0)
                    {
                        //没有获取到数据
                        checkDataCompletely = false;
                    }
                    else if (ManagerData.myTreeLists[i][0].id != 0)
                    {
                        //首位不为root
                        checkDataCompletely = false;
                    }
                }
            }
            if (checkDataCompletely == false)
            {
                ManagerData = AudioEditorData.CreateDefaultData();
                for (int i = 0; i < editorViewData.myTreeViewStates.Length; i++)
                {
                    editorViewData.myTreeViewStates[i] = new TreeViewState();
                }
                AudioEditorDebugLog.Log("新建数据");
            }
            CreateTempFile();


            //这里Position的数据并不准确，但是在后面GUI中会进行修正
            horizontalSpliterRect = new Rect(0, CurrentHorizontalSpliterHeight, window.position.width, 3f);
            verticalSpliterRect = new Rect(CurrentVerticalSpliterWidth, 0, 3f, window.position.height);

            InitTreeView();

            whiteLableStyleSkin = new GUIStyle();
            whiteLableStyleSkin.normal.textColor = Color.white;
            whiteLableStyleSkin.alignment = TextAnchor.UpperCenter;
            whiteLableStyleSkin.clipping = TextClipping.Clip;
            whiteLableStyleSkin.stretchWidth = false;
            whiteLableStyleSkin.stretchHeight = true;
            whiteLableStyleSkin.wordWrap = true;

            preGUIColor = GUI.color;

            CheckSoundSFXClipPath();


        }

        #region 列表树初始化及相关事项注册
        /// <summary>
        /// 初始化列表树
        /// </summary>
        void InitTreeView()
        {
            //Debug.Log("InitTreeView"); 

            var headerState = MultiColumnTreeView.CreateDefaultMultiColumnHeaderState();
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(mMultiColumnHeaderState, headerState))
            {
                MultiColumnHeaderState.OverwriteSerializedFields(mMultiColumnHeaderState, headerState);
            }

            mMultiColumnHeaderState = headerState;
            var myMultiColumnHeader = new MultiColumnHeader(headerState);
            myMultiColumnHeader.ResizeToFit();
            myMultiColumnHeader.height = 20;

            mSearchField = new SearchField();

            for (int i = 0; i < MyTreeLists.TreeListCount; i++)
            {
                myTreeModels[i] = new TreeModel<MyTreeElement>(ManagerData.myTreeLists[i]);

                myTreeViews[i] =
                    new MultiColumnTreeView(editorViewData.myTreeViewStates[i], myMultiColumnHeader, myTreeModels[i]);

                mSearchField.downOrUpArrowKeyPressed -= myTreeViews[i].SetFocusAndEnsureSelectedItem;
                mSearchField.downOrUpArrowKeyPressed += myTreeViews[i].SetFocusAndEnsureSelectedItem;

                myTreeViews[i].TreeViewItemSelectionChange -= TreeViewSelectionChange;
                myTreeViews[i].TreeViewItemSelectionChange += TreeViewSelectionChange;

                myTreeViews[i].TreeViewNameChange -= DataRegisterToUndo;
                myTreeViews[i].TreeViewNameChange += DataRegisterToUndo;
                myTreeViews[i].TreeViewItemRenameEnd -= TreeViewItemRenameEnd;
                myTreeViews[i].TreeViewItemRenameEnd += TreeViewItemRenameEnd;
                myTreeViews[i].CheckDragItems -= CheckDragItems;
                myTreeViews[i].CheckDragItems += CheckDragItems;
                if (i != 0)
                {
                    myTreeViews[i].disableToggle = true;
                }
            }
            //以下仅在Audio标签下可以实施的行为
            myTreeViews[0].TreeElementEnableChange -= TreeviewItemEnableChange;
            myTreeViews[0].TreeElementEnableChange += TreeviewItemEnableChange;
            myTreeViews[0].TreeViewItemDrag -= DragInContainerChildrenID;
            myTreeViews[0].TreeViewItemDrag += DragInContainerChildrenID;
            myTreeViews[0].OutsideResourceDragIn -= OutSideResourceImport;
            myTreeViews[0].OutsideResourceDragIn += OutSideResourceImport;
            myTreeViews[0].TreeViewContextClickedItem -= ShowTreeViewContextClickMenu;
            myTreeViews[0].TreeViewContextClickedItem += ShowTreeViewContextClickMenu;
            Undo.undoRedoPerformed -= TreeViewUndoRedo;
            Undo.undoRedoPerformed += TreeViewUndoRedo;

        }

        private void TreeViewItemRenameEnd(int id, string newName)
        {
            switch (editorViewData.ProjectExplorerTabIndex)
            {
                case (int)AudioEditorDataType.Audio:
                    AEComponent currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(id);
                    if (currentSelectData != null) currentSelectData.name = newName;
                    break;
                case (int)AudioEditorDataType.Events:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEEvent>(id);
                    if (currentSelectData != null) currentSelectData.name = newName;
                    break;
                case (int)AudioEditorDataType.GameSyncs:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEGameSyncs>(id);
                    if (currentSelectData != null) currentSelectData.name = newName;
                    break;
                    //case (int)AudioEditorDataType.Banks:
                    //    Debug.LogError("未完成");
                    //    break;
            }
        }

        /// <summary>
        /// 当用鼠标拖动列表中的数据时，记录拖动的数据及其父类
        /// </summary>
        /// <param name="selectedChildrenID"></param>
        /// private static List<int> dragIDs = new List<int>();
        ///  private HashSet<int> dragOutParentID = new HashSet<int>();
        [Obsolete]
        protected void DragOutContainerChildrenID(IList<int> selectedChildrenID)
        {
            //Debug.Log("drag out");
            //dragIDs.Clear();
            //dragOutParentID.Clear();
            ////  DataRegisterToUndo();
            //foreach (var childID in selectedChildrenID)
            //{
            //    var parentID = myTreeModels[data.ProjectExplorerTabInex].Find(childID).parent.id;
            //    dragOutParentID.Add(parentID);
            //}
            //dragIDs.InsertRange(0, selectedChildrenID as List<int>);
        }

        /// <summary>
        ///  确认每个组件之间的拖动逻辑
        /// </summary>
        /// <param name="dragitemIDs">要拖动的元素</param>
        /// <param name="parentItem">要拖入的父级元素</param>
        /// <returns></returns>
        private bool CheckDragItems(List<int> dragitemIDs, TreeViewItem parentItem)
        {
            var dragitemsHasWorkUnit = false;
            var dragitemsHasActorMixer = false;
            foreach (var dragitemID in dragitemIDs)
            {
                var treeElement = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(dragitemID);
                if (treeElement == null)
                {
                    AudioEditorDebugLog.LogError("发生错误");
                    break;
                }
                //拖动的项目中有以下类别时，不允许进行拖动
                if (treeElement.type == AEComponentType.Switch || treeElement.type == AEComponentType.State)
                {
                    return false;
                }

                if (treeElement.type == AEComponentType.WorkUnit)
                {
                    dragitemsHasWorkUnit = true;
                }

                if (treeElement.type == AEComponentType.ActorMixer)
                {
                    dragitemsHasActorMixer = true;
                }
            }

            if (parentItem != null)
            {
                var parentdata = (parentItem as TreeViewItem<MyTreeElement>).data;
                //拖入的父级有以下类别时，不允许拖入
                if (parentdata.type == AEComponentType.SwitchGroup ||
                    parentdata.type == AEComponentType.StateGroup ||
                    parentdata.type == AEComponentType.Event)
                {
                    return false;
                }
                //特殊情况,WorkUnit的父级必须为WorkUnit
                if (dragitemsHasWorkUnit && parentdata.type != AEComponentType.WorkUnit)
                {
                    return false;
                }

                //特殊情况,ActorMixer的父级必须为ActorMixer或者WorkUnit
                if (dragitemsHasActorMixer && parentdata.id == 0)
                {
                    return false;
                }
                if (dragitemsHasActorMixer && (parentdata.type != AEComponentType.ActorMixer &&
                                               parentdata.type != AEComponentType.WorkUnit))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 设置当外部资源拖入UI时的生成行为
        /// </summary>
        /// <param name="resourcePaths">资源的相对目录</param>
        /// <param name="dragInParentId">拖入的父级id</param>
        /// <param name="insertPosition">要插入的位置</param>
        private void OutSideResourceImport(List<string> resourcePaths, int dragInParentId, int insertPosition)
        {
            var newItemIDs = new List<int>();
            var parentItem = myTreeModels[0].Find(dragInParentId);
            foreach (var path in resourcePaths)
            {
                var newAudioClip = AssetDatabase.LoadAssetAtPath(path, typeof(AudioClip)) as AudioClip;
                var newComponentData = GenerateData(AEComponentType.SoundSFX, parentItem, insertPosition, newAudioClip.name) as SoundSFX;
                newComponentData.clip = newAudioClip;
                FreshGuidAndPathWhenClipLoad(newComponentData);
                GetTreeListElementByID(newComponentData.id).name = newComponentData.clip.name;
                newItemIDs.Add(newComponentData.id);
            }
            myTreeViews[0].SetSelection(newItemIDs);
            myTreeViews[0].Reload();
        }

        /// <summary>
        /// 当鼠标拖动释放时，处理拖动数据的原父类和现父类中的ChildrenID数据
        /// </summary>
        /// <param name="dragIDs">拖动的子项id</param>
        /// <param name="dragInParentId">要拖入的父项id</param>
        /// <param name="insertPosition">要插入的位置</param>
        private void DragInContainerChildrenID(List<int> dragIDs, int dragInParentId, int insertPosition)
        {
            //插入
            var dragInParentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(dragInParentId);
            //逆序插入使得插入序列与DragOut序列一致
            if (dragInParentData is IAEContainer container)
            {
                for (int i = dragIDs.Count - 1; i >= 0; i--)
                {
                    container.ChildrenID.Insert(insertPosition, dragIDs[i]);
                }
            }
            //删除
            var dragOutParentIDs = new HashSet<int>();
            foreach (var childID in dragIDs)
            {
                //获取原Parent的id
                var parentID = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(childID).parent.id;
                dragOutParentIDs.Add(parentID);
            }
            //先插入后删除会导致当为同一层级内的排列时候，ChildrenID中有复数相同ID，需鉴别
            foreach (var dragOutParentID in dragOutParentIDs)
            {
                var dragOutParentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(dragOutParentID);
                if (dragOutParentData is IAEContainer dragOutContainer)
                {
                    for (int i = 0; i < dragIDs.Count; i++)
                    {
                        if (dragOutParentID == dragInParentId)
                        {
                            var index = dragOutContainer.ChildrenID.FindIndex(x => x == dragIDs[i]);
                            //当有多个数据进行插入时，数据的位置为插入位置+自身在拖动列表中的位置
                            if (index == insertPosition + i)
                            {
                                index = dragOutContainer.ChildrenID.FindLastIndex(x => x == dragIDs[i]);
                            }
                            dragOutContainer.ChildrenID.RemoveAt(index);
                            //Debug.Log("remove Index:" + index);
                        }
                        else
                        {
                            var index = dragOutContainer.ChildrenID.FindIndex(x => x == dragIDs[i]);
                            if (index != -1)
                            {
                                dragOutContainer.ChildrenID.RemoveAt(index);
                            }
                        }
                    }
                    //检测SwitchContainer的OutputList
                    if (dragOutParentData is SwitchContainer switchContainer)
                    {
                        if (dragOutParentID != dragInParentId)
                        {
                            switchContainer.ResetOutputIDList(dragIDs);
                        }
                    }
                    //删除blendContainre中track相关数据
                    if (dragOutParentData is BlendContainer blendContainer)
                    {
                        foreach (var blendContainerTrack in blendContainer.blendContainerTrackList)
                        {
                            foreach (var dragID in dragIDs)
                            {
                                blendContainerTrack.RemoveChildBlendInfoByChildID(dragID);
                            }
                        }
                    }
                    manager.SyncDataToPlayable(dragOutParentID, AudioComponentDataChangeType.ContainerChildrenChange);
                }
            }
            manager.SyncDataToPlayable(dragInParentId, AudioComponentDataChangeType.ContainerChildrenChange);
            GUI.changed = true;
            //Debug.Log("Drag Complete");
        }

        /// <summary>
        /// 当某项Item的enabled切换时，修改其父级Container中ChildID的相关内容
        /// </summary>
        /// <param name="item"></param>
        private void TreeviewItemEnableChange(MyTreeElement item)
        {
            DataRegisterToUndo();
            var parentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(item.parent.id);
            //若当前enabled为true，则从ChildrenID中删除，再切换为false
            if (item.enabled)
            {
                if (parentData is IAEContainer container)
                {
                    var index = container.ChildrenID.FindIndex(x => x == item.id);
                    if (index != -1)
                    {
                        container.ChildrenID.RemoveAt(index);
                    }

                    if (parentData is SwitchContainer switchContainer)
                    {
                        if (switchContainer.outputIDList.Contains(item.id))
                        {
                            switchContainer.outputIDList[switchContainer.outputIDList.FindIndex(x => x == item.id)] = -1;
                        }
                    }

                    if (parentData is BlendContainer blendContainer)
                    {
                        foreach (var blendContainerTrack in blendContainer.blendContainerTrackList)
                        {
                            for (int i = 0; i < blendContainerTrack.ChildrenBlendInfoListCount; i++)
                            {
                                blendContainerTrack.RemoveChildBlendInfoByChildID(item.id);
                            }
                        }
                    }
                }
            }
            else
            {
                //通过TreeView寻找当前Child在Parent中的位置，以确定应该在ChildrenID中插入的位置
                int insertPosition = -1;
                for (int i = 0; i < item.parent.children.Count; i++)
                {
                    if (item.parent.children[i].id == item.id)
                    {
                        insertPosition++;
                        break;
                    }
                    else if (myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(item.parent.children[i].id).enabled)
                    {
                        insertPosition++;
                    }
                }
                //将ChildID插入对应的位置
                if (parentData is IAEContainer container)
                {
                    var index = container.ChildrenID.FindIndex(x => x == item.id);
                    if (index != -1) return;
                    container.ChildrenID.Insert(insertPosition, item.id);
                }
            }
        }
        /// <summary>
        /// 当列表选择变更时
        /// </summary>
        private void TreeViewSelectionChange()
        {
            switch (editorViewData.ProjectExplorerTabIndex)
            {
                case (int)AudioEditorDataType.Audio:
                    //标志位等刷新
                    foldoutHeaderFlags = new List<bool>();
                    scorllViewPosition = Vector2.zero;
                    blendContainerTracksDepthInfoList = null;
                    leftAnchorRectDragInBlendContainerGraph = false;
                    rightAnchorRectDragInBlendContainerGraph = false;

                    //_3DSettingScrollPosition = Vector2.zero;
                    //RTPCSettingScrollPosition = Vector2.zero;
                    whichRTPCSelect = 0;
                    if (editorViewData.freshPageWhenTabInexChange)
                    {
                        PropertyEditorTabIndex = 0;
                    }
                    GraphWaveDisplay.displayID = -1;

                    //停止正在预览的播放
                    if (previewState != PreviewModeState.Stoped)
                    {
                        previewState = PreviewModeState.Stoped;
                        manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                    }
                    break;
                case (int)AudioEditorDataType.Events:
                    whichEventUnitSelect = 0;
                    break;
                case (int)AudioEditorDataType.GameSyncs:
                    //StateGroupScrollPosition = Vector2.zero;
                    break;
                //case (int)ProjectExplorerTab.Banks:
                //    break;
                default:
                    break;
            }

            // //加载当前选择的SoundSFX的音频数据
            // var id = data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID;
            // var element = myTreeModels[data.ProjectExplorerTabIndex].Find(id);
            // if (element.Type == AEComponentType.soundSFX)
            // {
            //     var data =  AudioEditorManager.GetAEAudioComponentDataByID(id) as SoundSFX;
            //     Debug.Log(data.clip.loadState);
            //     if (data.clip != null && data.clip.loadState == AudioDataLoadState.Unloaded)
            //     {
            //         data.clip.LoadAudioData();
            //     }
            // }
        }

        /// <summary>
        /// 撤销后重做刷新树数据
        /// </summary>
        private void TreeViewUndoRedo()
        {
            InitTreeView();
        }

        /// <summary>
        /// 展示在Audio标签页下右键显示的菜单
        /// </summary>
        private void ShowTreeViewContextClickMenu()
        {
            Event.current.Use();
            // Debug.Log("ContextClickedItem:" + id);

            foreach (var selectedID in editorViewData.myTreeViewStates[0].selectedIDs)
            {
                //暂时ActorMixer不能使用右键菜单
                var data = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(selectedID);
                if (data is ActorMixer)
                {
                    return;
                }
            }

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("NewEvent/Play"), false, () =>
            {
                GenerateAEEvent(AEEventType.Play, editorViewData.myTreeViewStates[0].selectedIDs);
            });
            menu.AddItem(new GUIContent("NewEvent/Pause"), false, () =>
            {
                GenerateAEEvent(AEEventType.Pause, editorViewData.myTreeViewStates[0].selectedIDs);
            });
            menu.AddItem(new GUIContent("NewEvent/Resume"), false, () =>
            {
                GenerateAEEvent(AEEventType.Resume, editorViewData.myTreeViewStates[0].selectedIDs);
            });
            menu.AddItem(new GUIContent("NewEvent/Stop"), false, () =>
            {
                GenerateAEEvent(AEEventType.Stop, editorViewData.myTreeViewStates[0].selectedIDs);
            });
            // menu.AddItem(new GUIContent("NewEvent/StopAll"), false, () =>
            // {
            //     Debug.LogError("此功能尚未完成");
            // });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                TreeViewDelete(editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].selectedIDs);
            });
            menu.ShowAsContext();

            Repaint();
        }

        #endregion

        private void GenerateAEEvent(AEEventType eventType, List<int> targetIDs)
        {
            DataRegisterToUndo();
            for (int i = 0; i < targetIDs.Count; i++)
            {
                var targetData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(targetIDs[i]);
                var newEventData = GenerateData(AEComponentType.Event, dataName: $"{targetData.name}_{eventType}") as AEEvent;
                newEventData.eventList.Add(new AEEventUnit(eventType, targetData.id));
            }
        }

        private void GenerateEffectSettingGameObject()
        {
            var transform = manager.gameObject.transform.Find("EffectSettingObject");
            if (transform == null)
            {
                effectSettingObject = EditorUtility.CreateGameObjectWithHideFlags("EffectSettingObject",
                   AudioEditorManager.debugMode ? HideFlags.DontSave : HideFlags.HideAndDontSave);
                effectSettingObject.tag = "EditorOnly";
                effectSettingObject.transform.parent = manager.transform;
                effectSettingObject.AddComponent<AudioSource>();
                effectSettingObject.AddComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Off;
                effectSettingObject.AddComponent<AudioReverbZone>().reverbPreset = AudioReverbPreset.Off;
            }
            else
            {
                effectSettingObject = transform.gameObject;
            }
            transform = manager.gameObject.transform.Find("PreviewObject");
            if (transform == null)
            {
                previewObject = EditorUtility.CreateGameObjectWithHideFlags("PreviewObject",
                    AudioEditorManager.debugMode ? HideFlags.DontSave : HideFlags.HideAndDontSave);
                previewObject.tag = "EditorOnly";
                previewObject.transform.parent = manager.transform;

                //如果没有audioListener则挂载在preview下
                //if (FindObjectOfType<AudioListener>() == null)
                //{
                //    previewObject.AddComponent<AudioListener>();
                //}
            }
            else
            {
                previewObject = transform.gameObject;
            }
        }

        void OnDestroy()
        {
            Undo.undoRedoPerformed -= TreeViewUndoRedo;

            if (manager != null)
            {
                //取消列表命名中的状态
                for (int i = 0; i < MyTreeLists.TreeListCount; i++)
                {
                    ManagerData.myTreeLists[i] = myTreeModels[i].GetData().ToList();
                    myTreeViews[i].EndRename();
                }
                //停止正在播放的预览实例
                manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                //卸载已加载的AudioClip
                AudioEditorManager.UnloadAllAudioClip();
                //取消关联clip，如果正在runtime时关闭编辑器界面则不取消关联，等待Manager调用Destroy时再取消关联
                if (ManagerData && manager.staticallyLinkedAudioClips == false && EditorApplication.isPlaying == false)
                {
                    manager.UnlinkAllAudioClips();
                }
                //检查文件路径是否有更改
                CheckSoundSFXClipPath();

                //储存修改
                SaveData();

                ClearTempFile();

                DestroyImmediate(effectSettingObject);
                DestroyImmediate(previewObject);
            }

            DestroyWindow?.Invoke();
        }

        void OnGUI()
        {
            if (manager == null)
            {
                var currentColor = GUI.color;
                GUI.color = Color.red;
                GUILayout.Box(new GUIContent("请在场景中挂载AudioEditorManager"), GUI.skin.box, GUILayout.ExpandWidth(true));
                GUI.color = currentColor;
                if (GUILayout.Button("点击挂载manager"))
                {
                    var gameObject = new GameObject("AudioEditorManager");
                    gameObject.AddComponent<AudioEditorManager>();
                }
                if (Event.current.type == EventType.Repaint)
                {
                    manager = AudioEditorManager.FindManager();
                    if (manager != null)
                    {
                        Init();
                    }
                }
                return;
            }

            if (manager != null && effectSettingObject == null)
            {
                GenerateEffectSettingGameObject();
            }

            Undo.RecordObject(editorViewData, "AudioEditorWindow Change");
            Undo.RecordObject(ManagerData, "AudioEditorWindow Change");

            try
            {
                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();

                GUI.color = preGUIColor;
                EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(CurrentVerticalSpliterWidth), GUILayout.Height(CurrentHorizontalSpliterHeight));
                GUILayout.BeginArea(new Rect(0, 0, CurrentVerticalSpliterWidth, CurrentHorizontalSpliterHeight), GUI.skin.box);
                DrawProjectExplorerWindow();
                GUILayout.EndArea();
                EditorGUILayout.EndScrollView();

                SetVerticalSpliter();

                EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(CurrentHorizontalSpliterHeight));
                var rect = new Rect(0, 0, position.width - CurrentVerticalSpliterWidth - verticalSpliterRect.width, CurrentHorizontalSpliterHeight);
                // GUI.SetNextControlName("PropertyEditorWindow Background Area");
                GUILayout.BeginArea(rect, GUI.skin.box);
                DrawPropertyEditorWindow();
                //if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                //{
                //    GUI.FocusControl("PropertyEditorWindow Background Area");
                //    myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();
                //}
                GUILayout.EndArea();
                EditorGUILayout.EndScrollView();

                GUILayout.EndHorizontal();

                SetHorizontalSpliter();

                GUILayout.BeginHorizontal();

                EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(CurrentVerticalSpliterWidth), GUILayout.Height(position.height - CurrentHorizontalSpliterHeight - horizontalSpliterRect.height));
                GUILayout.BeginArea(new Rect(0, 0, CurrentVerticalSpliterWidth, position.height - CurrentHorizontalSpliterHeight - horizontalSpliterRect.height), GUI.skin.box);
                //GUILayout.Box(new GUIContent("说明窗口"), GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUILayout.Box("", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUILayout.EndArea();
                EditorGUILayout.EndScrollView();

                GUILayout.Space(verticalSpliterRect.width);

                EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(position.height - CurrentHorizontalSpliterHeight - horizontalSpliterRect.height));
                GUILayout.BeginArea(new Rect(0, 0, position.width - CurrentVerticalSpliterWidth - verticalSpliterRect.width, position.height - CurrentHorizontalSpliterHeight - horizontalSpliterRect.height), GUI.skin.box);
                DrawPreviewWindow();
                //GUILayout.Box(new GUIContent("右下方窗口"), GUI.skin.box, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                GUILayout.EndArea();
                EditorGUILayout.EndScrollView();

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            catch (Exception e)
            {
                if (e is UnityEngine.ExitGUIException == false)
                {
                    AudioEditorDebugLog.LogWarning("发生未知错误");
                    Debug.Log(e);
                    editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID = 0;
                    throw;
                }
            }

            //点击空白地方取消TreeView的焦点，并结束命名状态（如果有）
            if (Event.current.type == EventType.MouseDown)
            {
                GUIUtility.keyboardControl = -1;
                myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();
                Event.current.Use();
            }

            if (EditorWindow.focusedWindow == window && Event.current.control && Event.current.keyCode == KeyCode.S &&
                Event.current.type == EventType.KeyDown)
            {
                SaveData();
                AudioEditorDebugLog.Log("保存数据");
            }

        }

        /// <summary>
        /// 绘制水平的分隔栏
        /// </summary>
        private void SetHorizontalSpliter()
        {
            horizontalSpliterRect.Set(horizontalSpliterRect.x, CurrentHorizontalSpliterHeight, position.width, horizontalSpliterRect.height);
            var currentColor = GUI.color;
            GUI.color = Color.grey;
            GUI.DrawTexture(horizontalSpliterRect, EditorGUIUtility.whiteTexture);
            EditorGUIUtility.AddCursorRect(horizontalSpliterRect, MouseCursor.ResizeVertical);

            if (Event.current.type == EventType.MouseDown && horizontalSpliterRect.Contains(Event.current.mousePosition))
            {
                resizeHorizontalSpliter = true;
            }
            if (resizeHorizontalSpliter)
            {
                CurrentHorizontalSpliterHeight = Event.current.mousePosition.y;
                horizontalSpliterRect.Set(horizontalSpliterRect.x, CurrentHorizontalSpliterHeight, position.width, horizontalSpliterRect.height);
            }
            if (Event.current.type == EventType.MouseUp)
            {
                resizeHorizontalSpliter = false;
            }
            GUILayout.Space(horizontalSpliterRect.height);
            GUI.color = currentColor;
        }

        /// <summary>
        /// 绘制竖直的分隔栏
        /// </summary>
        private void SetVerticalSpliter()
        {
            verticalSpliterRect.Set(CurrentVerticalSpliterWidth, verticalSpliterRect.y, verticalSpliterRect.width, position.height);
            var currentColor = GUI.color;
            GUI.color = Color.grey;
            GUI.DrawTexture(verticalSpliterRect, EditorGUIUtility.whiteTexture);
            EditorGUIUtility.AddCursorRect(verticalSpliterRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && verticalSpliterRect.Contains(Event.current.mousePosition))
            {
                resizeVerticalSpliter = true;
            }
            if (resizeVerticalSpliter)
            {
                CurrentVerticalSpliterWidth = Event.current.mousePosition.x;
                verticalSpliterRect.Set(CurrentVerticalSpliterWidth, verticalSpliterRect.y, verticalSpliterRect.width, position.height);
            }
            if (Event.current.type == EventType.MouseUp)
            {
                resizeVerticalSpliter = false;
            }
            GUILayout.Space(verticalSpliterRect.width);
            GUI.color = currentColor;
        }

        #region 绘制左上的窗口界面

        /// <summary>
        /// 绘制左上的窗口界面
        /// </summary>
        private void DrawProjectExplorerWindow()
        {
            int tabIndex = GUILayout.Toolbar(editorViewData.ProjectExplorerTabIndex, projectExplorerTabNames, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (tabIndex != editorViewData.ProjectExplorerTabIndex)
            {
                editorViewData.ProjectExplorerTabIndex = tabIndex;
                myTreeViews[tabIndex].SetFocus();
                for (int i = 0; i < myTreeViews.Length; i++)
                {
                    myTreeViews[i].EndRename();
                }
                Repaint();
            }
            GUILayoutOption[] buttonOptions = { GUILayout.MaxWidth(21), GUILayout.ExpandWidth(false) };
            GUI.color = backgroundBoxColor;

            var currentSelectID = editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID;

            var currentSelectElement = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectID);

            switch (editorViewData.ProjectExplorerTabIndex)
            {
                case (int)AudioEditorDataType.Audio:
                    EditorGUILayout.BeginHorizontal();
                    //New WorkUnit Button
                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.WorkUnit), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.WorkUnit);
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit &&
                                                 currentSelectElement.type != AEComponentType.ActorMixer);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.ActorMixer), GUI.skin.box,
                        buttonOptions))
                    {
                        GenerateData(AEComponentType.ActorMixer);
                    }
                    EditorGUI.EndDisabledGroup();
                    //New SoundSFX Button
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.SoundSFX), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.SoundSFX);
                    }
                    //New RandomContainer Button
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.RandomContainer), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.RandomContainer);
                    }
                    //New SequenceContainer Button
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.SequenceContainer), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.SequenceContainer);
                    }
                    //New SwitchContainer Button
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.SwitchContainer), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.SwitchContainer);
                    }
                    //New BlendContainer Button
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.BlendContainer), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.BlendContainer);
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
                case (int)AudioEditorDataType.Events:
                    EditorGUILayout.BeginHorizontal();
                    //WorkUnit
                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.WorkUnit), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.WorkUnit);
                    }
                    EditorGUI.EndDisabledGroup();
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Event), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.Event);
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
                case (int)AudioEditorDataType.GameSyncs:
                    EditorGUILayout.BeginHorizontal();
                    //WorkUnit
                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.WorkUnit), GUI.skin.box,
                        buttonOptions))
                    {
                        GenerateData(AEComponentType.WorkUnit);
                    }

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.SwitchGroup), GUI.skin.box,
                        buttonOptions))
                    {
                        GenerateData(AEComponentType.SwitchGroup);
                    }

                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.SwitchGroup &&
                                                 currentSelectElement.type != AEComponentType.Switch);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Switch), GUI.skin.box,
                        buttonOptions))
                    {
                        GenerateData(AEComponentType.Switch);
                    }

                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.StateGroup), GUI.skin.box,
                        buttonOptions))
                    {
                        GenerateData(AEComponentType.StateGroup);
                        GenerateData(AEComponentType.State, dataName: "None");
                        myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();
                    }

                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.StateGroup &&
                                                 currentSelectElement.type != AEComponentType.State);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.State), GUI.skin.box,
                        buttonOptions))
                    {
                        GenerateData(AEComponentType.State);
                    }

                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.type != AEComponentType.WorkUnit && currentSelectElement.type != AEComponentType.GameParameter);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.GameParameter), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.GameParameter);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
                    //case (int)ProjectExplorerTab.Banks:
                    //    if (GUILayout.Button("保存数据"))
                    //    {
                    //        SaveData();
                    //    }
                    //    GUI.color = preGUIColor;
                    //    DrawSearchBarAndTreeView();
                    //    break;
            }

        }

        /// <summary>
        /// 在ManagerData中创建相应类型的数据
        /// </summary>
        /// <param name="type">要创建的类型</param>
        /// <param name="parentElement">父级项</param>
        /// <param name="insertPosition">要插入的位置</param>
        /// <param name="dataName">名字</param>
        private AEComponent GenerateData(AEComponentType type, MyTreeElement parentElement = null, int insertPosition = -1, string dataName = null)
        {
            DataRegisterToUndo();
            switch (type)
            {
                case AEComponentType.WorkUnit:
                    break;
                case AEComponentType.SoundSFX:
                case AEComponentType.RandomContainer:
                case AEComponentType.SequenceContainer:
                case AEComponentType.SwitchContainer:
                case AEComponentType.BlendContainer:
                case AEComponentType.ActorMixer:
                    editorViewData.ProjectExplorerTabIndex = 0;
                    break;
                case AEComponentType.Event:
                    editorViewData.ProjectExplorerTabIndex = 1;
                    break;
                case AEComponentType.SwitchGroup:
                case AEComponentType.Switch:
                case AEComponentType.StateGroup:
                case AEComponentType.State:
                case AEComponentType.GameParameter:
                    editorViewData.ProjectExplorerTabIndex = 2;
                    break;
                default:
                    Debug.LogError("发生错误");
                    return null;
            }

            if (parentElement == null)
            {
                parentElement = GetTreeListElementByID(editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID);
            }
            //workunit只能生成在workunit下
            if (type == AEComponentType.WorkUnit && parentElement.type != AEComponentType.WorkUnit)
            {
                return null;
            }

            if (type == AEComponentType.ActorMixer && parentElement.type != AEComponentType.ActorMixer)
            {
                return null;
            }

            var depth = parentElement.depth + 1;
            //某些组件不能作为父类，如果当前选择为soundSFX、Event等，则再往上一级创建
            if (parentElement.type == AEComponentType.SoundSFX ||
                parentElement.type == AEComponentType.Event ||
                parentElement.type == AEComponentType.GameParameter ||
                parentElement.type == AEComponentType.Switch ||
                parentElement.type == AEComponentType.State)
            {
                parentElement = (MyTreeElement)parentElement.parent;
                depth -= 1;
            }

            var id = myTreeModels[editorViewData.ProjectExplorerTabIndex].GenerateUniqueID();
            var name = dataName ?? string.Format("New_" + type.ToString() + "_" + id);
            MyTreeElement newElement = new MyTreeElement(name, depth, id, type);
            if (insertPosition == -1)
            {
                insertPosition = parentElement.hasChildren ? parentElement.children.Count : 0;
            }
            myTreeModels[editorViewData.ProjectExplorerTabIndex].AddElement(newElement, parentElement, insertPosition);
            myTreeViews[editorViewData.ProjectExplorerTabIndex].SetExpanded(parentElement.id, true);

            myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();
            myTreeViews[editorViewData.ProjectExplorerTabIndex].BeginRename(newElement.id);
            var newList = new List<int> { newElement.id };
            myTreeViews[editorViewData.ProjectExplorerTabIndex].SetSelection(newList);
            myTreeViews[editorViewData.ProjectExplorerTabIndex].FrameItem(newElement.id);
            var newComponentData = manager.GenerateManagerData(newElement.name, newElement.id, type, parentElement.id);

            var parentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(parentElement.id);
            if (parentData is IAEContainer container)
            {
                container.ChildrenID.Insert(insertPosition, newElement.id);
                manager.SyncDataToPlayable(parentData.id, AudioComponentDataChangeType.ContainerChildrenChange);
            }

            return newComponentData;
        }


        /// <summary>
        /// 绘制搜索框和列表树，实现删除等功能
        /// </summary>
        private void DrawSearchBarAndTreeView()
        {
            var e = Event.current;
            var searchString = mSearchField.OnGUI(editorViewData.searchString);
            //data.searchString = m_SearchField.OnGUI(data.searchString);
            myTreeViews[editorViewData.ProjectExplorerTabIndex].searchString = searchString;
            var rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            myTreeViews[editorViewData.ProjectExplorerTabIndex].OnGUI(rect);
            if (searchString != editorViewData.searchString)
            {
                editorViewData.searchString = searchString;
                if (searchString == "")
                {
                    myTreeViews[editorViewData.ProjectExplorerTabIndex].FrameItem(editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID);
                    myTreeViews[editorViewData.ProjectExplorerTabIndex].SetFocusAndEnsureSelectedItem();
                }
            }
            //实现删除功能
            if (rect.Contains(Event.current.mousePosition) &&
                GUIUtility.keyboardControl == myTreeViews[editorViewData.ProjectExplorerTabIndex].treeViewControlID &&
                e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                if (editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID != 0)
                {
                    //Debug.Log("delete");
                    var listID = editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].selectedIDs;
                    TreeViewDelete(listID);
                }
            }
        }

        /// <summary>
        /// 删除id关联的TreeViewData和managerData
        /// </summary>
        /// <param name="idList"></param>
        private void TreeViewDelete(List<int> idList)
        {
            DataRegisterToUndo();
            //删除ManagerDataAsseet中的数据
            foreach (var id in idList)
            {
                //递归删除子类
                if (myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id) == null) continue;
                var children = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).children;
                if (children != null)
                {
                    List<int> newList = new List<int>();
                    foreach (var child in children)
                    {
                        newList.Add(child.id);
                    }
                    TreeViewDelete(newList);
                }
            }

            switch (editorViewData.ProjectExplorerTabIndex)
            {
                case (int)AudioEditorDataType.Audio:
                    foreach (var id in idList)
                    {
                        if (myTreeModels[0].Find(id) == null) continue;
                        //删除父类中ChildID中的相关子类id
                        var parentID = myTreeModels[0].Find(id).parent.id;
                        var parentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(parentID);
                        if (parentData is IAEContainer container)
                        {
                            for (int i = 0; i < container.ChildrenID.Count; i++)
                            {
                                if (container.ChildrenID[i] == id) container.ChildrenID.RemoveAt(i);
                            }
                        }
                        //删除ManagerData中的数据
                        manager.RemoveAEComponentDataByID<AEAudioComponent>(id);
                    }
                    break;
                case (int)AudioEditorDataType.Events:
                    foreach (var id in idList)
                    {
                        manager.RemoveAEComponentDataByID<AEEvent>(id);
                    }
                    break;
                case (int)AudioEditorDataType.GameSyncs:
                    foreach (var id in idList)
                    {
                        //检测是否有不能删除的内容
                        if (AudioEditorManager.GetAEComponentDataByID<State>(id) is State state && state.IsNone == true)
                        {
                            return;
                        }
                        manager.RemoveAEComponentDataByID<AEGameSyncs>(id);
                    }

                    break;
                default:
                    Debug.LogError("未完成");
                    break;
            }

            //删除WindowDataAseet中列表树的数据
            myTreeModels[editorViewData.ProjectExplorerTabIndex].RemoveElements(idList);
            editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID = 0;
        }


        #endregion

        #region 绘制右上的属性编辑界面
        /// <summary>
        /// 绘制右上的窗口界面
        /// </summary>
        private void DrawPropertyEditorWindow()
        {
            if (editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].selectedIDs.Count != 1) return;
            var currentSelectID = editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID;
            switch (GetTreeListElementByID(currentSelectID).type)
            {
                case AEComponentType.WorkUnit:
                    break;
                case AEComponentType.ActorMixer:
                    DrawActorMixerPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.SoundSFX:
                    DrawSoundSFXPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.RandomContainer:
                    DrawRandomContainerPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.SequenceContainer:
                    DrawSequenceContainerPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.SwitchContainer:
                    DrawSwitchContainerPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.BlendContainer:
                    DrawBlendContainerPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.Event:
                    DrawEventPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.SwitchGroup:
                    DrawSwitchGroupPropertyEditor(currentSelectID);
                    break;
                case AEComponentType.Switch:
                    // var currentSelectSwitch = AudioEditorManager.GetAEComponentDataByID<Switch>(currentSelectID);
                    var currentSelectSwitch = GetCurrentSelectData(currentSelectID, AEComponentType.Switch) as Switch;
                    if (currentSelectSwitch == null)
                        return;
                    DrawNameUnit(currentSelectSwitch);
                    break;
                case AEComponentType.StateGroup:
                    DrawStateGroupProperty(currentSelectID);
                    break;
                case AEComponentType.State:
                    // var currentSelectState = AudioEditorManager.GetAEComponentDataByID<State>(currentSelectID);
                    var currentSelectState = GetCurrentSelectData(currentSelectID, AEComponentType.State) as State;
                    if (currentSelectState == null)
                        return;
                    DrawNameUnit(currentSelectState);
                    break;
                case AEComponentType.GameParameter:
                    DrawGameParameterProperty(currentSelectID);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawActorMixerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.ActorMixer) as ActorMixer;
            if (currentSelectData == null) return;

            EditorGUI.BeginDisabledGroup(!myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    {
                        EditorGUILayout.BeginHorizontal();

                        var name = EditorGUILayout.TextField(currentSelectData.name);
                        if (name != currentSelectData.name)
                        {
                            GetTreeListElementByID(id).name = name;
                            myTreeViews[editorViewData.ProjectExplorerTabIndex].Reload();
                        }
                        currentSelectData.name = GetTreeListElementByID(id).name;
                        GUILayout.Label("id:");
                        EditorGUI.BeginDisabledGroup(editorViewData.ProjectExplorerTabIndex != 1);
                        EditorGUILayout.IntField(currentSelectData.id);
                        EditorGUI.EndDisabledGroup();
                        GUILayout.FlexibleSpace();

                        EditorGUILayout.EndHorizontal();

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                        EditorGUILayout.BeginHorizontal();

                        GUI.color = backgroundBoxColor;
                        using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentVerticalSpliterWidth) / 3 * 2)))
                        {
                            GUI.color = preGUIColor;
                            DrawGeneralSettingUnit(currentSelectData);

                            DrawTransitionsUnit(currentSelectData);
                        }

                        GUI.color = backgroundBoxColor;
                        using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
                        {
                            GUI.color = preGUIColor;
                            EditorGUI.BeginChangeCheck();

                            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild == false);
                            var newOverrideActorMixer = GUILayout.Toggle((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.OutputMixerGroup) == AEComponentDataOverrideType.OutputMixerGroup, "Override OutputMixer");
                            if (newOverrideActorMixer)
                            {
                                currentSelectData.overrideFunctionType |= AEComponentDataOverrideType.OutputMixerGroup;
                            }
                            else
                            {
                                currentSelectData.overrideFunctionType &= ~AEComponentDataOverrideType.OutputMixerGroup;
                            }
                            
                            EditorGUI.EndDisabledGroup();
                            EditorGUI.BeginDisabledGroup((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.OutputMixerGroup) != AEComponentDataOverrideType.OutputMixerGroup && currentSelectData.isContianerChild);
                            currentSelectData.outputMixer = EditorGUILayout.ObjectField(currentSelectData.outputMixer, typeof(AudioMixerGroup), true) as AudioMixerGroup;
                            EditorGUI.EndDisabledGroup();

                            if (EditorGUI.EndChangeCheck())
                            {
                                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                                //Debug.Log("数据刷新 In DrawGeneralSettingsSidePage");
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    break;
                case PropertyEditorTab.Effects:
                    DrawEffectsSetting(currentSelectData);
                    break;
                case PropertyEditorTab.Attenuation:
                    Draw3DSetting(currentSelectData);
                    break;
                case PropertyEditorTab.RTPC:
                    DrawRTPCSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.States:
                    DrawStateSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.OtherSetting:
                    DrawOtherSettingUnit(currentSelectData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawSoundSFXPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SoundSFX) as SoundSFX;
            if (currentSelectData == null) return;

            EditorGUI.BeginDisabledGroup(!myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    {
                        EditorGUILayout.BeginHorizontal();

                        var name = EditorGUILayout.TextField(currentSelectData.name);
                        if (name != currentSelectData.name)
                        {
                            GetTreeListElementByID(id).name = name;
                            myTreeViews[editorViewData.ProjectExplorerTabIndex].Reload();
                        }
                        currentSelectData.name = GetTreeListElementByID(id).name;
                        GUILayout.Label("id:");
                        EditorGUI.BeginDisabledGroup(editorViewData.ProjectExplorerTabIndex != 1);
                        EditorGUILayout.IntField(currentSelectData.id);
                        EditorGUI.EndDisabledGroup();
                        GUILayout.FlexibleSpace();

                        LoadSoundSFXAudioClip(currentSelectData);
                        var newAudioClip = EditorGUILayout.ObjectField(currentSelectData.clip, typeof(AudioClip), true) as AudioClip;
                        if (newAudioClip != currentSelectData.clip)
                        {
                            currentSelectData.clip = newAudioClip;
                            FreshGuidAndPathWhenClipLoad(currentSelectData);
                            GraphWaveDisplay.displayID = -1;
                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.AudioClipChange);
                        }
                        EditorGUILayout.EndHorizontal();

                        GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

                        EditorGUILayout.BeginHorizontal();

                        GUI.color = backgroundBoxColor;
                        using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentVerticalSpliterWidth) / 3 * 2)))
                        {
                            GUI.color = preGUIColor;
                            DrawGeneralSettingUnit(currentSelectData);

                            DrawTransitionsUnit(currentSelectData);
                        }

                        GUI.color = backgroundBoxColor;
                        using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
                        {
                            GUI.color = preGUIColor;
                            DrawGeneralSettingsSidePage(currentSelectData);
                        }
                        EditorGUILayout.EndHorizontal();
                        var waveDispalyRect = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));
                        GraphWaveDisplay.Draw(waveDispalyRect, currentSelectData);
                    }
                    break;
                case PropertyEditorTab.Effects:
                    DrawEffectsSetting(currentSelectData);
                    break;
                case PropertyEditorTab.Attenuation:
                    Draw3DSetting(currentSelectData);
                    break;
                case PropertyEditorTab.RTPC:
                    DrawRTPCSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.States:
                    DrawStateSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.OtherSetting:
                    DrawOtherSettingUnit(currentSelectData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawRandomContainerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.RandomContainer) as RandomContainer;
            if (currentSelectData == null) return;

            EditorGUI.BeginDisabledGroup(!myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    DrawNameUnit(currentSelectData);

                    EditorGUILayout.BeginHorizontal();

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentVerticalSpliterWidth) / 3 * 2)))
                    {
                        GUI.color = preGUIColor;

                        DrawGeneralSettingUnit(currentSelectData);

                        DrawTransitionsUnit(currentSelectData);

                        EditorGUILayout.LabelField("Container Setting", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Type"));
                        currentSelectData.playType = (RandomContainerPlayType)GUILayout.Toolbar((int)currentSelectData.playType, Enum.GetNames(typeof(RandomContainerPlayType)));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Mode"));
                        currentSelectData.PlayMode = (ContainerPlayMode)GUILayout.Toolbar((int)currentSelectData.PlayMode, Enum.GetNames(typeof(ContainerPlayMode)));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);

                        EditorGUILayout.BeginHorizontal();
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUI.BeginDisabledGroup(currentSelectData.PlayMode != ContainerPlayMode.Continuous);
                            currentSelectData.crossFade = EditorGUILayout.BeginToggleGroup(new GUIContent("CrossFade", "交叉淡变"), currentSelectData.crossFade);
                            currentSelectData.crossFadeTime = EditorGUILayout.FloatField(new GUIContent("CrossFade Time"), currentSelectData.crossFadeTime);
                            currentSelectData.crossFadeType = (CrossFadeType)EditorGUILayout.EnumPopup(new GUIContent("CrossFade Type", "淡变类型"), currentSelectData.crossFadeType);
                            EditorGUILayout.EndToggleGroup();
                            EditorGUI.EndDisabledGroup();
                        }
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();

                        EditorGUI.indentLevel--;
                    }

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        DrawGeneralSettingsSidePage(currentSelectData);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;
                case PropertyEditorTab.Effects:
                    DrawEffectsSetting(currentSelectData);
                    break;
                case PropertyEditorTab.Attenuation:
                    Draw3DSetting(currentSelectData);
                    break;
                case PropertyEditorTab.RTPC:
                    DrawRTPCSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.States:
                    DrawStateSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.OtherSetting:
                    DrawOtherSettingUnit(currentSelectData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawSequenceContainerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SequenceContainer) as SequenceContainer;
            if (currentSelectData == null) return;

            EditorGUI.BeginDisabledGroup(!myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    DrawNameUnit(currentSelectData);

                    EditorGUILayout.BeginHorizontal();
                    //   EditorGUILayout.BeginVertical();

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentVerticalSpliterWidth) / 3 * 2)))
                    {
                        GUI.color = preGUIColor;
                        DrawGeneralSettingUnit(currentSelectData);
                        DrawTransitionsUnit(currentSelectData);

                        EditorGUILayout.LabelField("Container Setting", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Type", "决定下一次播放的顺序"));
                        currentSelectData.playType = (SequenceContainerPlayType)GUILayout.Toolbar((int)currentSelectData.playType, Enum.GetNames(typeof(SequenceContainerPlayType)));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Mode", "决定播放模式为步进或连续"));
                        currentSelectData.PlayMode = (ContainerPlayMode)GUILayout.Toolbar((int)currentSelectData.PlayMode, Enum.GetNames(typeof(ContainerPlayMode)));
                        EditorGUILayout.EndHorizontal();
                        currentSelectData.alwaysResetPlayList = EditorGUILayout.Toggle(new GUIContent("Always Reset PlayList", "每次播放都重置为正序或逆序的标准序列组"), currentSelectData.alwaysResetPlayList, GUILayout.ExpandWidth(false));

                        EditorGUILayout.BeginHorizontal();
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUI.BeginDisabledGroup(currentSelectData.PlayMode != ContainerPlayMode.Continuous);
                            currentSelectData.crossFade = EditorGUILayout.BeginToggleGroup(new GUIContent("CrossFade", "交叉淡变"), currentSelectData.crossFade);
                            currentSelectData.crossFadeTime = EditorGUILayout.FloatField(new GUIContent("CrossFade Time"), currentSelectData.crossFadeTime);
                            currentSelectData.crossFadeType = (CrossFadeType)EditorGUILayout.EnumPopup(new GUIContent("CrossFade Type", "淡变类型"), currentSelectData.crossFadeType);
                            EditorGUILayout.EndToggleGroup();
                            EditorGUI.EndDisabledGroup();
                        }
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();

                        EditorGUI.indentLevel--;
                    }
                    // EditorGUILayout.EndVertical();

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        DrawGeneralSettingsSidePage(currentSelectData);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;
                case PropertyEditorTab.Effects:
                    DrawEffectsSetting(currentSelectData);
                    break;
                case PropertyEditorTab.Attenuation:
                    Draw3DSetting(currentSelectData);
                    break;
                case PropertyEditorTab.RTPC:
                    DrawRTPCSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.States:
                    DrawStateSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.OtherSetting:
                    DrawOtherSettingUnit(currentSelectData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUI.EndDisabledGroup();

        }

        #region 绘制SwtichContianer的属性编辑界面
        private void DrawSwitchContainerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SwitchContainer) as SwitchContainer;
            if (currentSelectData == null) return;

            EditorGUI.BeginDisabledGroup(!myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    DrawNameUnit(currentSelectData);
                    EditorGUILayout.BeginHorizontal();

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentVerticalSpliterWidth) / 3 * 2)))
                    {
                        GUI.color = preGUIColor;

                        DrawGeneralSettingUnit(currentSelectData);
                        DrawTransitionsUnit(currentSelectData);

                        EditorGUILayout.LabelField("Container Setting", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Mode", "决定播放模式为步进或连续"));
                        currentSelectData.PlayMode = (ContainerPlayMode)GUILayout.Toolbar((int)currentSelectData.PlayMode, Enum.GetNames(typeof(ContainerPlayMode)));
                        EditorGUILayout.EndHorizontal();

                        var switchGroupData = AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(currentSelectData.switchGroupID);
                        if (switchGroupData == null)
                        {
                            currentSelectData.switchGroupID = -1;
                            Repaint();
                        }
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("SwitchGroup"));
                        var selectedSwitchGroupName = currentSelectData.switchGroupID == -1 ? "null" : switchGroupData.name;
                        var buttonRect = EditorGUILayout.GetControlRect();
                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent(selectedSwitchGroupName), FocusType.Passive))
                        {
                            //方案一
                            ShowSwitchGroupMenu(buttonRect, currentSelectData);
                            //方案二
                            // PopupWindow.Show(buttonRect,new SelectPopupWindow(WindowOpenFor.SelectSwichGroup, (selectedSwitchGroupID) =>
                            // {
                            //     currentSelectData.switchGroupID = selectedSwitchGroupID;
                            //     //初始化output数据，用于对应switch列表
                            //     currentSelectData.outputIDList = new List<int>();
                            //     for (int i = 0; i < (AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(selectedSwitchGroupID) as SwitchGroup).SwitchListCount; i++)
                            //     {
                            //         currentSelectData.outputIDList.Add(-1);
                            //     }
                            //     switchContainerGroupHeaderFlags.Clear();
                            // }));
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        using (new EditorGUILayout.VerticalScope())
                        {
                            EditorGUI.BeginDisabledGroup(currentSelectData.PlayMode != ContainerPlayMode.Continuous);
                            currentSelectData.crossFade = EditorGUILayout.BeginToggleGroup(new GUIContent("CrossFade", "交叉淡变"), currentSelectData.crossFade);
                            currentSelectData.crossFadeTime = EditorGUILayout.FloatField(new GUIContent("CrossFade Time"), currentSelectData.crossFadeTime);
                            currentSelectData.crossFadeType = (CrossFadeType)EditorGUILayout.EnumPopup(new GUIContent("CrossFade Type", "淡变类型"), currentSelectData.crossFadeType);
                            EditorGUILayout.EndToggleGroup();
                            EditorGUI.EndDisabledGroup();
                        }
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();

                        EditorGUI.indentLevel--;
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Group Setting", EditorStyles.boldLabel);
                        if (currentSelectData.switchGroupID != -1)
                        {
                            if (foldoutHeaderFlags.Count != switchGroupData.SwitchListCount)
                            {
                                foldoutHeaderFlags.Clear();
                            }
                            if (foldoutHeaderFlags.Count == 0)
                            {
                                for (int i = 0; i < switchGroupData.SwitchListCount; i++)
                                {
                                    foldoutHeaderFlags.Add(true);
                                }
                            }

                            scorllViewPosition = EditorGUILayout.BeginScrollView(scorllViewPosition, GUILayout.ExpandHeight(false));
                            for (int i = 0; i < switchGroupData.SwitchListCount; i++)
                            {
                                var rect = EditorGUILayout.GetControlRect();
                                rect.width -= GUI.skin.verticalScrollbar.fixedWidth;
                                var name = i == currentSelectData.defualtPlayIndex ? string.Format(switchGroupData.GetSwitchAt(i).name + "(default)") : switchGroupData.GetSwitchAt(i).name;
                                foldoutHeaderFlags[i] = EditorGUI.BeginFoldoutHeaderGroup(rect, foldoutHeaderFlags[i], name);
                                if (foldoutHeaderFlags[i])
                                {
                                    using (new EditorGUI.IndentLevelScope())
                                    {
                                        EditorGUILayout.BeginHorizontal();
                                        using (new EditorGUI.DisabledGroupScope(true))
                                        {
                                            var childData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(currentSelectData.outputIDList[i]);
                                            var selectedChildName = childData == null ? "null" : childData.name;
                                            EditorGUILayout.TextField(selectedChildName);
                                        }
                                        buttonRect = EditorGUILayout.GetControlRect();
                                        buttonRect.width -= GUI.skin.verticalScrollbar.fixedWidth;
                                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                        {
                                            ShowSwitchContainerChildrenListMenu(buttonRect, currentSelectData, i);
                                        }
                                        EditorGUILayout.EndHorizontal();
                                    }
                                }
                                if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ContextClick)
                                {
                                    ShowHeaderContextMenu(i, currentSelectData);
                                }
                                EditorGUI.EndFoldoutHeaderGroup();
                            }

                            EditorGUILayout.EndScrollView();
                        }
                    }

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        DrawGeneralSettingsSidePage(currentSelectData);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;
                case PropertyEditorTab.Effects:
                    DrawEffectsSetting(currentSelectData);
                    break;
                case PropertyEditorTab.Attenuation:
                    Draw3DSetting(currentSelectData);
                    break;
                case PropertyEditorTab.RTPC:
                    DrawRTPCSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.States:
                    DrawStateSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.OtherSetting:
                    DrawOtherSettingUnit(currentSelectData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void ShowSwitchGroupMenu(Rect rect, SwitchContainer currentSelectData)
        {
            GenericMenu menu = new GenericMenu();
            menu.allowDuplicateNames = true;
            foreach (var item in ManagerData.gameSyncsData)
            {
                if (item is SwitchGroup switchGroup)
                {
                    menu.AddItem(new GUIContent(switchGroup.name), currentSelectData.switchGroupID == switchGroup.id,
                        () =>
                        {
                            if (currentSelectData.switchGroupID != switchGroup.id)
                            {
                                currentSelectData.switchGroupID = switchGroup.id;
                                //初始化output数据，用于对应switch列表
                                currentSelectData.outputIDList = new List<int>();
                                for (int i = 0; i < AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(switchGroup.id).SwitchListCount; i++)
                                {
                                    currentSelectData.outputIDList.Add(-1);
                                }
                                foldoutHeaderFlags.Clear();
                                currentSelectData.defualtPlayIndex = 0;
                                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.SwitchContainerGroupSettingChange);
                            }
                        });
                }
            }
            menu.DropDown(rect);
        }

        private void ShowHeaderContextMenu(int listIndex, SwitchContainer currentSelectData)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Set As Default"), false, () =>
                {
                    currentSelectData.defualtPlayIndex = listIndex;
                });
            menu.ShowAsContext();
        }

        private void ShowSwitchContainerChildrenListMenu(Rect rect, SwitchContainer currentSelectedData, int outputListWaitForSelect)
        {
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };
            foreach (var id in currentSelectedData.ChildrenID)
            {
                var name = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(id).name;
                menu.AddItem(new GUIContent(name), currentSelectedData.outputIDList[outputListWaitForSelect] == id, (x) =>
                {
                    currentSelectedData.outputIDList[(int)x] = id;
                    manager.SyncDataToPlayable(currentSelectedData.id, AudioComponentDataChangeType.SwitchContainerGroupSettingChange);
                }, outputListWaitForSelect);
            }
            menu.DropDown(rect);
        }

        #endregion

        #region 绘制BlendContianer的属性编辑界面

        private void DrawBlendContainerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.BlendContainer) as BlendContainer;
            if (currentSelectData == null) return;

            if (blendContainerTracksDepthInfoList == null) blendContainerTracksDepthInfoList = new List<List<int>>();
            if (blendContainerTracksDepthInfoList.Count != currentSelectData.blendContainerTrackList.Count)
            {
                for (int i = 0; i < currentSelectData.blendContainerTrackList.Count; i++)
                {
                    blendContainerTracksDepthInfoList.Add(new List<int>());
                }
            }

            EditorGUI.BeginDisabledGroup(!myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    DrawNameUnit(currentSelectData);

                    EditorGUILayout.BeginHorizontal();

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentVerticalSpliterWidth) / 3 * 2)))
                    {
                        GUI.color = preGUIColor;

                        DrawGeneralSettingUnit(currentSelectData);

                        //DrawTransitionsUnit(currentSelectData);

                        EditorGUILayout.LabelField("Container Setting", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Mode"));
                        currentSelectData.PlayMode = (ContainerPlayMode)GUILayout.Toolbar((int)currentSelectData.PlayMode, Enum.GetNames(typeof(ContainerPlayMode)));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Set NewBlendTrack"));
                        var buttonRect = EditorGUILayout.GetControlRect();
                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent("Set GameParameter >>"), FocusType.Passive))
                        {
                            var menu = new GenericMenu();
                            foreach (var gameSyncse in ManagerData.gameSyncsData)
                            {
                                if (gameSyncse is GameParameter gameParameter)
                                {
                                    menu.AddItem(new GUIContent(gameParameter.name), false, () =>
                                    {
                                        currentSelectData.blendContainerTrackList.Add(new BlendContainerTrack(gameParameter.id));
                                    });
                                }
                            }
                            menu.DropDown(buttonRect);
                        }
                        EditorGUILayout.EndHorizontal();

                        EditorGUI.indentLevel--;

                        EditorGUILayout.Space();

                        EditorGUILayout.LabelField("BlendTacks Setting", EditorStyles.boldLabel);
                        scorllViewPosition = EditorGUILayout.BeginScrollView(scorllViewPosition, GUILayout.ExpandHeight(true));

                        for (var i = 0; i < currentSelectData.blendContainerTrackList.Count; i++)
                        {
                            var blendContainerTack = currentSelectData.blendContainerTrackList[i];
                            var selectedGameParameter =
                                AudioEditorManager.GetAEComponentDataByID<GameParameter>(blendContainerTack
                                    .gameParameterId);

                            float? xAxisMinValue = null;
                            float? xAxisMaxValue = null;
                            float? xAxisValue = null;
                            var gameParameterName = "null";
                            if (selectedGameParameter == null)
                            {
                                blendContainerTack.gameParameterId = -1;
                                xAxisMinValue = null;
                                xAxisMaxValue = null;
                            }
                            else
                            {
                                xAxisMinValue = selectedGameParameter.MinValue;
                                xAxisMaxValue = selectedGameParameter.MaxValue;
                                xAxisValue = selectedGameParameter.Value;
                                gameParameterName = selectedGameParameter.name;
                            }

                            EditorGUILayout.BeginHorizontal();
                            var selectGameParameterButtonRect =
                                EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false));
                            if (EditorGUI.DropdownButton(selectGameParameterButtonRect,
                                new GUIContent(gameParameterName), FocusType.Passive))
                            {
                                var menu = new GenericMenu();
                                foreach (var gameSyncse in ManagerData.gameSyncsData)
                                {
                                    if (gameSyncse is GameParameter gameParameter)
                                    {
                                        menu.AddItem(new GUIContent(gameParameter.name), false, (x) =>
                                        {
                                            currentSelectData.blendContainerTrackList[(int)x].gameParameterId = gameParameter.id;
                                        }, i);
                                    }
                                }
                                menu.DropDown(selectGameParameterButtonRect);
                            }
                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button("删除"))
                            {
                                currentSelectData.blendContainerTrackList.RemoveAt(i);
                                continue;
                            }
                            EditorGUILayout.EndHorizontal();
                            var graphRect = EditorGUILayout.GetControlRect(GUILayout.Height(130));

                            var graphdataRect = GraphCurveRendering.Draw(graphRect, null, xAxisMinValue, xAxisMaxValue,
                                0, 1, xAxisValue);
                            DrawBlendContainerChildrenBlendInfo(graphdataRect, currentSelectData.blendContainerTrackList[i], i);

                            //图表空白处点击取消当前选中的ChildBlendInfo
                            if (graphRect.Contains(Event.current.mousePosition) &&
                                Event.current.type == EventType.MouseDown)
                            {
                                GUIUtility.keyboardControl = -1;
                                myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();

                                if (blendContainerTracksDepthInfoList[i].Exists(x => x == -1))
                                {
                                    for (int j = 0; j < blendContainerTracksDepthInfoList[i].Count; j++)
                                    {
                                        blendContainerTracksDepthInfoList[i][j]++;
                                    }
                                }
                                Repaint();
                                Event.current.Use();
                            }

                            if (graphdataRect.Contains(Event.current.mousePosition) &&
                                Event.current.type == EventType.ContextClick)
                            {
                                var mousePosition = Event.current.mousePosition;
                                //设置右键菜单显示子类选项以设置ChildBlendInfo
                                var menu = new GenericMenu();
                                foreach (var childId in currentSelectData.ChildrenID)
                                {
                                    var childData =
                                        AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(childId);
                                    if (childData == null)
                                    {
                                        AudioEditorDebugLog.LogError("数据错误，无法找到子类数据");
                                        continue;
                                    }

                                    var funcData = new int[] { i, childId };
                                    menu.AddItem(new GUIContent(childData.name), false, (x) =>
                                    {
                                        //添加新的BlendInfo
                                        var mousePositionInGraphRect = Rect.PointToNormalized(graphdataRect, mousePosition);

                                        //currentSelectData.blendContainerTackList[((int[])x)[0]].childrenBlendInfoList.Add(new BlendContainerChildBlendInfo(((int[])x)[1]));
                                        currentSelectData.blendContainerTrackList[((int[])x)[0]]
                                            .AddNewChildBlendInfo(((int[])x)[1], mousePositionInGraphRect);
                                    }, funcData);
                                }
                                menu.AddSeparator(string.Empty);

                                //menu.AddItem(new GUIContent("All"),false, (x) =>
                                //{
                                //    //为所有未添加的child组件新增BlendInfo信息
                                //    foreach (var childId in currentSelectData.ChildrenID)
                                //    {
                                //        if (currentSelectData.blendContainerTackList[(int)x].childrenBlendInfoList
                                //            .Find(y => y.childId == childId) != null)
                                //        {
                                //            continue;
                                //        }
                                //        currentSelectData.blendContainerTackList[(int)x].childrenBlendInfoList.Add(new BlendContainerChildBlendInfo(childId));
                                //    }
                                //},i);

                                menu.ShowAsContext();

                                Event.current.Use();
                            }
                            //绘制当前选中的ChildBlendInfo的信息
                            if (blendContainerTracksDepthInfoList[i].Exists(x => x == -1) && selectedGameParameter != null)
                            {
                                var currentSelectChildBlendInfoIndex =
                                    blendContainerTracksDepthInfoList[i].FindIndex(x => x == -1);
                                var childBlendInfo = currentSelectData.blendContainerTrackList[i]
                                    .GetChildBlendInfo(currentSelectChildBlendInfoIndex);
                                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                                {
                                    EditorGUILayout.LabelField(new GUIContent("Xmin"), GUILayout.Width(40));
                                    var leftValueInGameParameterAxis =
                                        childBlendInfo.LeftXAxisValue *
                                        (selectedGameParameter.MaxValue - selectedGameParameter.MinValue);
                                    var newLeftValue = EditorGUILayout.DelayedFloatField(leftValueInGameParameterAxis);
                                    if (Math.Abs(newLeftValue - leftValueInGameParameterAxis) > 0.0001)
                                    {
                                        var newLeftValueInNormalized =
                                            newLeftValue / (selectedGameParameter.MaxValue - selectedGameParameter.MinValue);
                                        currentSelectData.blendContainerTrackList[i]
                                            .ResizeChildBlendInfoRect(currentSelectChildBlendInfoIndex, newLeftValueInNormalized, true);
                                    }
                                    EditorGUILayout.LabelField("Xmax", GUILayout.Width(40));
                                    var rightValueInGameParameterAxis =
                                        childBlendInfo.RightXAxisValue *
                                        (selectedGameParameter.MaxValue - selectedGameParameter.MinValue);
                                    var newRightValue = EditorGUILayout.DelayedFloatField(rightValueInGameParameterAxis);
                                    if (Math.Abs(newRightValue - rightValueInGameParameterAxis) > 0.0001)
                                    {
                                        var newRightValueInNormalized =
                                            newRightValue / (selectedGameParameter.MaxValue - selectedGameParameter.MinValue);
                                        currentSelectData.blendContainerTrackList[i]
                                            .ResizeChildBlendInfoRect(currentSelectChildBlendInfoIndex, newRightValueInNormalized, false);
                                    }

                                    EditorGUILayout.LabelField("Left EaseType", GUILayout.Width(90));
                                    EditorGUI.BeginDisabledGroup(childBlendInfo.LeftBlendXAxisValue <= childBlendInfo.LeftXAxisValue);
                                    childBlendInfo.leftEaseType =
                                        (EaseType)EditorGUILayout.EnumPopup(childBlendInfo.leftEaseType);
                                    EditorGUI.EndDisabledGroup();

                                    EditorGUILayout.LabelField("Right EaseType", GUILayout.Width(90));
                                    EditorGUI.BeginDisabledGroup(childBlendInfo.RightBlendXAxisValue >= childBlendInfo.RightXAxisValue);
                                    childBlendInfo.rightEaseType =
                                        (EaseType)EditorGUILayout.EnumPopup(childBlendInfo.rightEaseType);
                                    EditorGUI.EndDisabledGroup();
                                }
                            }

                            if (i != currentSelectData.blendContainerTrackList.Count - 1)
                            {
                                EditorGUILayout.Space();
                            }
                        }

                        EditorGUILayout.EndScrollView();

                    }

                    GUI.color = backgroundBoxColor;
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        DrawGeneralSettingsSidePage(currentSelectData);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;
                case PropertyEditorTab.Effects:
                    DrawEffectsSetting(currentSelectData);
                    break;
                case PropertyEditorTab.Attenuation:
                    Draw3DSetting(currentSelectData);
                    break;
                case PropertyEditorTab.RTPC:
                    DrawRTPCSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.States:
                    DrawStateSettingUnit(currentSelectData);
                    break;
                case PropertyEditorTab.OtherSetting:
                    DrawOtherSettingUnit(currentSelectData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawBlendContainerChildrenBlendInfo(Rect graphDataRect, BlendContainerTrack blendTrack, int trackInListOder)
        {
            if (blendContainerTracksDepthInfoList[trackInListOder] == null) blendContainerTracksDepthInfoList[trackInListOder] = new List<int>();
            if (blendContainerTracksDepthInfoList[trackInListOder].Count != blendTrack.ChildrenBlendInfoListCount)
            {
                blendContainerTracksDepthInfoList[trackInListOder].Clear();
                for (int i = 0; i < blendTrack.ChildrenBlendInfoListCount; i++)
                {
                    blendContainerTracksDepthInfoList[trackInListOder].Add(i);
                }
            }

            var trackDepthInfo = blendContainerTracksDepthInfoList[trackInListOder];
            //复制一份深度表，通过每轮遍历从复制表中寻找最小值，通过最小值获取其序列从而获取对应的ChildBlenInfo，并且从复制表中删除最小值
            var depthInfoCopy = trackDepthInfo.ToList();

            var preColor = GUI.color;

            var mousePositionInGraphRect = Rect.PointToNormalized(graphDataRect, Event.current.mousePosition);
            //拖出界面外则取消选中
            if ((mousePositionInGraphRect.x <= 0 || mousePositionInGraphRect.x >= 1) && Event.current.type == EventType.MouseDrag)
            {
                var selectedChildBlendInfoOrder =
                    blendContainerTracksDepthInfoList[trackInListOder].FindIndex(x => x == -1);
                if (selectedChildBlendInfoOrder != -1)
                {
                    if (leftAnchorRectDragInBlendContainerGraph && mousePositionInGraphRect.x <= 0)
                    {
                        blendTrack.ResizeChildBlendInfoRect(selectedChildBlendInfoOrder, 0, true);
                    }

                    if (rightAnchorRectDragInBlendContainerGraph && mousePositionInGraphRect.x >= 1)
                    {
                        blendTrack.ResizeChildBlendInfoRect(selectedChildBlendInfoOrder, 1, false);
                    }

                    for (int j = 0; j < blendContainerTracksDepthInfoList[trackInListOder].Count; j++)
                    {
                        blendContainerTracksDepthInfoList[trackInListOder][j]++;
                    }

                    leftAnchorRectDragInBlendContainerGraph = false;
                    rightAnchorRectDragInBlendContainerGraph = false;
                    Event.current.Use();
                }
            }

            using (new GUI.GroupScope(graphDataRect))
            {
                graphDataRect.x = graphDataRect.y = 0;

                for (var index = 0; index < blendTrack.ChildrenBlendInfoListCount; index++)
                {
                    //ChildBlendInfo的绘制顺序并非是ChildrenBlendInfoList的顺序（其顺序请查看定义）
                    //而是通过一个额外的深度表的顺序进行绘制，这很重要，可以让鼠标点击后将元素置于首位以确保下次点击相同的重叠区块时依旧是选中之前的元素
                    //从而获得正常的点击体验
                    var currentChildBlendInfoOrder = trackDepthInfo.FindIndex(x => x == depthInfoCopy.Min());
                    if (currentChildBlendInfoOrder == -1)
                    {
                        //当后续点击操作导致某ChildBlendInfo的深度发生变化时，Copy的列表没有更新，可能会导致数值对应不上
                        //可以直接跳过至下一次循环来处理
                        depthInfoCopy.Remove(depthInfoCopy.Min());
                        Repaint();
                        continue;
                    }
                    depthInfoCopy.Remove(depthInfoCopy.Min());
                    var childBlendInfo = blendTrack.GetChildBlendInfo(currentChildBlendInfoOrder);

                    var childAudioComponent =
                        AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(childBlendInfo.childId);
                    if (childAudioComponent == null)
                    {
                        AudioEditorDebugLog.LogError("数据出错");
                        blendTrack.RemoveChildBlendInfo(currentChildBlendInfoOrder);
                        Repaint();
                        return;
                    }
                    var childRect = Rect.zero;
                    childRect.x = graphDataRect.width * childBlendInfo.LeftXAxisValue;
                    childRect.width = graphDataRect.width * (childBlendInfo.RightXAxisValue - childBlendInfo.LeftXAxisValue);
                    childRect.height = graphDataRect.height;

                    var leftAnchorRect = childRect;
                    leftAnchorRect.width = 3f;
                    EditorGUIUtility.AddCursorRect(leftAnchorRect, MouseCursor.ResizeHorizontal);

                    var rightAnchorRect = childRect;
                    rightAnchorRect.xMin = rightAnchorRect.xMax - 3f;
                    EditorGUIUtility.AddCursorRect(rightAnchorRect, MouseCursor.ResizeHorizontal);


                    //为ChildRect定义事件行为
                    //左锚点拖动
                    var e = Event.current;
                    if (!leftAnchorRectDragInBlendContainerGraph && leftAnchorRect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
                    {
                        GUIUtility.keyboardControl = -1;
                        myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();

                        //如果有已选中的则全员后移一位
                        if (trackDepthInfo.Exists(x => x == -1))
                        {
                            // trackDepthInfo.ForEach((x)=> x+=1);
                            for (int j = 0; j < trackDepthInfo.Count; j++)
                            {
                                trackDepthInfo[j]++;
                            }
                        }
                        trackDepthInfo[currentChildBlendInfoOrder] = -1;

                        rightAnchorRectDragInBlendContainerGraph = false;
                        leftAnchorRectDragInBlendContainerGraph = true;
                        e.Use();
                    }

                    if (leftAnchorRectDragInBlendContainerGraph && graphDataRect.Contains(e.mousePosition) &&
                        trackDepthInfo[currentChildBlendInfoOrder] == -1 && e.type == EventType.MouseDrag)
                    {
                        blendTrack.ResizeChildBlendInfoRect(currentChildBlendInfoOrder, mousePositionInGraphRect.x, true);
                        e.Use();
                    }

                    if (leftAnchorRectDragInBlendContainerGraph && e.type == EventType.MouseUp)
                    {
                        leftAnchorRectDragInBlendContainerGraph = false;
                        e.Use();
                    }

                    //右锚点拖动
                    if (!rightAnchorRectDragInBlendContainerGraph && rightAnchorRect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
                    {
                        GUIUtility.keyboardControl = -1;
                        myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();

                        //如果有已选中的则全员后移一位
                        if (trackDepthInfo.Exists(x => x == -1))
                        {
                            trackDepthInfo.ForEach((x) => x += 1);
                            for (int j = 0; j < trackDepthInfo.Count; j++)
                            {
                                trackDepthInfo[j]++;
                            }
                        }
                        trackDepthInfo[currentChildBlendInfoOrder] = -1;

                        leftAnchorRectDragInBlendContainerGraph = false;
                        rightAnchorRectDragInBlendContainerGraph = true;
                        e.Use();
                    }

                    if (rightAnchorRectDragInBlendContainerGraph && graphDataRect.Contains(e.mousePosition) &&
                        trackDepthInfo[currentChildBlendInfoOrder] == -1 && e.type == EventType.MouseDrag)
                    {
                        blendTrack.ResizeChildBlendInfoRect(currentChildBlendInfoOrder, mousePositionInGraphRect.x, false);
                        e.Use();
                    }

                    if (rightAnchorRectDragInBlendContainerGraph && e.type == EventType.MouseUp)
                    {
                        rightAnchorRectDragInBlendContainerGraph = false;
                        e.Use();
                    }
                }

                //所有锚点的相应优先级比选中整体的优先级更高，所以需要锚点的事件相应遍历结束再遍历选中整体的事件
                depthInfoCopy = trackDepthInfo.ToList();
                for (var index = 0; index < blendTrack.ChildrenBlendInfoListCount; index++)
                {
                    var currentChildBlendInfoOrder = trackDepthInfo.FindIndex(x => x == depthInfoCopy.Min());
                    if (currentChildBlendInfoOrder == -1)
                    {
                        //当后续点击操作导致某ChildBlendInfo的深度发生变化时，Copy的列表没有更新，可能会导致数值对应不上
                        //可以直接跳过至下一次循环来处理
                        depthInfoCopy.Remove(depthInfoCopy.Min());
                        Repaint();
                        continue;
                    }

                    depthInfoCopy.Remove(depthInfoCopy.Min());
                    var childBlendInfo = blendTrack.GetChildBlendInfo(currentChildBlendInfoOrder);

                    var childRect = Rect.zero;
                    childRect.x = graphDataRect.width * childBlendInfo.LeftXAxisValue;
                    childRect.width = graphDataRect.width *
                                      (childBlendInfo.RightXAxisValue - childBlendInfo.LeftXAxisValue);
                    childRect.height = graphDataRect.height;

                    //整体移动
                    var e = Event.current;
                    if (childRect.Contains(e.mousePosition) && e.type == EventType.MouseDown)
                    {
                        GUIUtility.keyboardControl = -1;
                        myTreeViews[editorViewData.ProjectExplorerTabIndex].EndRename();


                        //如果有已选中的则全员后移一位
                        if (trackDepthInfo.Exists(x => x == -1))
                        {
                            //trackDepthInfo.ForEach((x) => x += 1);
                            for (int j = 0; j < trackDepthInfo.Count; j++)
                            {
                                trackDepthInfo[j]++;
                            }
                        }
                        trackDepthInfo[currentChildBlendInfoOrder] = -1;
                        leftAnchorRectDragInBlendContainerGraph = false;
                        rightAnchorRectDragInBlendContainerGraph = false;
                        e.Use();
                    }

                    if (graphDataRect.Contains(e.mousePosition) && trackDepthInfo[currentChildBlendInfoOrder] == -1 && e.type == EventType.MouseDrag)
                    {
                        blendTrack.DoChildBlendInfoTransform(currentChildBlendInfoOrder, e.delta.x / graphDataRect.width);
                        Event.current.Use();
                        Repaint();
                    }

                    if (childRect.Contains(e.mousePosition) && trackDepthInfo[currentChildBlendInfoOrder] == -1 &&
                        e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
                    {
                        trackDepthInfo.RemoveAt(currentChildBlendInfoOrder);
                        blendTrack.RemoveChildBlendInfo(currentChildBlendInfoOrder);
                        leftAnchorRectDragInBlendContainerGraph = false;
                        rightAnchorRectDragInBlendContainerGraph = false;
                        e.Use();
                        Repaint();
                    }

                    //设定在childRect上右键无法打开新建菜单
                    if (childRect.Contains(e.mousePosition) && e.type == EventType.ContextClick)
                    {
                        e.Use();
                    }
                }


                //绘制Rect的表现,深度优先级高的后绘制，以保证其在优先级低的前方
                depthInfoCopy = trackDepthInfo.ToList();
                for (var index = 0; index < blendTrack.ChildrenBlendInfoListCount; index++)
                {
                    var currentChildBlendInfoOrder = trackDepthInfo.FindIndex(x => x == depthInfoCopy.Max());
                    if (currentChildBlendInfoOrder == -1)
                    {
                        //当后续点击操作导致某ChildBlendInfo的深度发生变化时，Copy的列表没有更新，可能会导致数值对应不上
                        //可以直接跳过至下一次循环来处理
                        depthInfoCopy.Remove(depthInfoCopy.Max());
                        Repaint();
                        continue;
                    }

                    depthInfoCopy.Remove(depthInfoCopy.Max());
                    var childBlendInfo = blendTrack.GetChildBlendInfo(currentChildBlendInfoOrder);

                    var childAudioComponent =
                        AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(childBlendInfo.childId);
                    if (childAudioComponent == null)
                    {
                        AudioEditorDebugLog.LogError("数据出错");
                        blendTrack.RemoveChildBlendInfo(currentChildBlendInfoOrder);
                        Repaint();
                        return;
                    }

                    var childRect = Rect.zero;
                    childRect.x = graphDataRect.width * childBlendInfo.LeftXAxisValue;
                    childRect.width = graphDataRect.width * (childBlendInfo.RightXAxisValue - childBlendInfo.LeftXAxisValue);
                    childRect.height = graphDataRect.height;

                    var leftAnchorRect = childRect;
                    leftAnchorRect.width = trackDepthInfo[currentChildBlendInfoOrder] == -1 ? 3f : 1f;

                    var rightAnchorRect = childRect;
                    rightAnchorRect.xMin = rightAnchorRect.xMax - (trackDepthInfo[currentChildBlendInfoOrder] == -1 ? 3f : 1f);

                    GUI.color = colorRankList[currentChildBlendInfoOrder];
                    GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, trackDepthInfo[currentChildBlendInfoOrder] == -1 ? 0.3f : 0.15f);

                    GUI.DrawTexture(childRect, EditorGUIUtility.whiteTexture);

                    GUI.color = new Color(GUI.color.r, GUI.color.g, GUI.color.b, trackDepthInfo[currentChildBlendInfoOrder] == -1 ? 0.8f : 0.5f);
                    GUI.DrawTexture(leftAnchorRect, EditorGUIUtility.whiteTexture);
                    GUI.DrawTexture(rightAnchorRect, EditorGUIUtility.whiteTexture);
                    GUI.color = preColor;

                    if (childBlendInfo.LeftBlendXAxisValue > childBlendInfo.LeftXAxisValue)
                    {
                        var blendTransitionRect = graphDataRect;
                        blendTransitionRect.xMin = childBlendInfo.LeftXAxisValue * graphDataRect.width;
                        blendTransitionRect.xMax = childBlendInfo.LeftBlendXAxisValue * graphDataRect.width;
                        using (new GUI.GroupScope(blendTransitionRect))
                        {
                            var rect = blendTransitionRect;
                            rect.x = rect.y = 0;
                            AudioCurveRendering.DrawCurve(rect, f =>
                            {
                                //var value = Mathf.Clamp01(Easing.Get(childBlendInfo.leftEaseType).Invoke(f));
                                var value = childBlendInfo.GetValue(
                                    childBlendInfo.LeftXAxisValue + f *
                                    (childBlendInfo.LeftBlendXAxisValue - childBlendInfo.LeftXAxisValue));
                                var normalizedValue = (value - 0.5f) / 0.5f;
                                return normalizedValue;
                            }, colorRankList[currentChildBlendInfoOrder]);
                        }
                    }

                    if (childBlendInfo.RightBlendXAxisValue < childBlendInfo.RightXAxisValue)
                    {
                        var blendTransitionRect = graphDataRect;
                        blendTransitionRect.xMin = childBlendInfo.RightBlendXAxisValue * graphDataRect.width;
                        blendTransitionRect.xMax = childBlendInfo.RightXAxisValue * graphDataRect.width;
                        using (new GUI.GroupScope(blendTransitionRect))
                        {
                            var rect = blendTransitionRect;
                            rect.x = rect.y = 0;
                            AudioCurveRendering.DrawCurve(rect, f =>
                            {
                                //var value = Mathf.Clamp01(Easing.Get(childBlendInfo.rightEaseType).Invoke(f));
                                var value = childBlendInfo.GetValue(
                                    childBlendInfo.RightBlendXAxisValue + f *
                                    (childBlendInfo.RightXAxisValue - childBlendInfo.RightBlendXAxisValue));

                                var normalizedValue = (value - 0.5f) / 0.5f;
                                //normalizedValue *= -1;
                                return normalizedValue;
                            }, colorRankList[currentChildBlendInfoOrder]);
                        }
                    }

                    EditorGUI.LabelField(childRect, new GUIContent(childAudioComponent.name), whiteLableStyleSkin);
                }

            }

            GUI.color = preColor;
        }

        #endregion

        private void DrawEventPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.Event) as AEEvent;
            if (currentSelectData == null) return;

            DrawNameUnit(currentSelectData);
            if (GUILayout.Button("新建 >>", GUILayout.ExpandWidth(false)))
            {
                //最多允许创建15个执行事项
                if (currentSelectData.eventList.Count > 15) return;
                currentSelectData.NewEventUnit();
                whichEventUnitSelect = currentSelectData.eventList.Count - 1;
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUILayout.VerticalScope("box", GUILayout.Width((position.width - CurrentHorizontalSpliterHeight) / 3 * 2), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = backgroundBoxColor;
                GUILayout.Box(new GUIContent("Type"), GUILayout.Width((position.width - CurrentHorizontalSpliterHeight) / 5));
                GUILayout.Box(new GUIContent("Target"), GUILayout.ExpandWidth(true));
                GUI.color = preGUIColor;
                EditorGUILayout.EndHorizontal();
                for (int i = 0; i < currentSelectData.eventList.Count; i++)
                {
                    if (whichEventUnitSelect == i)
                    {
                        GUI.color = RectSelectedColor;
                    }
                    // GUI.color = whichEventUnitSelect == i ? rectSelectedColor : backgroundBoxColor;
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        var newEventType = (AEEventType)EditorGUILayout.Popup((int)currentSelectData.eventList[i].type, eventTypeNames, GUILayout.Width((position.width - CurrentHorizontalSpliterHeight) / 5 - 10));
                        if (newEventType != currentSelectData.eventList[i].type)
                        {
                            currentSelectData.eventList[i].type = newEventType;
                            currentSelectData.eventList[i].targetID = -1;
                        }
                        EditorGUILayout.Space();

                        switch (currentSelectData.eventList[i].type)
                        {
                            case AEEventType.Play:
                            case AEEventType.Pause:
                            case AEEventType.PauseAll:
                            case AEEventType.Resume:
                            case AEEventType.ResumeAll:
                            case AEEventType.Stop:
                            case AEEventType.StopAll:
                                //case AEEventType.SetToPreviousStep:
                                var targetId = currentSelectData.eventList[i].targetID;
                                var targetData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(targetId);
                                //if (targetData == null)
                                //{
                                //    targetId = currentSelectData.eventList[i].targetID = -1;
                                //}
                                var showName = targetId == -1 ? "null" : targetData == null ? "missing" : targetData.name;
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    if (targetData == null && targetId != -1)
                                    {
                                        GUI.color = Color.red;
                                    }
                                    EditorGUILayout.TextField(showName, GUILayout.ExpandWidth(true));
                                    GUI.color = preGUIColor;
                                }
                                var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                EditorGUI.BeginDisabledGroup(currentSelectData.eventList[i].type == AEEventType.StopAll || currentSelectData.eventList[i].type == AEEventType.PauseAll || currentSelectData.eventList[i].type == AEEventType.ResumeAll);
                                if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                {
                                    PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectAudio, (selectedID) =>
                                    {
                                        currentSelectData.eventList[i].targetID = selectedID;
                                    }));
                                }
                                EditorGUI.EndDisabledGroup();
                                break;
                            case AEEventType.SetSwitch:
                                var switchID = currentSelectData.eventList[i].targetID;
                                var showSwitchName = "null";
                                var selectSwitch = AudioEditorManager.GetAEComponentDataByID<Switch>(switchID);
                                if (currentSelectData.eventList[i].targetID != -1)
                                {
                                    showSwitchName = selectSwitch == null ? "missing" : selectSwitch.name;
                                }
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    if (selectSwitch == null && currentSelectData.eventList[i].targetID != -1)
                                    {
                                        GUI.color = Color.red;
                                    }
                                    EditorGUILayout.TextField(showSwitchName, GUILayout.ExpandWidth(true));
                                    GUI.color = preGUIColor;
                                }

                                buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                {
                                    PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectSwitch, (selectedID) =>
                                    {
                                        currentSelectData.eventList[i].targetID = selectedID;
                                    }));
                                }
                                break;
                            case AEEventType.SetState:
                                var stateID = currentSelectData.eventList[i].targetID;
                                var showStateName = "null";
                                var selectState = AudioEditorManager.GetAEComponentDataByID<State>(stateID);
                                if (currentSelectData.eventList[i].targetID != -1)
                                {
                                    showStateName = selectState == null ? "missing" : selectState.name;
                                }
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    if (showStateName == null && currentSelectData.eventList[i].targetID != -1)
                                    {
                                        GUI.color = Color.red;
                                    }
                                    EditorGUILayout.TextField(showStateName, GUILayout.ExpandWidth(true));
                                    GUI.color = preGUIColor;
                                }

                                buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                {
                                    PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectState, (selectedID) =>
                                    {
                                        currentSelectData.eventList[i].targetID = selectedID;
                                    }));
                                }
                                break;
                            case AEEventType.InstallBank:
                            case AEEventType.UnInstallBank:
                                if (currentSelectData.eventList[i].targetID != -1)
                                {
                                    currentSelectData.eventList[i].targetID = -1;
                                }
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    EditorGUILayout.TextField("null", GUILayout.ExpandWidth(true));
                                }
                                buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                EditorGUI.BeginDisabledGroup(true);
                                EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive);
                                EditorGUI.EndDisabledGroup();
                                break;
                            default:
                                Debug.LogError("未完成");
                                break;
                                //throw new ArgumentOutOfRangeException();
                        }

                        if (GUILayout.Button("删除", EditorStyles.miniButtonMid, GUILayout.ExpandWidth(false)))
                        {
                            whichEventUnitSelect = -1;
                            currentSelectData.eventList.RemoveAt(i);
                        }

                    }

                    var rect = GUILayoutUtility.GetLastRect();
                    if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                    {
                        whichEventUnitSelect = i;
                        Event.current.Use();
                        Repaint();
                    }

                    if (whichEventUnitSelect == i && rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
                    {
                        DataRegisterToUndo();
                        currentSelectData.eventList.RemoveAt(i);
                        whichEventUnitSelect = 0;
                        Event.current.Use();
                        Repaint();
                    }
                }
            }

            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                if (whichEventUnitSelect >= 0 && whichEventUnitSelect < currentSelectData.eventList.Count)
                {
                    var eventUnit = currentSelectData.eventList[whichEventUnitSelect];
                    switch (eventUnit.type)
                    {
                        case AEEventType.Play:
                            var newTriggerType =
                               (AEEventTriggerType)EditorGUILayout.EnumPopup(new GUIContent("Trigger Type"),
                                   eventUnit.triggerType);
                            if (newTriggerType != eventUnit.triggerType)
                            {
                                eventUnit.triggerType = newTriggerType;
                                eventUnit.triggerData = 0;
                                eventUnit.scope = AEEventScope.GameObject;
                            }
                            using (new EditorGUI.IndentLevelScope())
                            {
                                switch (eventUnit.triggerType)
                                {
                                    case AEEventTriggerType.Instance:
                                        break;
                                    case AEEventTriggerType.Delay:
                                        eventUnit.triggerData =
                                            EditorGUILayout.FloatField("Delay Time", eventUnit.triggerData);
                                        EditorGUILayout.Space();
                                        break;
                                    case AEEventTriggerType.Trigger:
                                        EditorGUILayout.BeginHorizontal();
                                        var targetAudioComponent =
                                            AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(
                                                Mathf.RoundToInt(eventUnit.triggerData));
                                        var showName = targetAudioComponent == null ? "null" : targetAudioComponent.name;
                                        using (new EditorGUI.DisabledGroupScope(true))
                                        {
                                            EditorGUILayout.TextField("Target", showName, GUILayout.ExpandWidth(true));
                                        }
                                        var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                        {
                                            PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectAudio, (selectedID) =>
                                            {
                                                eventUnit.triggerData = selectedID;
                                            }));
                                        }
                                        EditorGUILayout.EndHorizontal();
                                        eventUnit.triggerOccasion = (AEEventTriggerOccasion)EditorGUILayout.EnumPopup(new GUIContent("Play At"),
                                            eventUnit.triggerOccasion);
                                        eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                            eventUnit.scope);
                                        EditorGUILayout.Space();
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }

                            eventUnit.probability = EditorGUILayout.IntSlider(new GUIContent("Probability", "触发概率"),
                                eventUnit.probability, 0, 100);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeIn Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeIn Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.Pause:
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeOut Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeOut Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.PauseAll:
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeOut Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeOut Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.Resume:
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeIn Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeIn Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.ResumeAll:
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeIn Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeIn Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.Stop:

                            newTriggerType = (AEEventTriggerType)EditorGUILayout.EnumPopup(new GUIContent("Trigger Type"),
                                eventUnit.triggerType);
                            if (newTriggerType != eventUnit.triggerType)
                            {
                                eventUnit.triggerType = newTriggerType;
                                eventUnit.triggerData = 0;
                                eventUnit.scope = AEEventScope.GameObject;
                            }

                            using (new EditorGUI.IndentLevelScope())
                            {
                                switch (eventUnit.triggerType)
                                {
                                    case AEEventTriggerType.Instance:
                                        break;
                                    case AEEventTriggerType.Delay:
                                        eventUnit.triggerData =
                                            EditorGUILayout.FloatField("Delay Time", eventUnit.triggerData);
                                        EditorGUILayout.Space();
                                        break;
                                    case AEEventTriggerType.Trigger:
                                        EditorGUILayout.BeginHorizontal();
                                        var targetAudioComponent =
                                            AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(
                                                Mathf.RoundToInt(eventUnit.triggerData));
                                        var showName = targetAudioComponent == null ? "null" : targetAudioComponent.name;
                                        using (new EditorGUI.DisabledGroupScope(true))
                                        {
                                            EditorGUILayout.TextField("Target", showName, GUILayout.ExpandWidth(true));
                                        }
                                        var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                        {
                                            PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectAudio, (selectedID) =>
                                            {
                                                eventUnit.triggerData = selectedID;
                                            }));
                                        }
                                        EditorGUILayout.EndHorizontal();
                                        eventUnit.triggerOccasion = (AEEventTriggerOccasion)EditorGUILayout.EnumPopup(new GUIContent("Play At"),
                                            eventUnit.triggerOccasion);
                                        EditorGUILayout.Space();
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeOut Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeOut Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.StopAll:
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            eventUnit.FadeTime =
                                EditorGUILayout.FloatField("FadeOut Time",
                                    eventUnit.FadeTime);
                            eventUnit.fadeType =
                                (EaseType)EditorGUILayout.EnumPopup("FadeOut Type",
                                    eventUnit.fadeType);
                            break;
                        case AEEventType.SetSwitch:
                            newTriggerType =
                                (AEEventTriggerType)EditorGUILayout.EnumPopup(new GUIContent("Trigger Type"),
                                    eventUnit.triggerType);
                            if (newTriggerType != eventUnit.triggerType)
                            {
                                eventUnit.triggerType = newTriggerType;
                                eventUnit.triggerData = 0;
                                eventUnit.scope = AEEventScope.GameObject;
                            }

                            using (new EditorGUI.IndentLevelScope())
                            {
                                switch (eventUnit.triggerType)
                                {
                                    case AEEventTriggerType.Instance:
                                        break;
                                    case AEEventTriggerType.Delay:
                                        eventUnit.triggerData =
                                            EditorGUILayout.FloatField("Delay Time", eventUnit.triggerData);
                                        EditorGUILayout.Space();
                                        break;
                                    case AEEventTriggerType.Trigger:
                                        EditorGUILayout.BeginHorizontal();
                                        var targetAudioComponent =
                                            AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(
                                                Mathf.RoundToInt(eventUnit.triggerData));
                                        var showName = targetAudioComponent == null ? "null" : targetAudioComponent.name;
                                        using (new EditorGUI.DisabledGroupScope(true))
                                        {
                                            EditorGUILayout.TextField("Target", showName, GUILayout.ExpandWidth(true));
                                        }
                                        var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                        {
                                            PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectAudio, (selectedID) =>
                                            {
                                                eventUnit.triggerData = selectedID;
                                            }));
                                        }
                                        EditorGUILayout.EndHorizontal();
                                        eventUnit.triggerOccasion = (AEEventTriggerOccasion)EditorGUILayout.EnumPopup(new GUIContent("Play At"),
                                            eventUnit.triggerOccasion);
                                        EditorGUILayout.Space();
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            break;
                        case AEEventType.SetState:
                            newTriggerType = (AEEventTriggerType)EditorGUILayout.EnumPopup(new GUIContent("Trigger Type"),
                                eventUnit.triggerType);
                            if (newTriggerType != eventUnit.triggerType)
                            {
                                eventUnit.triggerType = newTriggerType;
                                eventUnit.triggerData = 0;
                                eventUnit.scope = AEEventScope.GameObject;
                            }

                            using (new EditorGUI.IndentLevelScope())
                            {
                                switch (eventUnit.triggerType)
                                {
                                    case AEEventTriggerType.Instance:
                                        break;
                                    case AEEventTriggerType.Delay:
                                        eventUnit.triggerData =
                                            EditorGUILayout.FloatField("Delay Time", eventUnit.triggerData);
                                        EditorGUILayout.Space();
                                        break;
                                    case AEEventTriggerType.Trigger:
                                        EditorGUILayout.BeginHorizontal();
                                        var targetAudioComponent =
                                            AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(
                                                Mathf.RoundToInt(eventUnit.triggerData));
                                        var showName = targetAudioComponent == null ? "null" : targetAudioComponent.name;
                                        using (new EditorGUI.DisabledGroupScope(true))
                                        {
                                            EditorGUILayout.TextField("Target", showName, GUILayout.ExpandWidth(true));
                                        }
                                        var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                                        if (EditorGUI.DropdownButton(buttonRect, new GUIContent("更改"), FocusType.Passive))
                                        {
                                            PopupWindow.Show(buttonRect, new SelectPopupWindow(WindowOpenFor.SelectAudio, (selectedID) =>
                                            {
                                                eventUnit.triggerData = selectedID;
                                            }));
                                        }
                                        EditorGUILayout.EndHorizontal();
                                        eventUnit.triggerOccasion = (AEEventTriggerOccasion)EditorGUILayout.EnumPopup(new GUIContent("Play At"),
                                            eventUnit.triggerOccasion);
                                        EditorGUILayout.Space();
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                            }
                            eventUnit.scope = (AEEventScope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                eventUnit.scope);
                            break;
                        case AEEventType.InstallBank:
                            EditorGUILayout.Space();
                            break;
                        case AEEventType.UnInstallBank:
                            EditorGUILayout.Space();
                            break;
                        //case AEEventType.SetToPreviousStep:
                        //    EditorGUILayout.Space();
                        //    break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSwitchGroupPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SwitchGroup) as SwitchGroup;
            if (currentSelectData == null) return;

            DrawNameUnit(currentSelectData);

            // if (GUILayout.Button("新建 >>", GUILayout.ExpandWidth(false)))
            // {
            //     var newSwitchID = myTreeModels[data.ProjectExplorerTabIndex].GenerateUniqueID();
            //     currentSelectData.GenerateSwitch(newSwitchID);
            //     foreach (var audioComponent in ManagerData.AudioComponentData)
            //     {
            //         if (audioComponent is SwitchContainer switchContainer && switchContainer.switchGroupID == currentSelectData.id)
            //         {
            //             switchContainer.outputIDList.Add(-1);
            //         }
            //     }
            //     switchContainerGroupHeaderFlags.Clear();
            // }

            // GUI.color = backgroundBoxColor;
            // using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true)))
            // {
            //     GUI.color = Color.white;
            //     EditorGUILayout.LabelField("Group List");
            // }
            // for (int i = 0; i < currentSelectData.SwitchListCount; i++)
            // {
            //     GUI.color = backgroundBoxColor;
            //     using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
            //     {
            //         GUI.color = Color.white;
            //         EditorGUILayout.BeginHorizontal();
            //         currentSelectData.FindSwitchAt(i).name = EditorGUILayout.DelayedTextField(currentSelectData.FindSwitchAt(i).name, GUILayout.Width(250));
            //         if (GUILayout.Button("更改", GUILayout.ExpandWidth(false)))
            //         {
            //             Debug.LogWarning("未完成");
            //         }
            //         if (GUILayout.Button("删除", GUILayout.ExpandWidth(false)))
            //         {
            //             currentSelectData.RemoveSwitchAt(i);
            //             foreach (var audioComponent in ManagerData.AudioComponentData)
            //             {
            //                 if (audioComponent is SwitchContainer switchContainer && switchContainer.switchGroupID == currentSelectData.id)
            //                 {
            //                     if (switchContainer.defualtPlayIndex == i)
            //                     {
            //                         switchContainer.defualtPlayIndex = 0;
            //                     }
            //                     switchContainer.outputIDList.Remove(i);
            //                 }
            //             }
            //             switchContainerGroupHeaderFlags.Clear();
            //         }
            //         EditorGUILayout.EndHorizontal();
            //     }
            // }
        }

        private void DrawStateGroupProperty(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.StateGroup) as StateGruop;
            if (currentSelectData == null) return;

            DrawNameUnit(currentSelectData);
            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            var buttonrect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false));
            if (EditorGUI.DropdownButton(buttonrect, new GUIContent("New Transition >>"), FocusType.Passive))
            {
                var menu = new GenericMenu();
                for (int i = 0; i < currentSelectData.StateListCount; i++)
                {
                    for (int j = 0; j < currentSelectData.StateListCount; j++)
                    {
                        bool check = i == j;
                        //判断是否已有当前选项的State过渡项
                        foreach (var customStateTransition in currentSelectData.customStateTransitionList)
                        {
                            if (customStateTransition.Equal(currentSelectData.GetStateAt(i), currentSelectData.GetStateAt(j)))
                            {
                                check = true;
                                break;
                            }
                        }
                        if (check)
                        {
                            continue;
                        }
                        menu.AddItem(new GUIContent(currentSelectData.GetStateAt(i).name + "/" + currentSelectData.GetStateAt(j).name), false,
                            (newStateTransition) =>
                            {
                                currentSelectData.customStateTransitionList.Add(newStateTransition as StateTransition);
                            }, new StateTransition(currentSelectData.GetStateAt(i), currentSelectData.GetStateAt(j), currentSelectData.defaultTransitionTime));
                    }
                }
                menu.DropDown(buttonrect);
            }

            EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            currentSelectData.defaultTransitionTime = EditorGUILayout.FloatField(
                new GUIContent("Default Transition Time"), currentSelectData.defaultTransitionTime,
                GUILayout.ExpandWidth(false));
            EditorGUILayout.BeginHorizontal(GUILayout.Width(600));
            // using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
            // {
            //     
            // }
            GUI.color = backgroundBoxColor;
            GUILayout.Box("from", GUILayout.Width(100));
            GUILayout.Box("time", GUILayout.Width(200));
            GUILayout.Box("to", GUILayout.Width(100));
            GUILayout.Box("<-->", GUILayout.Width(50));
            GUILayout.Box("option");
            GUI.color = preGUIColor;
            EditorGUILayout.EndHorizontal();

            scorllViewPosition =
                GUILayout.BeginScrollView(scorllViewPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            for (int i = 0; i < currentSelectData.customStateTransitionList.Count; i++)
            {
                GUI.color = backgroundBoxColor;
                using (new GUILayout.HorizontalScope("box", GUILayout.Width(500)))
                {
                    GUI.color = preGUIColor;
                    //  EditorGUILayout.BeginHorizontal( GUILayout.Width(500));
                    var state = AudioEditorManager.GetAEComponentDataByID<State>(currentSelectData.customStateTransitionList[i].sourceStateID);
                    EditorGUILayout.LabelField(state.name, GUILayout.Width(100));
                    GUILayout.Space(20);
                    currentSelectData.customStateTransitionList[i].transitionTime = EditorGUILayout.FloatField(currentSelectData.customStateTransitionList[i].transitionTime, GUILayout.Width(160));
                    GUILayout.Space(20);
                    state = AudioEditorManager.GetAEComponentDataByID<State>(currentSelectData.customStateTransitionList[i].destinationStateID);
                    EditorGUILayout.LabelField(state.name, GUILayout.Width(100));
                    var check = false;
                    for (int j = 0; j < currentSelectData.customStateTransitionList.Count; j++)
                    {
                        if (i == j) continue;
                        if (currentSelectData.customStateTransitionList[i]
                            .Relate(currentSelectData.customStateTransitionList[j]))
                        {
                            check = true;
                            break;
                        }
                    }
                    using (new EditorGUI.DisabledGroupScope(check))
                    {
                        currentSelectData.customStateTransitionList[i].canRevert = EditorGUILayout.Toggle(currentSelectData.customStateTransitionList[i].canRevert, GUILayout.ExpandWidth(false));
                    }

                    if (GUILayout.Button("删除"))
                    {
                        currentSelectData.customStateTransitionList.RemoveAt(i);
                    }
                    //  EditorGUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawGameParameterProperty(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.GameParameter) as GameParameter;
            if (currentSelectData == null) return;

            DrawNameUnit(currentSelectData);
            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
            {
                GUI.color = preGUIColor;
                currentSelectData.MinValue = EditorGUILayout.FloatField("Min Value", currentSelectData.MinValue,
                    GUILayout.ExpandWidth(false));
                currentSelectData.MaxValue = EditorGUILayout.FloatField("Max Value", currentSelectData.MaxValue,
                    GUILayout.ExpandWidth(false));
                currentSelectData.DefaultValue =
                    EditorGUILayout.FloatField("Default Value", currentSelectData.DefaultValue,
                        GUILayout.ExpandWidth(false));
            }
        }


        /// <summary>
        /// 获取当前列表树中选择对应ID的数据
        /// </summary>
        /// <param name="id"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private AEComponent GetCurrentSelectData(int id, AEComponentType type)
        {
            AEComponent currentSelectData;
            switch (type)
            {
                case AEComponentType.SoundSFX:
                case AEComponentType.RandomContainer:
                case AEComponentType.SequenceContainer:
                case AEComponentType.SwitchContainer:
                case AEComponentType.BlendContainer:
                case AEComponentType.ActorMixer:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(id);
                    break;
                case AEComponentType.Event:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEEvent>(id);
                    break;
                case AEComponentType.SwitchGroup:
                case AEComponentType.StateGroup:
                case AEComponentType.Switch:
                case AEComponentType.State:
                case AEComponentType.GameParameter:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEGameSyncs>(id);
                    break;
                default:
                    AudioEditorDebugLog.LogError("id匹配发生错误，请检查");
                    return null;
            }
            if (currentSelectData == null)
            {
                AudioEditorDebugLog.LogError("发生严重的数据错误");
                return null;
            }
            var parent = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectData.id).parent as MyTreeElement;
            if (currentSelectData is AEAudioComponent audioComponent)
            {
                audioComponent.isContianerChild = parent.type != AEComponentType.WorkUnit;
            }

            return currentSelectData;
        }

        #region 绘制属性编辑界面的一些单元

        private void DrawPropertyEditorTitleToolBar(AEAudioComponent currentSelectData)
        {
            EditorGUILayout.BeginHorizontal();
            var newPropertyEditorTabIndex = (PropertyEditorTab)
                GUILayout.Toolbar((int)PropertyEditorTabIndex, propertyEditorTabNames, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents, GUILayout.ExpandWidth(false));
            if ((int)newPropertyEditorTabIndex != (int)PropertyEditorTabIndex)
            {
                PropertyEditorTabIndex = newPropertyEditorTabIndex;
            }
            var rect = EditorGUILayout.GetControlRect();
            EditorGUILayout.EndHorizontal();
            //GUILayout.Space(5);

            if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("复制此页数据至/父级"), false, () =>
                {
                    DataRegisterToUndo();
                    var parentTreeElement = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
                    var tempList = new HashSet<int> { parentTreeElement.id };
                    DeepCopypropertyEditorTabPageData(currentSelectData, tempList);
                });
                menu.AddItem(new GUIContent("复制此页数据至/所有同级"), false, () =>
                {
                    DataRegisterToUndo();
                    var parentTreeElement = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
                    var tempList = new HashSet<int>();
                    foreach (var child in parentTreeElement.children)
                    {
                        tempList.Add(child.id);
                    }
                    DeepCopypropertyEditorTabPageData(currentSelectData, tempList);
                });
                menu.AddItem(new GUIContent("复制此页数据至/父级及其子级"), false, () =>
                {
                    DataRegisterToUndo();
                    var parentTreeElement = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
                    var tempList = new HashSet<int>();
                    foreach (var child in parentTreeElement.children)
                    {
                        tempList.Add(child.id);
                    }
                    tempList.Add(parentTreeElement.id);
                    DeepCopypropertyEditorTabPageData(currentSelectData, tempList);
                });
                menu.AddItem(new GUIContent("复制此页数据至/所属的工作单元下的所有子级"), false, () =>
                {
                    DataRegisterToUndo();
                    var parentworkUnitTreeElement = myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
                    int i = 0;
                    while ((parentworkUnitTreeElement as MyTreeElement).type != AEComponentType.WorkUnit)
                    {
                        parentworkUnitTreeElement = parentworkUnitTreeElement.parent;
                        i++;
                        if (i == 100) break;
                    }
                    Debug.Log(parentworkUnitTreeElement.name);
                    if ((parentworkUnitTreeElement as MyTreeElement) == myTreeModels[editorViewData.ProjectExplorerTabIndex].root || i == 100)
                    {
                        Debug.LogWarning("寻找不到上级WorkUnit，请检查");
                        return;
                    }
                    var tempList = new HashSet<int>();
                    (parentworkUnitTreeElement as MyTreeElement).GetAllChildrenIDByDeepSearch(ref tempList);
                    foreach (var id in tempList)
                    {
                        Debug.Log(id);
                    }
                    DeepCopypropertyEditorTabPageData(currentSelectData, tempList);
                });
                menu.ShowAsContext();
            }
        }


        private void DrawNameUnit(AEComponent currentSelectData)
        {
            EditorGUILayout.BeginHorizontal();

            var name = EditorGUILayout.TextField(currentSelectData.name);
            if (name != currentSelectData.name && name != string.Empty)
            {
                GetTreeListElementByID(currentSelectData.id).name = name;
                myTreeViews[editorViewData.ProjectExplorerTabIndex].Reload();
            }
            currentSelectData.name = GetTreeListElementByID(currentSelectData.id).name;
            GUILayout.Label("id:");
            EditorGUI.BeginDisabledGroup(editorViewData.ProjectExplorerTabIndex != 1);
            var newID = EditorGUILayout.DelayedIntField(currentSelectData.id);
            if (newID != currentSelectData.id)
            {
                //只能修改Event的id
                if (editorViewData.ProjectExplorerTabIndex == 1)
                {
                    if (myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(newID) == null)
                    {
                        myTreeModels[editorViewData.ProjectExplorerTabIndex].Find(currentSelectData.id).id = newID;
                        currentSelectData.id = newID;
                        editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID = newID;
                        myTreeViews[editorViewData.ProjectExplorerTabIndex].Reload();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
        }

        private void DrawGeneralSettingUnit(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("General Setting", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (currentSelectData.randomVolume == false)
            {
                currentSelectData.volume = EditorGUILayout.Slider(new GUIContent("Volume", "音量"), currentSelectData.volume, 0f, 1f);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.MinMaxSlider(new GUIContent("Volume", "音量"), ref currentSelectData.minVolume,
                        ref currentSelectData.maxVolume, 0, 1);
                    EditorGUI.indentLevel--;
                    currentSelectData.minVolume = EditorGUILayout.FloatField(currentSelectData.minVolume, GUILayout.Width(50));
                    currentSelectData.maxVolume = EditorGUILayout.FloatField(currentSelectData.maxVolume, GUILayout.Width(50));
                    EditorGUI.indentLevel++;
                }
            }

            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Event.current.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("random"), currentSelectData.randomVolume, () =>
                 {
                     currentSelectData.randomVolume = !currentSelectData.randomVolume;
                     Repaint();
                 });
                menu.ShowAsContext();
                Event.current.Use();
            }

            if (currentSelectData.randomPitch == false)
            {
                currentSelectData.pitch = EditorGUILayout.Slider(new GUIContent("Pitch", "音高"), currentSelectData.pitch, 0, 3);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.MinMaxSlider(new GUIContent("Pitch", "音高"), ref currentSelectData.minPitch,
                        ref currentSelectData.maxPitch, 0, 3);
                    EditorGUI.indentLevel--;
                    currentSelectData.minPitch = EditorGUILayout.FloatField(currentSelectData.minPitch, GUILayout.Width(50));
                    currentSelectData.maxPitch = EditorGUILayout.FloatField(currentSelectData.maxPitch, GUILayout.Width(50));
                    EditorGUI.indentLevel++;
                }
            }

            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition) && Event.current.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("random"), currentSelectData.randomPitch, () =>
                {
                    currentSelectData.randomPitch = !currentSelectData.randomPitch;
                    Repaint();
                });
                menu.ShowAsContext();
                Event.current.Use();
            }

            EditorGUI.BeginDisabledGroup(currentSelectData is ActorMixer);
            currentSelectData.delayTime = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Initial Delay"), currentSelectData.delayTime, GUILayout.ExpandWidth(false)), 0, 999);
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                // Debug.Log("数据刷新 In GeneralSettingUnit");
            }
        }

        private void DrawTransitionsUnit(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUI.indentLevel++;
                currentSelectData.fadeIn = EditorGUILayout.BeginToggleGroup("Fade In", currentSelectData.fadeIn);
                var newvalue = EditorGUILayout.FloatField(new GUIContent("FadeIn Time", "淡入时间"), currentSelectData.fadeInTime);
                if (newvalue < 0)
                {
                    newvalue = 0;
                }
                currentSelectData.fadeInTime = newvalue;
                currentSelectData.fadeInType = (EaseType)EditorGUILayout.EnumPopup(new GUIContent("FadeIn Type", "淡入类型"), currentSelectData.fadeInType);
                EditorGUILayout.EndToggleGroup();
                EditorGUI.indentLevel--;

            }

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(currentSelectData is ActorMixer);
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUI.indentLevel++;

                currentSelectData.fadeOut = EditorGUILayout.BeginToggleGroup(new GUIContent("Fade Out", "淡出"), currentSelectData.fadeOut);
                var newvalue = EditorGUILayout.FloatField(new GUIContent("FadeOut Time", "淡出时间"), currentSelectData.fadeOutTime);
                if (newvalue < 0)
                {
                    newvalue = 0;
                }
                currentSelectData.fadeOutTime = newvalue;
                currentSelectData.fadeOutType = (EaseType)EditorGUILayout.EnumPopup(new GUIContent("FadeOut Type", "淡出类型"), currentSelectData.fadeOutType);
                EditorGUILayout.EndToggleGroup();
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                //Debug.Log("数据刷新 In DrawTransitionsUnit");
            }
        }

        private void DrawGeneralSettingsSidePage(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild == false);
            var newOverrideActorMixer = GUILayout.Toggle((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.OutputMixerGroup) != 0, "Override OutputMixer");
            if (newOverrideActorMixer)
            {
                currentSelectData.overrideFunctionType |= AEComponentDataOverrideType.OutputMixerGroup;
            }
            else
            {
                currentSelectData.overrideFunctionType &= ~AEComponentDataOverrideType.OutputMixerGroup;
            }
            
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.OutputMixerGroup) == 0 && currentSelectData.isContianerChild);
            currentSelectData.outputMixer = EditorGUILayout.ObjectField(currentSelectData.outputMixer, typeof(AudioMixerGroup), true) as AudioMixerGroup;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            //currentSelectData.mute = EditorGUILayout.ToggleLeft("Mute", currentSelectData.mute);
            currentSelectData.loop = EditorGUILayout.BeginToggleGroup("Loop", currentSelectData.loop);
            using (new EditorGUI.IndentLevelScope())
            {
                var toggleResult = EditorGUILayout.ToggleLeft(new GUIContent("Infinite"), currentSelectData.loopInfinite, GUILayout.Width(100));
                if (toggleResult) currentSelectData.loopInfinite = true;
                EditorGUILayout.BeginHorizontal();
                toggleResult = EditorGUILayout.ToggleLeft(new GUIContent("Loop Of Times"), !currentSelectData.loopInfinite, GUILayout.Width(130));
                if (toggleResult) currentSelectData.loopInfinite = false;
                EditorGUI.BeginDisabledGroup(!toggleResult);
                var newIndex = EditorGUILayout.DelayedIntField(currentSelectData.loopTimes);
                if (newIndex > 1 && newIndex != currentSelectData.loopTimes)
                {
                    currentSelectData.loopTimes = newIndex;
                    manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.LoopIndexChange);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndToggleGroup();

            //GUILayout.Box("待施工", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                //Debug.Log("数据刷新 In DrawGeneralSettingsSidePage");
            }
        }

        private void DrawEffectsSetting(AEAudioComponent currentSelectData)
        {
            if (foldoutHeaderFlags.Count == 0 || foldoutHeaderFlags.Count != currentSelectData.effectSettings.Count)
            {
                foldoutHeaderFlags.Clear();
                for (int i = 0; i < currentSelectData.effectSettings.Count; i++)
                {
                    foldoutHeaderFlags.Add(true);
                }
            }

            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild == false);
            var newOverrideEffect = GUILayout.Toggle((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.Effect) == AEComponentDataOverrideType.Effect, "Override Effects");
            if (newOverrideEffect)
            {
                currentSelectData.overrideFunctionType |= AEComponentDataOverrideType.Effect;
            }
            else
            {
                currentSelectData.overrideFunctionType &= ~AEComponentDataOverrideType.Effect;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild && (currentSelectData.overrideFunctionType & AEComponentDataOverrideType.Effect) != AEComponentDataOverrideType.Effect);

            var buttonRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false));
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent("新建 >>"), FocusType.Passive))
            {
                var menu = new GenericMenu();
                var effectTypeNames = Enum.GetNames(typeof(AEEffectType));
                for (int i = 0; i < effectTypeNames.Length; i++)
                {
                    menu.AddItem(new GUIContent(effectTypeNames[i]), false, (x) =>
                    {
                        DataRegisterToUndo();
                        var targetType = (AEEffectType)x;
                        foreach (var effectSetting in currentSelectData.effectSettings)
                        {
                            if (effectSetting.type == targetType)
                            {
                                return;
                            }
                        }
                        switch (targetType)
                        {
                            case AEEffectType.LowPassFilter:
                                currentSelectData.effectSettings.Add(new AELowPassFilter());
                                break;
                            case AEEffectType.HighPassFilter:
                                currentSelectData.effectSettings.Add(new AEHighPassFilter());
                                break;
                            case AEEffectType.ReverbFilter:
                                currentSelectData.effectSettings.Add(new AEReverbFilter());
                                break;
                            //case AEEffectType.ReverbZoneFilter:
                            //    currentSelectData.effectSettings.Add(new AEReverbZone());
                            //    break;
                            case AEEffectType.EchoFilter:
                                currentSelectData.effectSettings.Add(new AEEchoFilter());
                                break;
                            case AEEffectType.ChorusFilter:
                                currentSelectData.effectSettings.Add(new AEChorusFilter());
                                break;
                            case AEEffectType.DistortionFilter:
                                currentSelectData.effectSettings.Add(new AEDistortionFilter());
                                break;
                        }
                        foldoutHeaderFlags.Add(true);
                        manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.AddEffect);
                    }, i);
                }
                menu.DropDown(buttonRect);
            }
            GUI.color = backgroundBoxColor;
            using (new GUILayout.VerticalScope("box"))
            {
                GUI.color = preGUIColor;
                scorllViewPosition = EditorGUILayout.BeginScrollView(scorllViewPosition);
                for (int i = 0; i < currentSelectData.effectSettings.Count; i++)
                {
                    var effectSetting = currentSelectData.effectSettings[i];
                    foldoutHeaderFlags[i] = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutHeaderFlags[i],
                        new GUIContent(effectSetting.type.ToString()),
                        menuAction: rect =>
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Delete"), false, () =>
                             {
                                 DataRegisterToUndo();
                                 var deleteEffectType = effectSetting.type;
                                 currentSelectData.effectSettings.Remove(effectSetting);
                                 foldoutHeaderFlags.Clear();
                                 manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.DeleteEffect, deleteEffectType);
                             });
                            menu.AddItem(new GUIContent("ResetData"), false, () =>
                             {
                                 effectSetting.Reset();
                                 manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                             });
                            menu.DropDown(rect);
                        });
                    var hearderRect = GUILayoutUtility.GetLastRect();
                    if (hearderRect.Contains(Event.current.mousePosition) &&
                        Event.current.type == EventType.ContextClick)
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            DataRegisterToUndo();
                            var deleteEffectType = effectSetting.type;
                            currentSelectData.effectSettings.Remove(effectSetting);
                            foldoutHeaderFlags.Clear();
                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.DeleteEffect, deleteEffectType);
                        });
                        menu.AddItem(new GUIContent("ResetData"), false, () =>
                        {
                            effectSetting.Reset();
                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                        });
                        menu.ShowAsContext();
                    }
                    if (foldoutHeaderFlags[i])
                    {
                        EditorGUI.indentLevel++;
                        switch (effectSetting.type)
                        {
                            case AEEffectType.LowPassFilter:
                                var lowpassFilter = effectSetting as AELowPassFilter;
                                if (currentSelectData.attenuationCurveSettings.Find((x) =>
                                    x.attenuationCurveType == AttenuationCurveType.LowPass) == null)
                                {
                                    lowpassFilter.CutoffFrequency =
                                        EditorGUILayout.Slider(new GUIContent("CutoffFrequency", "截止频率，单位为Hz"), lowpassFilter.CutoffFrequency, 10f, 22000f);
                                }
                                else
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.PrefixLabel(new GUIContent("CutoffFrequency", "截止频率，单位为Hz"));
                                    EditorGUILayout.LabelField("由曲线控制", EditorStyles.toolbarButton);
                                    EditorGUILayout.EndHorizontal();
                                }
                                lowpassFilter.LowPassResonanceQ = EditorGUILayout.Slider(new GUIContent("LowPassResonanceQ", "谐振因子,确定滤波器的自谐振衰减了多少"),
                                    lowpassFilter.LowPassResonanceQ, 1, 10);
                                break;
                            case AEEffectType.HighPassFilter:
                                var highpassFilter = effectSetting as AEHighPassFilter;
                                highpassFilter.CutoffFrequency =
                                    EditorGUILayout.Slider(new GUIContent("CutoffFrequency", "截止频率，单位为Hz"), highpassFilter.CutoffFrequency, 10f, 22000f);
                                highpassFilter.HighPassResonanceQ = EditorGUILayout.Slider(new GUIContent("LowPassResonanceQ", "谐振因子,确定滤波器的自谐振衰减了多少"),
                                    highpassFilter.HighPassResonanceQ, 1, 10);
                                break;
                            case AEEffectType.ReverbFilter:
                                // var reverbFilter = effectSetting as AEReverbFilter;
                                if (effectSetting is AEReverbFilter reverbFilter)
                                {
                                    var newPreset = (AudioReverbPreset)EditorGUILayout.EnumPopup(
                                        new GUIContent("Preset", "预设"),
                                        reverbFilter.preset);
                                    if (newPreset != reverbFilter.preset)
                                    {
                                        effectSettingObject.GetComponent<AudioReverbFilter>().reverbPreset = newPreset;
                                        reverbFilter.SetData(effectSettingObject.GetComponent<AudioReverbFilter>());
                                        effectSettingObject.GetComponent<AudioReverbFilter>().reverbPreset =
                                            AudioReverbPreset.Off;
                                    }

                                    EditorGUI.BeginDisabledGroup(reverbFilter.preset != AudioReverbPreset.User);
                                    reverbFilter.dryLevel = EditorGUILayout.IntSlider(new GUIContent("Dry Level"),
                                        (int)reverbFilter.dryLevel, -10000, 0);
                                    reverbFilter.room = EditorGUILayout.IntSlider(new GUIContent("Room"),
                                        (int)reverbFilter.room,
                                        -10000, 0);
                                    reverbFilter.roomHF = EditorGUILayout.IntSlider(new GUIContent("Room HF"),
                                        (int)reverbFilter.roomHF, -10000, 0);
                                    reverbFilter.roomLF = EditorGUILayout.IntSlider(new GUIContent("Room LF"),
                                        (int)reverbFilter.roomLF, -10000, 0);
                                    reverbFilter.decayTime = EditorGUILayout.Slider(new GUIContent("Decay Time"),
                                        reverbFilter.decayTime, 0.1f, 20f);
                                    reverbFilter.decayHFRatio = EditorGUILayout.Slider(new GUIContent("Decay HF Ratio"),
                                        reverbFilter.decayHFRatio, 0.1f, 2f);
                                    reverbFilter.reflectionsLevel = EditorGUILayout.IntSlider(
                                        new GUIContent("Reflections Level"),
                                        (int)reverbFilter.reflectionsLevel, -10000, 1000);
                                    reverbFilter.reflectionsDelay = EditorGUILayout.Slider(
                                        new GUIContent("Reflections Delay"),
                                        reverbFilter.reflectionsDelay, 0, 0.3f);
                                    reverbFilter.reverbLevel = EditorGUILayout.IntSlider(new GUIContent("Reverb Level"),
                                        (int)reverbFilter.reverbLevel, -10000, 2000);
                                    reverbFilter.reverbDelay = EditorGUILayout.Slider(new GUIContent("Reverb Delay"),
                                        reverbFilter.reverbDelay, 0, 0.1f);
                                    reverbFilter.hfReference = EditorGUILayout.IntSlider(new GUIContent("HF Reference"),
                                        (int)reverbFilter.hfReference, 1000, 20000);
                                    reverbFilter.lfReference = EditorGUILayout.IntSlider(new GUIContent("LF Reference"),
                                        (int)reverbFilter.lfReference, 20, 1000);
                                    reverbFilter.diffusion = EditorGUILayout.Slider(new GUIContent("Diffusion"),
                                        reverbFilter.diffusion, 0, 100f);
                                    reverbFilter.density = EditorGUILayout.Slider(new GUIContent("Density"),
                                        reverbFilter.density, 0, 100f);
                                    EditorGUI.EndDisabledGroup();
                                }

                                break;
                                {
                                    //case AEEffectType.ReverbZoneFilter:
                                    //    if (effectSetting is AEReverbZone reverbZoneFilter)
                                    //    {
                                    //        reverbZoneFilter.minDistance = EditorGUILayout.FloatField(new GUIContent("Min Distance"), reverbZoneFilter.minDistance);
                                    //        reverbZoneFilter.maxDistance = EditorGUILayout.FloatField(new GUIContent("Max Distance"), reverbZoneFilter.maxDistance);
                                    //        EditorGUILayout.Space();
                                    //        var newPreset = (AudioReverbPreset)EditorGUILayout.EnumPopup(new GUIContent("Preset", "预设"),
                                    //            reverbZoneFilter.preset);
                                    //        if (newPreset != reverbZoneFilter.preset)
                                    //        {
                                    //            effectSettingObject.GetComponent<AudioReverbZone>().reverbPreset = newPreset;
                                    //            reverbZoneFilter.SetData(effectSettingObject.GetComponent<AudioReverbZone>());
                                    //            effectSettingObject.GetComponent<AudioReverbZone>().reverbPreset = AudioReverbPreset.Off;
                                    //        }
                                    //        EditorGUI.BeginDisabledGroup(reverbZoneFilter.preset != AudioReverbPreset.User);
                                    //        reverbZoneFilter.room = EditorGUILayout.IntSlider(new GUIContent("Room"), reverbZoneFilter.room, -10000, 0);
                                    //        reverbZoneFilter.roomHF = EditorGUILayout.IntSlider(new GUIContent("Room HF"), reverbZoneFilter.roomHF, -10000, 0);
                                    //        reverbZoneFilter.roomLF = EditorGUILayout.IntSlider(new GUIContent("Room LF"), reverbZoneFilter.roomLF, -10000, 0);
                                    //        reverbZoneFilter.decayTime = EditorGUILayout.Slider(new GUIContent("Decay Time"), reverbZoneFilter.decayTime, 0.1f, 20f);
                                    //        reverbZoneFilter.decayHFRatio = EditorGUILayout.Slider(new GUIContent("Decay HF Ratio"), reverbZoneFilter.decayHFRatio, 0.1f, 2f);
                                    //        reverbZoneFilter.reflections = EditorGUILayout.IntSlider(new GUIContent("Reflections"), reverbZoneFilter.reflections, -10000, 1000);
                                    //        reverbZoneFilter.reflectionsDelay = EditorGUILayout.Slider(new GUIContent("Reflections Delay"), reverbZoneFilter.reflectionsDelay, 0, 0.3f);
                                    //        reverbZoneFilter.reverb = EditorGUILayout.IntSlider(new GUIContent("Reverb"), reverbZoneFilter.reverb, -10000, 2000);
                                    //        reverbZoneFilter.reverbDelay = EditorGUILayout.Slider(new GUIContent("Reverb Delay"), reverbZoneFilter.reverbDelay, 0, 0.1f);
                                    //        reverbZoneFilter.hfReference = EditorGUILayout.IntSlider(new GUIContent("HF Reference"), (int)reverbZoneFilter.hfReference, 1000, 20000);
                                    //        reverbZoneFilter.lfReference = EditorGUILayout.IntSlider(new GUIContent("LF Reference"), (int)reverbZoneFilter.lfReference, 20, 1000);
                                    //        reverbZoneFilter.diffusion = EditorGUILayout.Slider(new GUIContent("Diffusion"), reverbZoneFilter.diffusion, 0, 100f);
                                    //        reverbZoneFilter.density = EditorGUILayout.Slider(new GUIContent("Density"), reverbZoneFilter.density, 0, 100f);
                                    //        EditorGUI.EndDisabledGroup();
                                    //    }
                                    //    break;
                                }
                            case AEEffectType.EchoFilter:
                                if (effectSetting is AEEchoFilter echoFilter)
                                {
                                    echoFilter.delay = EditorGUILayout.IntSlider(new GUIContent("Delay"), echoFilter.delay, 10, 5000);
                                    echoFilter.decayRatio = EditorGUILayout.Slider(new GUIContent("DecayRatio"), echoFilter.decayRatio, 0, 1f);
                                    echoFilter.wetMix = EditorGUILayout.Slider(new GUIContent("Wet Mix"), echoFilter.wetMix, 0, 1f);
                                    echoFilter.dryMix = EditorGUILayout.Slider(new GUIContent("Dry Mix"), echoFilter.dryMix, 0, 1f);
                                }
                                break;
                            case AEEffectType.ChorusFilter:
                                if (effectSetting is AEChorusFilter chorusFilter)
                                {
                                    chorusFilter.dryMix = EditorGUILayout.Slider(new GUIContent("Dry Mix"), chorusFilter.dryMix, 0, 1f);
                                    chorusFilter.wetMix1 = EditorGUILayout.Slider(new GUIContent("Wet Mix1"), chorusFilter.wetMix1, 0, 1f);
                                    chorusFilter.wetMix2 = EditorGUILayout.Slider(new GUIContent("Wet Mix2"), chorusFilter.wetMix2, 0, 1f);
                                    chorusFilter.wetMix3 = EditorGUILayout.Slider(new GUIContent("Wet Mix3"), chorusFilter.wetMix3, 0, 1f);
                                    chorusFilter.delay = EditorGUILayout.Slider(new GUIContent("Delay"), chorusFilter.delay, 0.1f, 100f);
                                    chorusFilter.rate = EditorGUILayout.Slider(new GUIContent("Rate"), chorusFilter.rate, 0, 20f);
                                    chorusFilter.depth = EditorGUILayout.Slider(new GUIContent("Depth"), chorusFilter.depth, 0, 1);
                                }
                                break;
                            case AEEffectType.DistortionFilter:
                                if (effectSetting is AEDistortionFilter distortionFilter)
                                {
                                    distortionFilter.distortionLevel = EditorGUILayout.Slider(new GUIContent("Distortion Level"), distortionFilter.distortionLevel, 0, 1f);
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                //Debug.Log("数据刷新 In EffectsSetting");
            }
        }


        #region 3D页面的绘制

        private void Draw3DSetting(AEAudioComponent currentSelectData)
        {
            bool outputVolumeControlByCurve = false;
            bool spatialBlendControlByCurve = false;
            bool spreadControlByCurve = false;
            bool reverbZoneMixControlByCurve = false;
            foreach (var curveSetting in currentSelectData.attenuationCurveSettings)
            {
                switch (curveSetting.attenuationCurveType)
                {
                    case AttenuationCurveType.OutputVolume:
                        outputVolumeControlByCurve = true;
                        break;
                    case AttenuationCurveType.SpatialBlend:
                        spatialBlendControlByCurve = true;
                        break;
                    case AttenuationCurveType.Spread:
                        spreadControlByCurve = true;
                        break;
                    case AttenuationCurveType.ReverbZoneMix:
                        reverbZoneMixControlByCurve = true;
                        break;
                }
            }
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild == false);
            var newOverrideActorMixer = GUILayout.Toggle((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.Attenuation) == AEComponentDataOverrideType.Attenuation, "Override 3D Setting");
            if (newOverrideActorMixer)
            {
                currentSelectData.overrideFunctionType |= AEComponentDataOverrideType.Attenuation;
            }
            else
            {
                currentSelectData.overrideFunctionType &= ~AEComponentDataOverrideType.Attenuation;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild && (currentSelectData.overrideFunctionType & AEComponentDataOverrideType.Attenuation) != AEComponentDataOverrideType.Attenuation);

            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false)))
            {
                GUI.color = preGUIColor;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Spatial Blend", "设置音源3D分量的程度，0为2D，1为3D"));
                if (spatialBlendControlByCurve)
                { EditorGUILayout.LabelField("由曲线控制", EditorStyles.toolbarButton); }
                else
                { currentSelectData.spatialBlend = EditorGUILayout.Slider(currentSelectData.spatialBlend, 0, 1); }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Stereo Pan", "设置2D分量的偏移程度，-1为左声道，1为右声道"));
                currentSelectData.panStereo = EditorGUILayout.Slider(currentSelectData.panStereo, -1, 1);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Doppler Level", "多普勒效应的程度"));
                currentSelectData.dopplerLevel = EditorGUILayout.Slider(currentSelectData.dopplerLevel, 0, 5);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Spread", "听者相对于发声体的位置，360为实际位置的相反角度"));
                if (spreadControlByCurve)
                { EditorGUILayout.LabelField("由曲线控制", EditorStyles.toolbarButton); }
                else
                { currentSelectData.spread = EditorGUILayout.Slider(currentSelectData.spread, 0, 360); }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Reverb Zone Mix",
                    "设置输出到混响区的输出信号量。该值在（0-1）范围内是线性的，但可以在（1-1.1）范围内进行10 dB的放大，这对于获得近场和远处的声音效果很有用。"));
                if (reverbZoneMixControlByCurve)
                { EditorGUILayout.LabelField("由曲线控制", EditorStyles.toolbarButton); }
                else
                { currentSelectData.reverbZoneMix = EditorGUILayout.Slider(currentSelectData.reverbZoneMix, 0, 1.1f); }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Min Distance"));
                var rect = EditorGUILayout.GetControlRect(GUILayout.Width(150));
                if (outputVolumeControlByCurve)
                {
                    EditorGUI.LabelField(rect, "由曲线控制", EditorStyles.toolbarButton);
                }
                else
                {
                    var newMinDistance = EditorGUI.DelayedFloatField(rect, currentSelectData.MinDistance);
                    if (Math.Abs(newMinDistance - currentSelectData.MinDistance) > 0.01)
                    {
                        currentSelectData.MinDistance = newMinDistance;
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.PrefixLabel(new GUIContent("Max Distance"));
                rect = EditorGUILayout.GetControlRect(GUILayout.Width(150));
                var newMaxDistance = EditorGUI.DelayedFloatField(rect, currentSelectData.MaxDistance);
                if (Math.Abs(newMaxDistance - currentSelectData.MaxDistance) > 0.01)
                {
                    currentSelectData.MaxDistance = newMaxDistance;
                }
                EditorGUILayout.EndHorizontal();

            }
            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            //绘制图表
            var graphRect = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));
            var tempCurveList = new List<AnimationCurve>();
            foreach (var curveSetting in currentSelectData.attenuationCurveSettings)
            {
                tempCurveList.Add(curveSetting.curveData);
            }
            GraphCurveRendering.Draw(graphRect, tempCurveList, currentSelectData.MinDistance, currentSelectData.MaxDistance, 0, 1);

            GUI.color = backgroundBoxColor;
            using (new GUILayout.VerticalScope("box"))
            {
                scorllViewPosition =
                    EditorGUILayout.BeginScrollView(scorllViewPosition, GUILayout.Height(120));
                for (int i = 0; i < currentSelectData.attenuationCurveSettings.Count; i++)
                {
                    // GUI.color = new Color(0,0,1,0.2f);
                    using (new GUILayout.VerticalScope("box"))
                    {
                        //     GUI.color = preGUIColor;
                        EditorGUILayout.BeginHorizontal();
                        var curveSetting = currentSelectData.attenuationCurveSettings[i];
                        var colorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(5));
                        EditorGUI.DrawRect(colorRect, colorRankList[i]);
                        // curveSetting.curveData = EditorGUILayout.CurveField(curveSetting.attenuationCurveType.ToString(),curveSetting.curveData);
                        EditorGUILayout.CurveField(curveSetting.attenuationCurveType.ToString(), curveSetting.curveData);
                        for (int j = 0; j < curveSetting.curveData.length; j++)
                        {
                            if (j == 0)
                            {
                                if (curveSetting.curveData[j].time != 0)
                                {
                                    var newKeyframe = curveSetting.curveData.keys[j];
                                    newKeyframe.time = 0;
                                    curveSetting.curveData.MoveKey(0, newKeyframe);
                                }
                            }

                            if (j == curveSetting.curveData.length - 1)
                            {
                                if (curveSetting.curveData[j].time != 1)
                                {
                                    var newKeyframe = curveSetting.curveData.keys[j];
                                    newKeyframe.time = 1;
                                    curveSetting.curveData.MoveKey(curveSetting.curveData.length - 1, newKeyframe);
                                }
                            }
                            if (curveSetting.curveData.keys[j].value < 0 || curveSetting.curveData.keys[j].value > 1)
                            {
                                var newKeyframe = curveSetting.curveData.keys[j];
                                newKeyframe.value = Mathf.Clamp01(newKeyframe.value);
                                curveSetting.curveData.MoveKey(j, newKeyframe);
                            }
                        }
                        if (GUILayout.Button("删除", GUILayout.ExpandWidth(false)))
                        {
                            DataRegisterToUndo();
                            currentSelectData.attenuationCurveSettings.RemoveAt(i);
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                }
                var buttonRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false));
                if (EditorGUI.DropdownButton(buttonRect, new GUIContent(">>"), FocusType.Passive))
                {
                    GetAttenuationCurveTypeMenu(buttonRect, currentSelectData);
                }

                EditorGUILayout.EndScrollView();
            }
            GUI.color = preGUIColor;
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                // Debug.Log("数据刷新 In 3DSetting");
            }
        }

        private void GetAttenuationCurveTypeMenu(Rect rect, AEAudioComponent currentSelectData)
        {
            var menu = new GenericMenu();
            menu.allowDuplicateNames = false;
            var curveTypeStr = Enum.GetNames(typeof(AttenuationCurveType));
            for (int i = 0; i < curveTypeStr.Length; i++)
            {
                menu.AddItem(new GUIContent(curveTypeStr[i]), false, (cureType) =>
                {
                    foreach (var curveSetting in currentSelectData.attenuationCurveSettings)
                    {
                        if (curveSetting.attenuationCurveType.CompareTo((AttenuationCurveType)cureType) == 0)
                        {
                            return;
                        }
                    }
                    DataRegisterToUndo();
                    if ((AttenuationCurveType)cureType == AttenuationCurveType.OutputVolume) currentSelectData.MinDistance = 0;
                    if ((AttenuationCurveType)cureType == AttenuationCurveType.LowPass)
                    {
                        if (currentSelectData.effectSettings.Find((x) => x.type == AEEffectType.LowPassFilter) == null)
                        {
                            currentSelectData.effectSettings.Add(new AELowPassFilter());
                        }
                    }
                    currentSelectData.attenuationCurveSettings.Add(new AttenuationCurveSetting((AttenuationCurveType)cureType));
                    manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                }, i);
            }
            menu.DropDown(rect);
        }
        #endregion

        #region RTPC页面的绘制

        private void DrawRTPCSettingUnit(AEAudioComponent currentSelectData)
        {
            AnimationCurve tempCurve = null;
            float? gameParameterMinValue = null;
            float? gameParameterMaxValue = null;
            float? gameParameterCurrentValue = null;
            float? yAxisMin = null;
            float? yAxisMax = null;
            if (currentSelectData.gameParameterCurveSettings.Count > 0 && whichRTPCSelect < currentSelectData.gameParameterCurveSettings.Count)
            {
                var gameParameterID = currentSelectData.gameParameterCurveSettings[whichRTPCSelect].gameParameterId;
                var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(gameParameterID);
                if (gameParameter != null)
                {
                    gameParameterMinValue = gameParameter.MinValue;
                    gameParameterMaxValue = gameParameter.MaxValue;
                    gameParameterCurrentValue = gameParameter.Value;
                }
                //tempCurveList.Add(currentSelectData.gameParameterCurveSettings[whichRTPCSelect].curveData);
                tempCurve = currentSelectData.gameParameterCurveSettings[whichRTPCSelect].curveData;
            }

            if (whichRTPCSelect < currentSelectData.gameParameterCurveSettings.Count)
            {
                yAxisMin = currentSelectData.gameParameterCurveSettings[whichRTPCSelect].yAxisMin;
                yAxisMax = currentSelectData.gameParameterCurveSettings[whichRTPCSelect].yAxisMax;
            }
            EditorGUI.BeginChangeCheck();
            var graphRect = EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));
            GraphCurveRendering.Draw(graphRect, tempCurve, colorRankList[whichRTPCSelect], gameParameterMinValue,
                gameParameterMaxValue, yAxisMin, yAxisMax, gameParameterCurrentValue);

            int column1With = 100;
            int column2With = 200;
            using (new GUILayout.VerticalScope("box"))
            {
                GUI.color = backgroundBoxColor;
                EditorGUILayout.BeginHorizontal();
                //GUILayout.Space(5);
                GUILayout.Box(new GUIContent("Y Axis"), GUILayout.Width(column1With));
                GUILayout.Box(new GUIContent("X Axis"), GUILayout.Width(column2With));
                GUILayout.Box(new GUIContent("Curve"), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                GUI.color = preGUIColor;

                scorllViewPosition =
                    EditorGUILayout.BeginScrollView(scorllViewPosition, GUILayout.Height(100));

                var rect = Rect.zero;

                for (int i = 0; i < currentSelectData.gameParameterCurveSettings.Count; i++)
                {
                    if (whichRTPCSelect == i)
                    {
                        GUI.color = RectSelectedColor;
                    }
                    using (new GUILayout.HorizontalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        var curveSetting = currentSelectData.gameParameterCurveSettings[i];
                        rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(5));
                        EditorGUI.DrawRect(rect, colorRankList[i]);
                        EditorGUILayout.LabelField(new GUIContent(curveSetting.targetType.ToString()), GUILayout.Width(column1With - 5));
                        var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(curveSetting.gameParameterId);
                        if (gameParameter == null)
                        {
                            rect = EditorGUILayout.GetControlRect(true, GUILayout.Width(column2With - 20));
                            if (EditorGUI.DropdownButton(rect, new GUIContent(">>"), FocusType.Passive))
                            {
                                // GetRelatedGameParameterMenu(rect, curveSetting);
                                var menu = new GenericMenu();
                                menu.allowDuplicateNames = false;
                                for (int j = 0; j < ManagerData.gameSyncsData.Count; j++)
                                {
                                    if (ManagerData.gameSyncsData[j] is GameParameter gameParameterData)
                                        menu.AddItem(new GUIContent(gameParameterData.name), false, (id) =>
                                        {
                                            DataRegisterToUndo();
                                            curveSetting.gameParameterId = (int)id;
                                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                                        }, gameParameterData.id);
                                }
                                menu.DropDown(rect);
                            }
                            GUILayout.Space(20);
                        }
                        else
                        {
                            EditorGUILayout.LabelField(new GUIContent(gameParameter.name), GUILayout.Width(column2With - 40));
                            rect = EditorGUILayout.GetControlRect(GUILayout.Width(20));
                            if (EditorGUI.DropdownButton(rect, new GUIContent("  "), FocusType.Passive))
                            {
                                var menu = new GenericMenu();
                                menu.allowDuplicateNames = false;
                                for (int j = 0; j < ManagerData.gameSyncsData.Count; j++)
                                {
                                    if (ManagerData.gameSyncsData[j] is GameParameter gameParameterData)
                                        menu.AddItem(new GUIContent(gameParameterData.name), false, (id) =>
                                        {
                                            DataRegisterToUndo();
                                            curveSetting.gameParameterId = (int)id;
                                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                                        }, gameParameterData.id);
                                }
                                menu.DropDown(rect);
                            }
                            GUILayout.Space(20);
                        }

                        EditorGUILayout.CurveField(curveSetting.curveData);
                        //检查所有点，钳制所有点的x轴和y轴坐标区间[0,1]内
                        for (int j = 0; j < curveSetting.curveData.length; j++)
                        {
                            if (j == 0)
                            {
                                if (curveSetting.curveData[j].time != 0)
                                {
                                    var newKeyframe = curveSetting.curveData.keys[j];
                                    newKeyframe.time = 0;
                                    curveSetting.curveData.MoveKey(0, newKeyframe);
                                }
                            }

                            if (j == curveSetting.curveData.length - 1)
                            {
                                if (curveSetting.curveData[j].time != 1)
                                {
                                    var newKeyframe = curveSetting.curveData.keys[j];
                                    newKeyframe.time = 1;
                                    curveSetting.curveData.MoveKey(curveSetting.curveData.length - 1, newKeyframe);
                                }
                            }
                            if (curveSetting.curveData.keys[j].value < 0 || curveSetting.curveData.keys[j].value > 1)
                            {
                                var newKeyframe = curveSetting.curveData.keys[j];
                                newKeyframe.value = Mathf.Clamp01(newKeyframe.value);
                                curveSetting.curveData.MoveKey(j, newKeyframe);
                            }
                        }
                        if (GUILayout.Button("删除", GUILayout.ExpandWidth(false)))
                        {
                            DataRegisterToUndo();
                            currentSelectData.gameParameterCurveSettings.RemoveAt(i);
                        }
                    }
                    rect = GUILayoutUtility.GetLastRect();
                    if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
                    {
                        whichRTPCSelect = i;
                        //Event.current.Use();
                        Repaint();
                    }
                    //键盘的delete快捷键
                    if (whichRTPCSelect == i && rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
                    {
                        DataRegisterToUndo();
                        currentSelectData.gameParameterCurveSettings.RemoveAt(i);
                        whichRTPCSelect = 0;
                        Event.current.Use();
                        GUI.changed = true;
                        Repaint();
                    }
                }
                var buttonRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false));
                if (EditorGUI.DropdownButton(buttonRect, new GUIContent(">>"), FocusType.Passive))
                {
                    GetGameParameterTargetTypeMenu(buttonRect, currentSelectData);
                }

                EditorGUILayout.EndScrollView();
            }
            GUI.color = preGUIColor;

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                // Debug.Log("数据刷新 In RTPC");
            }
        }

        /// <summary>
        /// 获取RTPC可调节选项列表
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="currentSelectData"></param>
        private void GetGameParameterTargetTypeMenu(Rect rect, AEAudioComponent currentSelectData)
        {
            var menu = new GenericMenu();
            menu.allowDuplicateNames = true;
            var curveTypeStr = Enum.GetNames(typeof(GameParameterTargetType));
            var gameParameterList = new List<GameParameter>();
            foreach (var aeGameSyncse in ManagerData.gameSyncsData)
            {
                if (aeGameSyncse is GameParameter gameParameter) gameParameterList.Add(gameParameter);
            }
            for (int i = 0; i < curveTypeStr.Length; i++)
            {
                for (int j = 0; j < gameParameterList.Count; j++)
                {
                    int[] data = { i, gameParameterList[j].id };
                    menu.AddItem(new GUIContent(curveTypeStr[i] + "/" + gameParameterList[j].name), false, (getData) =>
                        {
                            //目前根据需求只允许一个属性由一个GameParameter控制，最终实现应该可以一个属性由多个GameParameter控制
                            var targetType = (GameParameterTargetType)((int[])getData)[0];
                            var targetGameParameterId = ((int[])getData)[1];
                            foreach (var curveSetting in currentSelectData.gameParameterCurveSettings)
                            {
                                if (curveSetting.targetType.CompareTo(targetType) == 0)
                                {
                                    return;
                                }
                            }
                            DataRegisterToUndo();
                            currentSelectData.gameParameterCurveSettings.Add(new GameParameterCurveSetting(targetType, targetGameParameterId));
                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                        }, data);
                }
            }
            menu.DropDown(rect);
        }
        #endregion

        private void DrawStateSettingUnit(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();
            var buttonRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(false));
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent("Set StateGroup >>"), FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (var gameSyncs in ManagerData.gameSyncsData)
                {
                    if (gameSyncs is StateGruop stateGruop)
                    {
                        var check = false;
                        foreach (var stateSetting in currentSelectData.stateSettings)
                        {
                            if (stateGruop.id == stateSetting.stateGroupId) check = true;
                            break;
                        }
                        if (check == true) continue;
                        menu.AddItem(new GUIContent(stateGruop.name), false, (selectedStateGruop) =>
                        {
                            DataRegisterToUndo();
                            currentSelectData.stateSettings.Add(new StateSetting(selectedStateGruop as StateGruop));
                            manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                        }, stateGruop);
                    }
                }
                menu.DropDown(buttonRect);
            }
            //EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            if (foldoutHeaderFlags.Count == 0 || foldoutHeaderFlags.Count != currentSelectData.stateSettings.Count)
            {
                foldoutHeaderFlags.Clear();
                for (int i = 0; i < currentSelectData.stateSettings.Count; i++)
                {
                    foldoutHeaderFlags.Add(true);
                }
            }
            GUI.color = backgroundBoxColor;
            using (new GUILayout.VerticalScope("box"))
            {
                GUI.color = preGUIColor;
                scorllViewPosition = EditorGUILayout.BeginScrollView(scorllViewPosition);
                for (int i = 0; i < currentSelectData.stateSettings.Count; i++)
                {
                    //检测对应group是否被删除
                    var stateGruop =
                        AudioEditorManager.GetAEComponentDataByID<StateGruop>(currentSelectData.stateSettings[i]
                            .stateGroupId) as StateGruop;
                    if (stateGruop == null)
                    {
                        currentSelectData.stateSettings.RemoveAt(i);
                        Repaint();
                    }
                    if (currentSelectData.stateSettings[i].volumeList.Count != stateGruop.StateListCount || currentSelectData.stateSettings[i].pitchList.Count != stateGruop.StateListCount)
                    {
                        //在新建和删除state时会执行同步audioComponent数据的操作，不应该会进入此步
                        Debug.LogError("[AudioEditor]: 发生错误");
                        currentSelectData.stateSettings[i].Reset(stateGruop);
                    }

                    foldoutHeaderFlags[i] = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutHeaderFlags[i],
                        new GUIContent(stateGruop.name), menuAction: rect =>
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Delete"), false, (x) =>
                            {
                                DataRegisterToUndo();
                                currentSelectData.stateSettings.RemoveAt((int)x);
                                foldoutHeaderFlags.Clear();
                                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                            }, i);
                            menu.DropDown(rect);
                        });
                    if (foldoutHeaderFlags[i])
                    {
                        EditorGUI.indentLevel++;
                        for (int j = 0; j < stateGruop.StateListCount; j++)
                        {
                            var state = stateGruop.GetStateAt(j);
                            if (state.IsNone)
                            {
                                //None状态不进行设置
                                continue;
                            }
                            EditorGUILayout.BeginHorizontal();

                            EditorGUILayout.LabelField(state.name);
                            currentSelectData.stateSettings[i].volumeList[j] = EditorGUILayout.Slider(new GUIContent("Volume"),
                                currentSelectData.stateSettings[i].volumeList[j], 0, 1);
                            currentSelectData.stateSettings[i].pitchList[j] = EditorGUILayout.Slider(new GUIContent("Pitch"),
                                currentSelectData.stateSettings[i].pitchList[j], 0, 3);

                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }

                EditorGUILayout.EndScrollView();
            }

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.General);
                //Debug.Log("数据刷新 In States");
            }
        }

        private void DrawOtherSettingUnit(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild == false);
            var newOverrideActorMixer = GUILayout.Toggle((currentSelectData.overrideFunctionType & AEComponentDataOverrideType.OtherSetting) == AEComponentDataOverrideType.OtherSetting, "Override OtherSettings");
            if (newOverrideActorMixer)
            {
                currentSelectData.overrideFunctionType |= AEComponentDataOverrideType.OtherSetting;
            }
            else
            {
                currentSelectData.overrideFunctionType &= ~AEComponentDataOverrideType.OtherSetting;
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild && (currentSelectData.overrideFunctionType & AEComponentDataOverrideType.OtherSetting) != AEComponentDataOverrideType.OtherSetting);

            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                EditorGUILayout.LabelField("Other Setting", EditorStyles.boldLabel);

                var newTempo = EditorGUILayout.FloatField(new GUIContent("Tempo"), currentSelectData.tempo, GUILayout.ExpandWidth(false));
                if (Math.Abs(newTempo - currentSelectData.tempo) > 0.01)
                {
                    if (newTempo < 1) newTempo = 1;
                    currentSelectData.tempo = newTempo;
                }
                var newBeatsPerMeasure = EditorGUILayout.IntField(new GUIContent("Beats Per Measure"), currentSelectData.beatsPerMeasure, GUILayout.ExpandWidth(false));
                if (newBeatsPerMeasure != currentSelectData.beatsPerMeasure)
                {
                    if (newBeatsPerMeasure < 1) newBeatsPerMeasure = 1;
                    currentSelectData.beatsPerMeasure = newBeatsPerMeasure;
                }
                currentSelectData.offset = EditorGUILayout.FloatField(new GUIContent("Offset"), currentSelectData.offset, GUILayout.ExpandWidth(false));

                EditorGUILayout.Space();

                currentSelectData.unloadClipWhenPlayEnd = EditorGUILayout.ToggleLeft(
                    new GUIContent("当实例播放结束时直接卸载音频文件", "当音频文件的LoadType为Streaming时此选项无效"),
                    currentSelectData.unloadClipWhenPlayEnd);
                currentSelectData.stopWhenGameObjectDestroy = EditorGUILayout.ToggleLeft(
                    new GUIContent("当GameObject被销毁时是否停止播放组件"), currentSelectData.stopWhenGameObjectDestroy);
                currentSelectData.priority =
                    EditorGUILayout.IntSlider(new GUIContent("Priority", "优先级,数值越小优先级越高"), currentSelectData.priority, 0, 256);
                currentSelectData.limitPlayNumber = EditorGUILayout.IntSlider(
                    new GUIContent("Limit Play Number", "限制此组件的最大同时播放数量"), currentSelectData.limitPlayNumber, 1, 10);
            }

            #region 此部分内容已迁移至ProjectSetting中
            //GUI.color = backgroundBoxColor;
            //using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
            //{
            //    GUI.color = preGUIColor;
            //    EditorGUILayout.LabelField(new GUIContent("Global Setting", "此部分选项为全局设置，与单一组件无关"), EditorStyles.boldLabel);

            //    EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            //    //在勾选上后直接链接上所有的AudioClip，否则所有组件都还是空的状态，直接进行构建打包的话会无法加载AudioClip
            //    var newStaticallyLinkedAudioClipsOption =
            //        EditorGUILayout.ToggleLeft(new GUIContent("静态关联AudioClips", "如取消此选项，会调用加载AudioClip的委托方法"), manager.staticallyLinkedAudioClips);
            //    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            //    if (newStaticallyLinkedAudioClipsOption != manager.staticallyLinkedAudioClips)
            //    {
            //        manager.staticallyLinkedAudioClips = newStaticallyLinkedAudioClipsOption;
            //        if (newStaticallyLinkedAudioClipsOption == true)
            //        {
            //            foreach (var audioComponent in ManagerData.audioComponentData)
            //            {
            //                if (audioComponent is SoundSFX soundSFX)
            //                {
            //                    if (soundSFX.clip == null)
            //                    {
            //                        LoadSoundSFXAudioClip(soundSFX);
            //                    }
            //                }
            //            }
            //        }
            //        else
            //        {
            //            foreach (var audioComponent in ManagerData.audioComponentData)
            //            {
            //                if (audioComponent is SoundSFX soundSFX)
            //                {
            //                    soundSFX.clip = null;
            //                }
            //            }
            //        }
            //    }

            //    manager.autoLoadDataAsset =
            //        EditorGUILayout.ToggleLeft(new GUIContent("自动加载配置文件"), manager.autoLoadDataAsset);


            //    AudioConfiguration config = AudioSettings.GetConfiguration();
            //    var newRealVoiceNumer =
            //        EditorGUILayout.IntSlider(new GUIContent("RealVoicesNumer", "游戏中当前可同时听到的最大声音数"),
            //            config.numRealVoices, 1, 512);
            //    if (newRealVoiceNumer != config.numRealVoices)
            //    {
            //        config.numRealVoices = newRealVoiceNumer;
            //        AudioSettings.Reset(config);
            //    }

            //    EditorGUILayout.Space();

            //    EditorGUILayout.BeginHorizontal();
            //    EditorGUILayout.PrefixLabel("ResourceDataPath");
            //    using (new EditorGUI.DisabledGroupScope(true))
            //    {
            //        EditorGUILayout.LabelField(manager.programPath);
            //    }

            //    if (GUILayout.Button("更改"))
            //    {
            //        Debug.Log(FolderBrowserHelper.GetPathFromWindowsExplorer("请选择文件路径"));
            //    }
            //    EditorGUILayout.EndHorizontal();
            //    EditorGUI.EndDisabledGroup();
            //}

            //GUI.color = backgroundBoxColor;
            //using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
            //{
            //    GUI.color = preGUIColor;
            //    EditorGUILayout.LabelField(new GUIContent("UI Setting", "此部分选项为编辑器UI的设定，与组件无关"), EditorStyles.boldLabel);
            //    AudioConfiguration config = AudioSettings.GetConfiguration();
            //    editorViewData.freshPageWhenTabInexChange =
            //        EditorGUILayout.ToggleLeft(new GUIContent("当选择变更时是否刷新页面"), editorViewData.freshPageWhenTabInexChange);
            //    GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            //}

            #endregion
            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncDataToPlayable(currentSelectData.id, AudioComponentDataChangeType.OtherSetting);
                //Debug.Log("数据刷新 In OtherSetting");
            }
        }

        #endregion

        #endregion

        #region 绘制右下角的预览界面

        private void DrawPreviewWindow()
        {
            if (manager.PlayableInstanceList.Find(x => x.playableOutput.GetUserData() == previewObject) == null)
            {
                previewState = PreviewModeState.Stoped;
            }

            if (editorViewData.ProjectExplorerTabIndex != (int)AudioEditorDataType.Audio && previewState == PreviewModeState.Playing)
            {
                manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                previewState = PreviewModeState.Stoped;
            }
            EditorGUILayout.BeginHorizontal();

            var color = GUI.color;
            GUI.color = backgroundBoxColor;
            using (new GUILayout.VerticalScope("box", GUILayout.Width(250), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;

                EditorGUILayout.BeginVertical();

                // GUI.color = Color.Lerp(Color.white, Color.black, 0.2f);
                EditorGUILayout.BeginHorizontal();
                GUILayoutOption[] buttonOption = { GUILayout.Width(64), GUILayout.Height(64) };
                if (GUILayout.Button(IconUtility.GetIconContent(PreviewIconType.StopOff, "Stop"), GUI.skin.button, buttonOption))
                {
                    manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                    previewState = PreviewModeState.Stoped;
                }

                var pauseIcontype = previewState == PreviewModeState.Paused ? PreviewIconType.PauseOn : PreviewIconType.PauseOff;
                if (GUILayout.Button(IconUtility.GetIconContent(pauseIcontype, "Pause"), GUI.skin.button, buttonOption))
                {
                    manager.ApplyAudioComponentPreview(previewObject, AEEventType.Pause, editorViewData.myTreeViewStates[0].lastClickedID);
                    previewState = PreviewModeState.Paused;
                }

                var playIconType = previewState == PreviewModeState.Playing ? PreviewIconType.PlayOn : PreviewIconType.PlayOff;
                var playTipLabel = previewState == PreviewModeState.Paused ? "Resume" : "Play";
                if (GUILayout.Button(IconUtility.GetIconContent(playIconType, playTipLabel), GUI.skin.button, buttonOption))
                {
                    if (previewState == PreviewModeState.Paused)
                    {
                        manager.ApplyAudioComponentPreview(previewObject, AEEventType.Resume, editorViewData.myTreeViewStates[0].lastClickedID);
                    }
                    else
                    {
                        if (editorViewData.ProjectExplorerTabIndex == (int)AudioEditorDataType.Audio &&
                            AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(editorViewData.myTreeViewStates[0].lastClickedID) != null)
                        {
                            manager.ApplyAudioComponentPreview(previewObject, AEEventType.Play, editorViewData.myTreeViewStates[0].lastClickedID);
                        }
                    }
                    previewState = PreviewModeState.Playing;
                }
                GUI.color = color;

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
                {
                    switch (previewState)
                    {
                        case PreviewModeState.Playing:
                            manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                            previewState = PreviewModeState.Stoped;
                            break;
                        case PreviewModeState.Paused:
                            manager.ApplyAudioComponentPreview(previewObject, AEEventType.Resume, editorViewData.myTreeViewStates[0].lastClickedID);
                            previewState = PreviewModeState.Playing;
                            break;
                        case PreviewModeState.Stoped:
                            if (editorViewData.ProjectExplorerTabIndex == (int)AudioEditorDataType.Audio &&
                                AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(editorViewData.myTreeViewStates[0].lastClickedID) != null)
                            {
                                manager.ApplyAudioComponentPreview(previewObject, AEEventType.Play, editorViewData.myTreeViewStates[0].lastClickedID);
                            }
                            previewState = PreviewModeState.Playing;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    Event.current.Use();
                }

                previewWindowGridSelected = GUILayout.SelectionGrid(previewWindowGridSelected, previewWindowGridButtonName, 2, GUILayout.Width(150), GUILayout.ExpandWidth(false));
                EditorGUILayout.EndHorizontal();
                AudioListener.GetOutputData(outputVolumeData, 0);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true)), outputVolumeData.Max(), "");
                AudioListener.GetOutputData(outputVolumeData, 1);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true)), outputVolumeData.Max(), "");
                EditorGUILayout.EndVertical();
            }
            //根据当前选择组件绘制选项框中的涉及的内容
            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                if (editorViewData.ProjectExplorerTabIndex == (int)AudioEditorDataType.Audio)
                {
                    var currentSelectID = editorViewData.myTreeViewStates[editorViewData.ProjectExplorerTabIndex].lastClickedID;
                    if (AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(currentSelectID) is AEAudioComponent audiocomponentData)
                    {
                        switch (previewWindowGridSelected)
                        {
                            case 0:
                                //states
                                var tempStateGroupSet = new HashSet<StateGruop>();
                                //获取子类中的StateSetting
                                if (audiocomponentData is IAEContainer container)
                                {
                                    var containerChildrenSet = new HashSet<AEAudioComponent>();
                                    AudioEditorManager.GetContainerAllChildren(container, ref containerChildrenSet);
                                    foreach (var childAudioComponent in containerChildrenSet)
                                    {
                                        foreach (var stateSetting in childAudioComponent.stateSettings)
                                        {
                                            var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId);
                                            tempStateGroupSet.Add(stateGroup);
                                        }
                                    }
                                }
                                //获取本组件的StateSetting
                                foreach (var stateSetting in audiocomponentData.stateSettings)
                                {
                                    var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId);
                                    tempStateGroupSet.Add(stateGroup);
                                }
                                //绘制组件
                                foreach (var stateGroup in tempStateGroupSet)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.PrefixLabel(stateGroup.name);
                                    var buttonRect = EditorGUILayout.GetControlRect();
                                    if (EditorGUI.DropdownButton(buttonRect, new GUIContent(stateGroup.GetState(stateGroup.currentStateID).name),
                                        FocusType.Passive))
                                    {
                                        var menu = new GenericMenu();
                                        for (int i = 0; i < stateGroup.StateListCount; i++)
                                        {
                                            var state = stateGroup.GetStateAt(i);
                                            menu.AddItem(new GUIContent(state.name), false, (selectedState) =>
                                             {
                                                 manager.ApplyAudioComponentPreview(previewObject, AEEventType.SetState, (selectedState as State).id);
                                             }, state);
                                        }
                                        menu.DropDown(buttonRect);
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                                break;
                            case 1:
                                //rtpcs
                                var gameParameterSet = new HashSet<GameParameter>();
                                if (audiocomponentData is IAEContainer)
                                {
                                    var containerChildrenSet = new HashSet<AEAudioComponent>();
                                    AudioEditorManager.GetContainerAllChildren(audiocomponentData as IAEContainer, ref containerChildrenSet);
                                    foreach (var childAudioComponent in containerChildrenSet)
                                    {
                                        foreach (var gameParameterCurveSetting in childAudioComponent.gameParameterCurveSettings)
                                        {
                                            gameParameterSet.Add(
                                                AudioEditorManager.GetAEComponentDataByID<GameParameter>(
                                                    gameParameterCurveSetting.gameParameterId));
                                        }

                                        if (childAudioComponent is BlendContainer childBlendContainer)
                                        {
                                            foreach (var blendContainerTrack in childBlendContainer.blendContainerTrackList.Where(blendContainerTrack => blendContainerTrack.gameParameterId != -1))
                                            {
                                                gameParameterSet.Add(AudioEditorManager.GetAEComponentDataByID<GameParameter>(
                                                    blendContainerTrack.gameParameterId));
                                            }
                                        }
                                    }

                                    if (audiocomponentData is BlendContainer blendContainer)
                                    {
                                        foreach (var blendContainerTrack in blendContainer.blendContainerTrackList.Where(blendContainerTrack => blendContainerTrack.gameParameterId != -1))
                                        {
                                            gameParameterSet.Add(AudioEditorManager.GetAEComponentDataByID<GameParameter>(
                                                blendContainerTrack.gameParameterId));
                                        }
                                    }
                                }
                                foreach (var gameParameterCurveSetting in audiocomponentData.gameParameterCurveSettings)
                                {

                                    var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(gameParameterCurveSetting.gameParameterId);
                                    if (gameParameter != null)
                                    {
                                        gameParameterSet.Add(gameParameter);
                                    }
                                }

                                foreach (var gameParameter in gameParameterSet)
                                {
                                    if (gameParameter != null)
                                    {
                                        var newValue = EditorGUILayout.Slider(new GUIContent(gameParameter.name),
                                            gameParameter.Value, gameParameter.MinValue, gameParameter.MaxValue);
                                        if (Math.Abs(newValue - gameParameter.Value) > 0.01)
                                        {
                                            Repaint();
                                            AudioEditorManager.SetGameParameter(gameParameter.id, newValue);
                                        }
                                    }
                                }

                                break;
                            case 2:
                                //swithes
                                var switchGroupSet = new HashSet<SwitchGroup>();
                                if (audiocomponentData is IAEContainer)
                                {
                                    var containerChildrenSet = new HashSet<AEAudioComponent>();
                                    AudioEditorManager.GetContainerAllChildren(audiocomponentData as IAEContainer, ref containerChildrenSet);
                                    foreach (var childAudioComponent in containerChildrenSet)
                                    {
                                        if (childAudioComponent is SwitchContainer switchContainer)
                                        {
                                            var switchGroup = AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(switchContainer.switchGroupID);
                                            if (switchGroup != null)
                                            {
                                                switchGroupSet.Add(switchGroup);
                                            }
                                        }
                                    }
                                }

                                if (audiocomponentData is SwitchContainer)
                                {
                                    var switchGroup = AudioEditorManager.GetAEComponentDataByID<SwitchGroup>((audiocomponentData as SwitchContainer).switchGroupID);
                                    if (switchGroup != null)
                                    {
                                        switchGroupSet.Add(switchGroup);
                                    }
                                }

                                foreach (var switchGroup in switchGroupSet)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    EditorGUILayout.PrefixLabel(switchGroup.name);
                                    var buttonRect = EditorGUILayout.GetControlRect();
                                    var buttonName = switchGroup.GetSwitch(switchGroup.currentSwitchID) == null
                                        ? ""
                                        : switchGroup.GetSwitch(switchGroup.currentSwitchID).name;
                                    if (EditorGUI.DropdownButton(buttonRect, new GUIContent(buttonName),
                                        FocusType.Passive))
                                    {
                                        var menu = new GenericMenu();
                                        for (int i = 0; i < switchGroup.SwitchListCount; i++)
                                        {
                                            var aeSwitch = switchGroup.GetSwitchAt(i);
                                            menu.AddItem(new GUIContent(aeSwitch.name), false, (selectedSwitch) =>
                                            {
                                                manager.ApplyAudioComponentPreview(previewObject, AEEventType.SetSwitch, (selectedSwitch as Switch).id);
                                            }, aeSwitch);
                                        }
                                        menu.DropDown(buttonRect);
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                                break;
                            case 3:
                                //triggers
                                break;
                        }
                    }

                }
            }

            EditorGUILayout.EndHorizontal();


        }
        #endregion

        /// <summary>
        /// 将数据注册入撤销操作中
        /// </summary>
        private void DataRegisterToUndo()
        {
            Undo.RegisterCompleteObjectUndo(editorViewData, "AudioEditorWindow Change");
            Undo.RegisterCompleteObjectUndo(ManagerData, "AudioEditorWindow Change");
            Undo.IncrementCurrentGroup();
            //Debug.Log("DataRegisterToUndo.");
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        private void SaveData()
        {
            var fileDataList = new List<AudioEditorData>();
            for (int i = 0; i < MyTreeLists.TreeListCount; i++)
            {
                var dataPath = $"{AudioEditorManager.RuntimeDataFolder}/AudioEditorData_{projectExplorerTabNames[i]}/";
                SaveDataToFile(myTreeModels[i].root, dataPath, i, null, ref fileDataList);
            }

            //寻找多余的旧文件
            var allFileDataGUIDs = AssetDatabase.FindAssets("t:AudioEditorData", new[] { AudioEditorManager.RuntimeDataFolder });
            for (int i = 0; i < allFileDataGUIDs.Length; i++)
            {
                var filePath = AssetDatabase.GUIDToAssetPath(allFileDataGUIDs[i]);
                if (fileDataList.Exists(x =>
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(x, out var guid, out long localId);
                    return guid == allFileDataGUIDs[i];
                }) == false)
                {
                    AssetDatabase.MoveAssetToTrash(filePath);
                    AudioEditorDebugLog.Log("移除文件:" + filePath);
                }
            }

            EditorUtility.SetDirty(editorViewData);
            EditorUtility.SetDirty(ManagerData);
            foreach (var audioEditorData in fileDataList)
            {
                EditorUtility.SetDirty(audioEditorData);
            }
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            if (EditorApplication.isPlaying == false)
            {
                //TODO 在runtime时候会报错
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
        }

        /// <summary>
        /// 递归方法将数据存至文件中
        /// </summary>
        /// <param name="treeElement">要遍历的节点</param>
        /// <param name="dataPath">当前储存数据的路径，不包含具体文件名</param>
        /// <param name="whichTreeList">当前对哪一颗树进行操作，参考AudioEditorDataType</param>
        /// <param name="parentData">节点所对应的workunit文件</param>
        /// <param name="fileDataList">返回所有文件列表</param>
        private void SaveDataToFile(MyTreeElement treeElement, string dataPath, int whichTreeList, AudioEditorData parentData, ref List<AudioEditorData> fileDataList)
        {
            var audioEditorData = parentData;
            if (treeElement.type == AEComponentType.WorkUnit)
            {
                audioEditorData = AssetDatabase.LoadAssetAtPath<AudioEditorData>(dataPath + treeElement.name + ".asset");

                //原本存在文件
                if (audioEditorData != null)
                {
                    //清空原来的数据，以免有冗余的已删除数据
                    audioEditorData.myTreeLists[whichTreeList].Clear();
                    switch ((AudioEditorDataType)whichTreeList)
                    {
                        case AudioEditorDataType.Audio:
                            audioEditorData.audioComponentData.Clear();
                            break;
                        case AudioEditorDataType.Events:
                            audioEditorData.eventData.Clear();
                            break;
                        case AudioEditorDataType.GameSyncs:
                            audioEditorData.gameSyncsData.Clear();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(whichTreeList), whichTreeList, null);
                    }
                }
                else
                {
                    //原本不存在文件
                    audioEditorData = CreateInstance<AudioEditorData>();
                    if (System.IO.Directory.Exists(dataPath) == false)
                    {
                        System.IO.Directory.CreateDirectory(dataPath);
                    }
                    AssetDatabase.CreateAsset(audioEditorData, dataPath + treeElement.name + ".asset");
                    if (AudioEditorManager.debugMode)
                    {
                        AudioEditorDebugLog.Log("CreateAsset:" + treeElement.name);
                    }
                }

                fileDataList.Add(audioEditorData);
                //文件以自身节点开始
                audioEditorData.myTreeLists[whichTreeList].Add(treeElement);

            }

            if (treeElement.type != AEComponentType.WorkUnit)
            {
                switch ((AudioEditorDataType)whichTreeList)
                {
                    case AudioEditorDataType.Audio:
                        audioEditorData.audioComponentData.Add(AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(treeElement.id));
                        break;
                    case AudioEditorDataType.Events:
                        audioEditorData.eventData.Add(AudioEditorManager.GetAEComponentDataByID<AEEvent>(treeElement.id));
                        break;
                    case AudioEditorDataType.GameSyncs:
                        audioEditorData.gameSyncsData.Add(AudioEditorManager.GetAEComponentDataByID<AEGameSyncs>(treeElement.id));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(whichTreeList), whichTreeList, null);
                }
            }

            if (treeElement.hasChildren)
            {
                foreach (var treeElementChild in treeElement.children)
                {
                    if (treeElementChild is MyTreeElement myTreeElementChild)
                    {
                        audioEditorData.myTreeLists[whichTreeList].Add(myTreeElementChild);

                        SaveDataToFile(myTreeElementChild, dataPath, whichTreeList, audioEditorData, ref fileDataList);
                    }

                }
            }
        }

        /// <summary>
        /// 为Manager.audioEditorData生成一个临时文件，以避免进入playMode时已进行修改的操作被重置
        /// </summary>
        private void CreateTempFile()
        {
            if (ManagerData != null)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ManagerData, out string guid, out long localId))
                {
                    return;
                }
                var path = AudioEditorManager.EditorTempFilePath;
                if (System.IO.File.Exists(path))
                {
                    Debug.Log("delete");
                    AssetDatabase.DeleteAsset(path);
                }
                AssetDatabase.CreateAsset(ManagerData, path);
            }
            else
            {
                AudioEditorDebugLog.LogError("ManagerData未进行初始化");
            }
        }

        /// <summary>
        /// 清除临时文件
        /// </summary>
        private void ClearTempFile()
        {
            if (ManagerData != null)
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ManagerData, out string guid, out long localId))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(path);
                }
            }
            //删除文件后Data会为null，如果是自动加载数据时应该再重载
            if (ManagerData == null && manager.autoLoadDataAsset)
            {
                AudioEditorManager.LoadDataAsset(true);
            }
        }


        /// <summary>
        /// 对所有音频文件的位置否进行纠错检查
        /// </summary>
        private void CheckSoundSFXClipPath()
        {
            if (ManagerData != null)
            {
                foreach (var audioComponent in ManagerData.audioComponentData)
                {
                    if (audioComponent is SoundSFX soundSFX)
                    {
                        if (soundSFX.clipGUID != "" || soundSFX.clipAssetPath != "")
                            if (System.IO.File.Exists(soundSFX.clipAssetPath) == false)
                            {
                                var newPath = AssetDatabase.GUIDToAssetPath(soundSFX.clipGUID);
                                if (System.IO.File.Exists(newPath))
                                {
                                    AudioEditorDebugLog.LogWarning($"发现组件 {soundSFX.name} 所链接的音频文件路径修改，已刷新数据，移动前路径：{soundSFX.clipAssetPath}，移动后路径：{newPath}");
                                    soundSFX.clipAssetPath = newPath;
                                }
                                else
                                {
                                    AudioEditorDebugLog.LogError($"发现组件 {soundSFX.name} 所链接的音频文件已丢失，请检查，原文件路径为：{soundSFX.clipAssetPath}");
                                }
                            }
                    }
                }
            }
        }

        /// <summary>
        /// 加载SoundSFX的音频文件
        /// </summary>
        /// <param name="soundSFX"></param>
        internal static void LoadSoundSFXAudioClip(SoundSFX soundSFX)
        {
            if (soundSFX.clipAssetPath != "" || soundSFX.clipGUID != "")
            {
                if (soundSFX.clip == null)
                {
                    //通过路径获取文件
                    soundSFX.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(soundSFX.clipAssetPath);
                }

                if (soundSFX.clip == null)
                {
                    //路径已被修改，通过GUID获取文件
                    var path = AssetDatabase.GUIDToAssetPath(soundSFX.clipGUID);
                    soundSFX.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (soundSFX.clip != null)
                        soundSFX.clipAssetPath = path;
                }

                if (soundSFX.clip == null && soundSFX.clipAssetPath != "")
                {
                    //路径和GUID都无法获取文件，文件可能已被删除，发出警告
                    AudioEditorDebugLog.LogError($"组件名:{soundSFX.name}，id:{soundSFX.id}, 无法加载AudioClip，请检查文件列表，路径为{soundSFX.clipAssetPath}");
                    soundSFX.clipAssetPath = "";
                }

                if (soundSFX.clip != null)
                {
                    //加载成功刷新路径和GUID
                    FreshGuidAndPathWhenClipLoad(soundSFX);
                }
            }
        }

        /// <summary>
        /// 当SoundSFX的Clip修改时对GUID和Path进行刷新
        /// </summary>
        /// <param name="soundSFX"></param>
        private static void FreshGuidAndPathWhenClipLoad(SoundSFX soundSFX)
        {
            if (soundSFX.clip != null)
            {
                var path = AssetDatabase.GetAssetPath(soundSFX.clip);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (path != soundSFX.clipAssetPath || guid != soundSFX.clipGUID)
                {
                    soundSFX.clipAssetPath = path;
                    soundSFX.clipGUID = guid;
                }
            }
        }

        /// <summary>
        /// 将当前页面的数据进行深度复制
        /// </summary>
        /// <param name="resourceData"></param>
        /// <param name="targetIDs"></param>
        private void DeepCopypropertyEditorTabPageData(AEAudioComponent resourceData, HashSet<int> targetIDs)
        {
            //TODO 完善深度复制的功能
            foreach (var targetID in targetIDs)
            {
                var targetData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(targetID);
                if (targetData == null || targetID == resourceData.id) continue;
                switch (PropertyEditorTabIndex)
                {
                    case PropertyEditorTab.GeneralSettings:
                        targetData.volume = resourceData.volume;
                        targetData.pitch = resourceData.pitch;
                        targetData.delayTime = resourceData.delayTime;
                        targetData.fadeIn = resourceData.fadeIn;
                        targetData.fadeInTime = resourceData.fadeInTime;
                        targetData.fadeInType = resourceData.fadeInType;
                        targetData.fadeOut = resourceData.fadeOut;
                        targetData.fadeOutTime = resourceData.fadeOutTime;
                        targetData.fadeInType = resourceData.fadeOutType;
                        targetData.loop = resourceData.loop;
                        targetData.loopInfinite = resourceData.loopInfinite;
                        targetData.loopTimes = resourceData.loopTimes;
                        targetData.outputMixer = resourceData.outputMixer;
                        break;
                    case PropertyEditorTab.Effects:
                        Debug.LogError("未完成");
                        break;
                    case PropertyEditorTab.Attenuation:
                        targetData.spatialBlend = resourceData.spatialBlend;
                        targetData.panStereo = resourceData.panStereo;
                        targetData.dopplerLevel = resourceData.dopplerLevel;
                        targetData.spread = resourceData.spread;
                        targetData.reverbZoneMix = resourceData.reverbZoneMix;
                        targetData.ResetDistance(resourceData.MinDistance, resourceData.MaxDistance);
                        targetData.attenuationCurveSettings.Clear();

                        resourceData.attenuationCurveSettings.ForEach(curveSetting =>
                        {
                            var newData = new AttenuationCurveSetting(curveSetting.attenuationCurveType);
                            //目前只进行key的深度复制，有可能其他信息会没有同步
                            newData.curveData.keys = curveSetting.curveData.keys;
                            targetData.attenuationCurveSettings.Add(newData);
                        });
                        break;
                    case PropertyEditorTab.RTPC:
                        targetData.gameParameterCurveSettings.Clear();
                        resourceData.gameParameterCurveSettings.ForEach(curveSetting =>
                        {
                            var newData = new GameParameterCurveSetting(curveSetting.targetType, curveSetting.gameParameterId);
                            newData.curveData.keys = curveSetting.curveData.keys;
                            // newData.gameParameterId = curveSetting.gameParameterId;
                            targetData.gameParameterCurveSettings.Add(newData);
                        });
                        break;
                    case PropertyEditorTab.States:
                        Debug.LogError("未完成");
                        break;
                    case PropertyEditorTab.OtherSetting:
                        targetData.unloadClipWhenPlayEnd = resourceData.unloadClipWhenPlayEnd;
                        targetData.stopWhenGameObjectDestroy = resourceData.stopWhenGameObjectDestroy;
                        targetData.priority = resourceData.priority;
                        targetData.limitPlayNumber = resourceData.limitPlayNumber;
                        break;
                }
                //TODO 应该有多种类型的数据同步
                manager.SyncDataToPlayable(targetData.id, AudioComponentDataChangeType.General);
            }
        }

        /// <summary>
        /// 获取当前标签页的匹配id的树元素
        /// </summary>
        /// <param name="id">想要获取的元素的id</param>
        /// <returns></returns>
        public MyTreeElement GetTreeListElementByID(int id)
        {
            return ManagerData.myTreeLists[editorViewData.ProjectExplorerTabIndex].Find(x => x.id == id);
        }

    }

    /// <summary>
    /// 用于储存分隔栏的数据
    /// </summary>
    internal class SpliterData : ScriptableSingleton<SpliterData>
    {
        //继承自ScriptableSingleton的类可以将数据保存在编辑器内部中，这样在代码重编译时数据也不会丢失，并且在多人协作时具有各自不同的值
        //此项数据在关闭Unity工程后会被清除，可以与EditorPrefs配合使用做到数据的保存，不应用于保存重要数据
        public float Width { get; set; } = 360;
        public float Height { get; set; } = 580;
    }
}