using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Audio;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{
    public class AudioEditorView : EditorWindow
    {
        #region 参数设置
        //用于读写asset文件中的数据
        [SerializeField]
        private static EditorViewData data;
        public static readonly string EditorWindowDataPath = "Assets/AudioEditor/Data/AudioEditorWindowDataAsset.asset";
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

        //左上窗口(ProjectExplorer)的参数设置
        private enum ProjectExplorerTab
        {
            Audio, Events, GameSyncs, Banks
        }
        private static readonly string[] projectExplorerTabNames = Enum.GetNames(typeof(ProjectExplorerTab));

        //列表树的参数设置
        private SearchField m_SearchField;
        private MultiColumnTreeView[] myTreeViews = new MultiColumnTreeView[4];
        private MultiColumnHeaderState m_MultiColumnHeaderState;
        [SerializeField]
        private TreeModel<MyTreeElement>[] myTreeModels = new TreeModel<MyTreeElement>[4];

        //右上窗口（PropertyEditor）的参数设置
        private enum PropertyEditorTab
        {
            GeneralSettings, Effects, Attenuation, RTPC, States, OtherSetting
        }
        private static readonly string[] PropertyEditorTabNames = { "General Settings", "Effects", "3D Setting", "RTPC", "States", "Other Setting" };

        private PropertyEditorTab _propertyEditorTabIndex;

        private PropertyEditorTab PropertyEditorTabIndex
        {
            get => _propertyEditorTabIndex;
            set
            {
                _propertyEditorTabIndex = value;
                //右边窗口标签刷新时候重置页面参数
                FoldoutHeaderFlags = new List<bool>();
                ScorllViewPosition = Vector2.zero;
            }
        }

        private static readonly string[] EventTpyeNames = Enum.GetNames(typeof(AEEventType));

        //记录Header的展开状态，每次选择新的列表树中的内容后刷新
        private static List<bool> FoldoutHeaderFlags = new List<bool>();
        private static Vector2 ScorllViewPosition = Vector2.zero;

        private int whichEventUnitSelect = 0;

        private int whichRTPCSelect = 0;

        //右下窗口(PreviewEditor)的参数设置
        public enum PreviewModeState
        {
            playing,
            Paused,
            Stoped
        }
        private readonly string[] previewWindowGridButtonName = { "states", "RTPCs", "Switches", "Triggers" };
        private int previewWindowGridSelected = 0;

        public PreviewModeState previewState = PreviewModeState.Stoped;

        //其他设置
        private AudioEditorManager manager;
        private static AudioEditorView window;
        private GUISkin BoldLabelStyleSkin;
        private static Color backgroundBoxColor = Color.Lerp(Color.white, Color.black, 0.1f);
        private static Color preGUIColor = Color.white;
        private static Color rectSelectedColor = new Color(0, 0, 1, 0.2f);

        //委托
        public static event System.Action DestroyWindow;
        #endregion

        [MenuItem("Window/Audio Editor/EditorManager",false,1)]
        public static AudioEditorView GetWindow()
        {
            window = (AudioEditorView)EditorWindow.GetWindow(typeof(AudioEditorView), true, "Audio Editor Manager");
            if (EditorPrefs.HasKey("XSJ.YPZS.AudioEditor.FirstInit") == false || EditorPrefs.GetBool("XSJ.YPZS.AudioEditor.FirstInit") == false)
            {
                window.position = new Rect(300, 200, 1050, 650);
                EditorPrefs.SetBool("XSJ.YPZS.AudioEditor.FirstInit", true);
            }
            window.autoRepaintOnSceneChange = true;
            return window;
        }

        void OnEnable()
        {
            Init();
        }

        private void Init()
        {
            var loadEditorViewDataSuccess = false;
            var loadEditorManagerDataSuccess = false;

            manager = AudioEditorManager.FindManager();
            if (manager != null)
            {
                if (ManagerData == null)
                {
                    AudioEditorManager.LoadDataAsset();
                }
                if (ManagerData != null)
                {
                    loadEditorManagerDataSuccess = true;
                }
            }
            else
            {
                return;
            }

            data = AssetDatabase.LoadAssetAtPath(EditorWindowDataPath, typeof(EditorViewData)) as EditorViewData;
            if (data != null)
            {
                loadEditorViewDataSuccess = true;
            }

            if (loadEditorManagerDataSuccess == false || loadEditorViewDataSuccess == false)
            {
                data = ScriptableObject.CreateInstance<EditorViewData>();
                for (int i = 0; i < 4; i++)
                {
                    data.myTreeLists[i].Add(new MyTreeElement("root", -1, 0));
                    data.myTreeLists[i].Add(new MyTreeElement("Work Unit", 0, 1));
                }

                //创建目录
                ManagerData = ScriptableObject.CreateInstance<AudioEditorData>();
                var filePath = AudioEditorManager.DataPath;
                var number = filePath.LastIndexOf("/", StringComparison.Ordinal);
                filePath = filePath.Remove(number);
                System.IO.Directory.CreateDirectory(filePath);

                AssetDatabase.CreateAsset(data, EditorWindowDataPath);
                AssetDatabase.CreateAsset(ManagerData, AudioEditorManager.DataPath);
                Debug.Log("[AudioEditor]：创建数据");
            }

            horizontalSpliterRect = new Rect(0, CurrentHorizontalSpliterHeight, position.width, 3f);
            verticalSpliterRect = new Rect(CurrentVerticalSpliterWidth, 0, 3f, position.height);

            InitTreeView();

           BoldLabelStyleSkin = ScriptableObject.CreateInstance<GUISkin>();
           BoldLabelStyleSkin.label.fontStyle = FontStyle.Bold;

            preGUIColor = GUI.color;
        }


        #region 列表树初始化及相关事项注册
        /// <summary>
        /// 初始化列表树
        /// </summary>
        void InitTreeView()
        {
            // Debug.Log("InitTreeView");

            var headerState = MultiColumnTreeView.CreateDefaultMultiColumnHeaderState();
            if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
            {
                MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
            }

            m_MultiColumnHeaderState = headerState;
            var myMultiColumnHeader = new MultiColumnHeader(headerState);
            myMultiColumnHeader.ResizeToFit();
            myMultiColumnHeader.height = 20;

            m_SearchField = new SearchField();

            for (int i = 0; i < 4; i++)
            {
                myTreeModels[i] = new TreeModel<MyTreeElement>(data.myTreeLists[i]);
                myTreeViews[i] =
                    new MultiColumnTreeView(data.MyTreeViewStates[i], myMultiColumnHeader, myTreeModels[i]);

                m_SearchField.downOrUpArrowKeyPressed -= myTreeViews[i].SetFocusAndEnsureSelectedItem;
                m_SearchField.downOrUpArrowKeyPressed += myTreeViews[i].SetFocusAndEnsureSelectedItem;

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
            myTreeViews[0].TreeElementEnableChange -= ContainerChildEnableChange;
            myTreeViews[0].TreeElementEnableChange += ContainerChildEnableChange;
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
            switch (data.ProjectExplorerTabIndex)
            {
                case (int)ProjectExplorerTab.Audio:
                    var currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(id);
                    if (currentSelectData != null) currentSelectData.name = newName;
                    break;
                case (int)ProjectExplorerTab.Events:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEEvent>(id);
                    if (currentSelectData != null) currentSelectData.name = newName;
                    break;
                case (int)ProjectExplorerTab.GameSyncs:
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEGameSyncs>(id);
                    if (currentSelectData != null) currentSelectData.name = newName;
                    break;
                case (int)ProjectExplorerTab.Banks:
                    Debug.LogError("未完成");
                    break;
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
        /// <param name="dragitemIDs"></param>
        /// <param name="parentItem"></param>
        /// <returns></returns>
        private bool CheckDragItems(List<int> dragitemIDs, TreeViewItem parentItem)
        {
            var dragitemsHasWorkUnit = false;
            foreach (var dragitemID in dragitemIDs)
            {
                var treeElement = myTreeModels[data.ProjectExplorerTabIndex].Find(dragitemID);
                if (treeElement == null)
                {
                    Debug.LogError("[AudioEditor]：发生错误");
                    break;
                }
                //拖动的项目中有以下类别时，不允许进行拖动
                if (treeElement.Type == AEComponentType.Switch || treeElement.Type == AEComponentType.State)
                {
                    return false;
                }

                if (treeElement.Type == AEComponentType.Workunit)
                {
                    dragitemsHasWorkUnit = true;
                }
            }

            if (parentItem != null)
            {
                var parentdata = (parentItem as TreeViewItem<MyTreeElement>).data;
                //拖入的父级有以下类别时，不允许拖入
                if (parentdata.Type == AEComponentType.SwitchGroup ||
                    parentdata.Type == AEComponentType.StateGroup ||
                    parentdata.Type == AEComponentType.Event)
                {
                    return false;
                }
                //特殊情况,WorkUnit的父级必须为WorkUnit
                if (dragitemsHasWorkUnit && parentdata.Type != AEComponentType.Workunit)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 设置当外部资源拖入UI时的生成行为
        /// </summary>
        /// <param name="resourcePaths">资源的相对目录</param>
        /// <param name="dragInParentID">拖入的父级id</param>
        /// <param name="insertPosition">要插入的位置</param>
        private void OutSideResourceImport(List<string> resourcePaths, int dragInParentID, int insertPosition)
        {
            var newItemIDs = new List<int>();
            var parentItem = myTreeModels[0].Find(dragInParentID);
            foreach (var path in resourcePaths)
            {
                var newAudioClip = AssetDatabase.LoadAssetAtPath(path, typeof(AudioClip)) as AudioClip;
                var newComponentData = GenerateData(AEComponentType.SoundSFX, parentItem, insertPosition, newAudioClip.name) as SoundSFX;
                //如果需要在数据中剔除掉clip来使数据动态加载，需要修改此处
                // if (newAudioClip.LoadAudioData())
                // {
                //     Debug.Log("load success");
                // }
                newComponentData.clip = newAudioClip;
                CheckGUIDAndPathWhenClipLoad(newComponentData);
                // newComponentData.name = newComponentData.clip.name;
                data.GetTreeListElementByID(newComponentData.id).name = newComponentData.clip.name;
                newItemIDs.Add(newComponentData.id);
            }
            myTreeViews[0].SetSelection(newItemIDs);
            myTreeViews[0].Reload();


        }

        /// <summary>
        /// 当鼠标拖动释放时，处理拖动数据的原父类和现父类中的ChildrenID数据
        /// </summary>
        /// <param name="dragIDs">拖动的子项id</param>
        /// <param name="dragInParentID">要拖入的父项id</param>
        /// <param name="insertPosition">要插入的位置</param>
        private void DragInContainerChildrenID(List<int> dragIDs, int dragInParentID, int insertPosition)
        {
            //插入
            var dragInParentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(dragInParentID);
            //逆序插入使得插入序列与DragOut序列一致
            if (dragInParentData is AEContainer container)
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
                var parentID = myTreeModels[data.ProjectExplorerTabIndex].Find(childID).parent.id;
                dragOutParentIDs.Add(parentID);
            }
            //先插入后删除会导致当为同一层级内的排列时候，ChildrenID中有复数相同ID，需鉴别
            foreach (var ID in dragOutParentIDs)
            {
                var dragOutParentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(ID);
                if (dragOutParentData is AEContainer dragOutContainer)
                {
                    for (int i = 0; i < dragIDs.Count; i++)
                    {
                        if (ID == dragInParentID)
                        {
                            var index = dragOutContainer.ChildrenID.FindIndex(x => x == dragIDs[i]);
                            //当有多个数据进行插入时，数据的位置为插入位置+自身在拖动列表中的位置
                            if (index == insertPosition + i)
                            {
                                index = dragOutContainer.ChildrenID.FindLastIndex(x => x == dragIDs[i]);
                            }
                            dragOutContainer.ChildrenID.RemoveAt(index);
                            Debug.Log("remove Index:" + index);
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
                    if (dragOutParentData is SwitchContainer switchContainer)
                    {
                        switchContainer.ResetOutputIDList(dragIDs);
                    }
                }
            }

            GUI.changed = true;
            Debug.Log("Drag Complete");
        }

        /// <summary>
        /// 当Container中有子项的enabled切换时，修改Container中ChildID的相关内容
        /// </summary>
        /// <param name="Child"></param>
        private void ContainerChildEnableChange(MyTreeElement Child)
        {
            DataRegisterToUndo();
            var parentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(Child.parent.id);
            //若当前enabled为true，则从ChildrenID中删除，再切换为false
            if (Child.enabled)
            {
                if (parentData is AEContainer Container)
                {
                    var index = Container.ChildrenID.FindIndex(x => x == Child.id);
                    if (index != -1)
                    {
                        Container.ChildrenID.RemoveAt(index);
                    }
                }
            }
            else
            {
                //通过TreeView寻找当前Child在Parent中的位置，以确定应该在ChildrenID中插入的位置
                int insertPosition = -1;
                for (int i = 0; i < Child.parent.children.Count; i++)
                {
                    if (Child.parent.children[i].id == Child.id)
                    {
                        insertPosition++;
                        break;
                    }
                    else if (myTreeModels[data.ProjectExplorerTabIndex].Find(Child.parent.children[i].id).enabled)
                    {
                        insertPosition++;
                    }
                }
                //将ChildID插入对应的位置
                if (parentData is AEContainer Container)
                {
                    var index = Container.ChildrenID.FindIndex(x => x == Child.id);
                    if (index != -1) return;
                    Container.ChildrenID.Insert(insertPosition, Child.id);
                }
            }
        }
        /// <summary>
        /// 当列表选择变更时
        /// </summary>
        private void TreeViewSelectionChange()
        {
            switch (data.ProjectExplorerTabIndex)
            {
                case (int)ProjectExplorerTab.Audio:
                    //标志位等刷新
                    FoldoutHeaderFlags = new List<bool>();
                    ScorllViewPosition = Vector2.zero;

                    //_3DSettingScrollPosition = Vector2.zero;
                    //RTPCSettingScrollPosition = Vector2.zero;
                    whichRTPCSelect = 0;
                    if (data.freshPageWhenTabInexChange)
                    {
                        PropertyEditorTabIndex = 0;
                    }
                    GraphWaveDisplay.displayID = -1;

                    //停止正在预览的播放
                    previewState = PreviewModeState.Stoped;
                    manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                    break;
                case (int)ProjectExplorerTab.Events:
                    whichEventUnitSelect = 0;
                    break;
                case (int)ProjectExplorerTab.GameSyncs:
                    //StateGroupScrollPosition = Vector2.zero;
                    break;
                case (int)ProjectExplorerTab.Banks:
                    break;
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

        private void ShowTreeViewContextClickMenu()
        {
            // Debug.Log("ContextClickedItem:" + id);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("NewEvent/Play"), false, () =>
            {
                GenerateAEEvent(AEEventType.Play, data.MyTreeViewStates[0].selectedIDs);
            });
            menu.AddItem(new GUIContent("NewEvent/Pause"), false, () =>
            {
                GenerateAEEvent(AEEventType.Pause, data.MyTreeViewStates[0].selectedIDs);
            });
            menu.AddItem(new GUIContent("NewEvent/Resume"), false, () =>
            {
                GenerateAEEvent(AEEventType.Resume, data.MyTreeViewStates[0].selectedIDs);
            });
            menu.AddItem(new GUIContent("NewEvent/Stop"), false, () =>
            {
                GenerateAEEvent(AEEventType.Stop, data.MyTreeViewStates[0].selectedIDs);
            });
            // menu.AddItem(new GUIContent("NewEvent/StopAll"), false, () =>
            // {
            //     Debug.LogError("此功能尚未完成");
            // });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                TreeViewDelete(data.MyTreeViewStates[data.ProjectExplorerTabIndex].selectedIDs);
            });
            menu.ShowAsContext();
        }

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


        #endregion

        private void GenerateEffectSettingGameObject()
        {
            var transform = manager.gameObject.transform.Find("EffectSettingObject");
            if (transform == null)
            {
                effectSettingObject = EditorUtility.CreateGameObjectWithHideFlags("EffectSettingObject",
                    HideFlags.DontSave);
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
                    HideFlags.DontSave);
                previewObject.transform.parent = manager.transform;
            }
            else
            {
                previewObject = transform.gameObject;
            }
        }

        void OnDestroy()
        {
            //window = (AudioEditorView)EditorWindow.GetWindow(typeof(AudioEditorView), true, "Audio Editor Manager");
            //data.WindowPosition = window.position;
            Undo.undoRedoPerformed -= TreeViewUndoRedo;
            //取消列表命名中的状态
            for (int i = 0; i < 4; i++)
            {
                data.myTreeLists[i] = (List<MyTreeElement>)myTreeModels[i].GetData();
                myTreeViews[i].EndRename();
            }
            //停止正在播放的预览实例
            manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
            //卸载已加载的AudioClip
            AudioEditorManager.UnloadAllAudioClip();
            //取消关联clip，如果正在runtime时关闭编辑器界面则不取消关联，等待Manager调用Destroy时再取消关联
            if (ManagerData && ManagerData.StaticallyLinkedAudioClips == false && EditorApplication.isPlaying == false)
            {
                AudioEditorManager.UnlinkAllAudioClips();
            }

            //储存修改
            EditorUtility.SetDirty(data);
            if (manager)
            {
                EditorUtility.SetDirty(ManagerData);
            }
            AssetDatabase.SaveAssets();

            DestroyImmediate(effectSettingObject);
            DestroyImmediate(previewObject);

            DestroyWindow?.Invoke();
        }

        void OnGUI()
        {
            if (!manager)
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

            Undo.RecordObject(data, "AudioEditorWindow Change");
            Undo.RecordObject(ManagerData, "AudioEditorWindow Change");

            //TODO: 实例的数据同步

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            GUI.color = preGUIColor;
            EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(CurrentVerticalSpliterWidth), GUILayout.Height(CurrentHorizontalSpliterHeight));
            GUILayout.BeginArea(new Rect(0, 0, CurrentVerticalSpliterWidth, CurrentHorizontalSpliterHeight), GUI.skin.box);
            DrawProjectExplorerWindow();
            //  GUILayout.Box(new GUIContent("左上方窗口"), GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.EndArea();
            EditorGUILayout.EndScrollView();

            SetVerticalSpliter();

            EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(CurrentHorizontalSpliterHeight));
            var rect = new Rect(0, 0, position.width - CurrentVerticalSpliterWidth - verticalSpliterRect.width, CurrentHorizontalSpliterHeight);
            GUI.SetNextControlName("Empty Area");
            GUILayout.BeginArea(rect, GUI.skin.box);
            DrawPropertyEditorWindow();
            //GUILayout.Box(new GUIContent("右上方窗口"), GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
            {
                GUI.FocusControl("Empty Area");
                myTreeViews[data.ProjectExplorerTabIndex].EndRename();
            }
            GUILayout.EndArea();
            EditorGUILayout.EndScrollView();

            GUILayout.EndHorizontal();

            SetHorizontalSpliter();

            GUILayout.BeginHorizontal();

            EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Width(CurrentVerticalSpliterWidth), GUILayout.Height(position.height - CurrentHorizontalSpliterHeight - horizontalSpliterRect.height));
            GUILayout.BeginArea(new Rect(0, 0, CurrentVerticalSpliterWidth, position.height - CurrentHorizontalSpliterHeight - horizontalSpliterRect.height), GUI.skin.box);
            GUILayout.Box(new GUIContent("说明窗口"), GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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

            //Editor下更新频率低，主动在此调用
            // if (!EditorApplication.isPaused && !EditorApplication.isPlaying)
            // {
            //     for (int i = 0; i < manager.PlayableList.Count; i++)
            //     {
            //         manager.PlayableList[i].m_Graph.Evaluate();
            //     }
            // }

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
            int tabIndex = GUILayout.Toolbar(data.ProjectExplorerTabIndex, projectExplorerTabNames, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight));
            if (tabIndex != data.ProjectExplorerTabIndex)
            {
                data.ProjectExplorerTabIndex = tabIndex;
                for (int i = 0; i < myTreeViews.Length; i++)
                {
                    myTreeViews[i].EndRename();
                }
                Repaint();
            }
            GUILayoutOption[] buttonOptions = { GUILayout.MaxWidth(21), GUILayout.ExpandWidth(false) };
            GUI.color = backgroundBoxColor;
            switch (data.ProjectExplorerTabIndex)
            {
                case (int)ProjectExplorerTab.Audio:
                    EditorGUILayout.BeginHorizontal();
                    //New WorkUnit Buttonif (GUILayout.Button(new GUIContent(IconUtility.ComponentIconDictionary[AEComponentType.workUnit], "Work Unit"), GUI.skin.box, buttonOptions))
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Workunit), GUI.skin.box, buttonOptions))
                    {
                        var id = myTreeModels[0].GenerateUniqueID();
                        myTreeModels[0].AddElement(new MyTreeElement("Work Unit", 0, id, AEComponentType.Workunit), myTreeModels[0].root, myTreeModels[0].root.children.Count);
                        myTreeViews[0].EndRename();
                        myTreeViews[0].BeginRename(id);
                    }
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
                        //TODO : 实现BlandContainer功能
                        Debug.LogWarning("未完成");
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
                case (int)ProjectExplorerTab.Events:
                    EditorGUILayout.BeginHorizontal();
                    //WorkUnit
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Workunit), GUI.skin.box, buttonOptions))
                    {
                        myTreeModels[1].AddElement(new MyTreeElement("Work Unit", 0, myTreeModels[1].GenerateUniqueID(), AEComponentType.Workunit), myTreeModels[1].root, myTreeModels[1].root.children.Count);
                    }
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Event), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.Event);
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
                case (int)ProjectExplorerTab.GameSyncs:
                    var currentSelectID = data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID;
                    var currentSelectElement = myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectID);
                    EditorGUILayout.BeginHorizontal();
                    //WorkUnit
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Workunit), GUI.skin.box, buttonOptions))
                    {
                        myTreeModels[2].AddElement(new MyTreeElement("Work Unit", 0, myTreeModels[2].GenerateUniqueID(), AEComponentType.Workunit), myTreeModels[2].root, myTreeModels[2].root.children.Count);
                    }
                    EditorGUI.BeginDisabledGroup(currentSelectElement.Type != AEComponentType.Workunit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.SwitchGroup), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.SwitchGroup);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.BeginDisabledGroup(currentSelectElement.Type != AEComponentType.SwitchGroup && currentSelectElement.Type != AEComponentType.Switch);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.Switch), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.Switch);
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.Type != AEComponentType.Workunit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.StateGroup), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.StateGroup);
                        GenerateData(AEComponentType.State, dataName: "None");
                        myTreeViews[data.ProjectExplorerTabIndex].EndRename();
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.Type != AEComponentType.StateGroup && currentSelectElement.Type != AEComponentType.State);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.State), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.State);
                    }
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup(currentSelectElement.Type != AEComponentType.Workunit);
                    if (GUILayout.Button(IconUtility.GetIconContent(AEComponentType.GameParameter), GUI.skin.box, buttonOptions))
                    {
                        GenerateData(AEComponentType.GameParameter);
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUILayout.EndHorizontal();
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
                case (int)ProjectExplorerTab.Banks:
                    // if (GUILayout.Button("保存数据"))
                    // {
                    //
                    //     // AssetDatabase.SaveAssets();
                    //     if (manager)
                    //     {
                    //         EditorUtility.SetDirty(ManagerData);
                    //         EditorUtility.SetDirty(data);
                    //     }
                    //     AssetDatabase.SaveAssets();
                    //     //Debug.LogError("此页面未完成");
                    // }
                    if (GUILayout.Button("test"))
                    {
                        AudioEditorManager.GetAEComponentDataByID<Switch>(0);
                    }
                    GUI.color = preGUIColor;
                    DrawSearchBarAndTreeView();
                    break;
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
                case AEComponentType.Workunit:
                    break;
                case AEComponentType.SoundSFX:
                case AEComponentType.RandomContainer:
                case AEComponentType.SequenceContainer:
                case AEComponentType.SwitchContainer:
                case AEComponentType.BlendContainer:
                    data.ProjectExplorerTabIndex = 0;
                    break;
                case AEComponentType.Event:
                    data.ProjectExplorerTabIndex = 1;
                    break;
                case AEComponentType.SwitchGroup:
                case AEComponentType.Switch:
                case AEComponentType.StateGroup:
                case AEComponentType.State:
                case AEComponentType.GameParameter:
                    data.ProjectExplorerTabIndex = 2;
                    break;
                default:
                    Debug.LogError("发生错误");
                    return null;
            }

            if (parentElement == null)
            {
                parentElement = data.GetTreeListElementByID(data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID);
            }
            var depth = parentElement.depth + 1;
            //某些组件不能作为父类，如果当前选择为soundSFX、Event等，则再往上一级创建
            if (parentElement.Type == AEComponentType.SoundSFX ||
                parentElement.Type == AEComponentType.Event ||
                parentElement.Type == AEComponentType.GameParameter ||
                parentElement.Type == AEComponentType.Switch ||
                parentElement.Type == AEComponentType.State)
            {
                parentElement = (MyTreeElement)parentElement.parent;
                depth -= 1;
            }

            var id = myTreeModels[data.ProjectExplorerTabIndex].GenerateUniqueID();
            var name = dataName ?? string.Format("New " + type.ToString() + "_" + id);
            MyTreeElement newElement = new MyTreeElement(name, depth, id, type);
            if (insertPosition == -1)
            {
                insertPosition = parentElement.hasChildren ? parentElement.children.Count : 0;
            }
            myTreeModels[data.ProjectExplorerTabIndex].AddElement(newElement, parentElement, insertPosition);
            myTreeViews[data.ProjectExplorerTabIndex].SetExpanded(parentElement.id, true);

            var parentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(parentElement.id);

            if (parentData is AEContainer container)
            {
                container.ChildrenID.Insert(insertPosition, newElement.id);
            }
            myTreeViews[data.ProjectExplorerTabIndex].EndRename();
            myTreeViews[data.ProjectExplorerTabIndex].BeginRename(newElement.id);
            var newList = new List<int> { newElement.id };
            myTreeViews[data.ProjectExplorerTabIndex].SetSelection(newList);
            myTreeViews[data.ProjectExplorerTabIndex].FrameItem(newElement.id);
            return manager.GenerateManagerData(newElement.name, newElement.id, type, parentElement.id);
        }


        /// <summary>
        /// 绘制搜索框和列表树，实现删除等功能
        /// </summary>
        private void DrawSearchBarAndTreeView()
        {
            var e = Event.current;
            data.searchString = m_SearchField.OnGUI(data.searchString);
            myTreeViews[data.ProjectExplorerTabIndex].searchString = data.searchString;
            var rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            myTreeViews[data.ProjectExplorerTabIndex].OnGUI(rect);
            //实现删除功能
            if (GUIUtility.keyboardControl == myTreeViews[data.ProjectExplorerTabIndex].treeViewControlID && e.type == EventType.KeyDown && e.keyCode == KeyCode.Delete)
            {
                if (data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID != 0)
                {
                    //Debug.Log("delete");
                    var listID = data.MyTreeViewStates[data.ProjectExplorerTabIndex].selectedIDs;
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
                if(myTreeModels[data.ProjectExplorerTabIndex].Find(id) ==null) continue;
                var children = myTreeModels[data.ProjectExplorerTabIndex].Find(id).children;
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

            switch (data.ProjectExplorerTabIndex)
            {
                case (int)ProjectExplorerTab.Audio:
                    foreach (var id in idList)
                    {
                        if(myTreeModels[0].Find(id) ==null) continue;
                        //删除父类中ChildID中的相关子类id
                        var parentID = myTreeModels[0].Find(id).parent.id;
                        var parentData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(parentID);
                        if (parentData is AEContainer container)
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
                case (int)ProjectExplorerTab.Events:
                    foreach (var id in idList)
                    {
                        manager.RemoveAEComponentDataByID<AEEvent>(id);
                    }
                    break;
                case (int)ProjectExplorerTab.GameSyncs:
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
            myTreeModels[data.ProjectExplorerTabIndex].RemoveElements(idList);
            data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID = 0;
        }


        #endregion

        #region 绘制右上的属性编辑界面
        private void DrawPropertyEditorWindow()
        {
            var currentSelectID = data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID;
            switch (data.GetTreeListElementByID(currentSelectID).Type)
            {
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
                    DrawNameUnit(currentSelectSwitch);
                    break;
                case AEComponentType.StateGroup:
                    DrawStateGroupProperty(currentSelectID);
                    break;
                case AEComponentType.State:
                    // var currentSelectState = AudioEditorManager.GetAEComponentDataByID<State>(currentSelectID);
                    var currentSelectState = GetCurrentSelectData(currentSelectID, AEComponentType.State) as State;
                    DrawNameUnit(currentSelectState);
                    break;
                case AEComponentType.GameParameter:
                    DrawGameParameterProperty(currentSelectID);
                    break;
            }
        }

        private void DrawSoundSFXPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SoundSFX) as SoundSFX;

            EditorGUI.BeginDisabledGroup(!myTreeModels[data.ProjectExplorerTabIndex].Find(id).enabled);

            DrawPropertyEditorTitleToolBar(currentSelectData);
            switch (PropertyEditorTabIndex)
            {
                case PropertyEditorTab.GeneralSettings:
                    {
                        EditorGUILayout.BeginHorizontal();

                        var name = EditorGUILayout.TextField(currentSelectData.name);
                        if (name != currentSelectData.name)
                        {
                            data.GetTreeListElementByID(id).name = name;
                            myTreeViews[data.ProjectExplorerTabIndex].Reload();
                        }
                        currentSelectData.name = data.GetTreeListElementByID(id).name;
                        GUILayout.Label("id:");
                        EditorGUI.BeginDisabledGroup(data.ProjectExplorerTabIndex != 1);
                        EditorGUILayout.IntField(currentSelectData.id);
                        EditorGUI.EndDisabledGroup();
                        GUILayout.FlexibleSpace();
                        if (currentSelectData.clipAssetPath != "" || currentSelectData.clipGUID != "")
                        {
                            if (currentSelectData.clip == null)
                            {
                                currentSelectData.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(currentSelectData.clipAssetPath);
                            }
                            if (currentSelectData.clip == null)
                            {
                                var path = AssetDatabase.GUIDToAssetPath(currentSelectData.clipGUID);
                                currentSelectData.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                                if (currentSelectData.clip != null)
                                    currentSelectData.clipAssetPath = path;
                            }
                            if (currentSelectData.clip == null && currentSelectData.clipAssetPath !="")
                            {
                                Debug.LogError($"[AudioEditor]: 组件名:{currentSelectData.name}，id:{currentSelectData.id}, 无法加载AudioClip，请检查文件列表，路径为{currentSelectData.clipAssetPath}");
                                currentSelectData.clipAssetPath = "";
                            }

                            if (currentSelectData.clip != null)
                            {
                                //TODO: 应该在编辑器打开时候遍历一次检查clip的GUID和路径是否正确，以避免在关闭时候移动文件导致数据错误
                                CheckGUIDAndPathWhenClipLoad(currentSelectData);
                            }
                        }
                        var newAudioClip = EditorGUILayout.ObjectField(currentSelectData.clip, typeof(AudioClip), true) as AudioClip;
                        if (newAudioClip != currentSelectData.clip)
                        {
                            currentSelectData.clip = newAudioClip;
                            CheckGUIDAndPathWhenClipLoad(currentSelectData);
                            GraphWaveDisplay.displayID = -1;
                            manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.AudioClipChange);
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

                        EditorGUILayout.LabelField("Container Setting", BoldLabelStyleSkin.label);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Type"));
                        currentSelectData.playType = (RandomContainerPlayType)GUILayout.Toolbar((int)currentSelectData.playType, Enum.GetNames(typeof(RandomContainerPlayType)));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Mode"));
                        currentSelectData.PlayMode = (ContainerPlayMode)GUILayout.Toolbar((int)currentSelectData.PlayMode, Enum.GetNames(typeof(ContainerPlayMode)));
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
                    EditorGUILayout.GetControlRect(GUILayout.ExpandHeight(true));
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
        }

        private void DrawSequenceContainerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SequenceContainer) as SequenceContainer;

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

                        EditorGUILayout.LabelField("Container Setting", BoldLabelStyleSkin.label);
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


        }

        #region 绘制SwtichContianer的属性编辑界面
        // private int outputListWaitForSelect;
        private void DrawSwitchContainerPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.SwitchContainer) as SwitchContainer;

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

                        EditorGUILayout.LabelField("Container Setting", BoldLabelStyleSkin.label);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(new GUIContent("Play Mode", "决定播放模式为步进或连续"));
                        currentSelectData.PlayMode = (ContainerPlayMode)GUILayout.Toolbar((int)currentSelectData.PlayMode, Enum.GetNames(typeof(ContainerPlayMode)));
                        EditorGUILayout.EndHorizontal();

                        var switchGroupData = AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(currentSelectData.switchGroupID) as SwitchGroup;
                        if (switchGroupData == null)
                        {
                            currentSelectData.switchGroupID = -1;
                            Repaint();
                        }
                        //TODO: container的特别控件没有实例同步数据检测
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
                        EditorGUI.indentLevel--;
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Group Setting", BoldLabelStyleSkin.label);
                        if (currentSelectData.switchGroupID != -1)
                        {
                            if (FoldoutHeaderFlags.Count != switchGroupData.SwitchListCount)
                            {
                                FoldoutHeaderFlags.Clear();
                            }
                            if (FoldoutHeaderFlags.Count == 0)
                            {
                                for (int i = 0; i < switchGroupData.SwitchListCount; i++)
                                {
                                    FoldoutHeaderFlags.Add(true);
                                }
                            }

                            ScorllViewPosition = EditorGUILayout.BeginScrollView(ScorllViewPosition);
                            for (int i = 0; i < switchGroupData.SwitchListCount; i++)
                            {
                                var rect = EditorGUILayout.GetControlRect();
                                rect.width -= GUI.skin.verticalScrollbar.fixedWidth;
                                var name = i == currentSelectData.defualtPlayIndex ? string.Format(switchGroupData.FindSwitchAt(i).name + "(default)") : switchGroupData.FindSwitchAt(i).name;
                                FoldoutHeaderFlags[i] = EditorGUI.BeginFoldoutHeaderGroup(rect, FoldoutHeaderFlags[i], name);
                                if (FoldoutHeaderFlags[i])
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
        }

        private void ShowSwitchGroupMenu(Rect rect, SwitchContainer currentSelectData)
        {
            GenericMenu menu = new GenericMenu();
            menu.allowDuplicateNames = true;
            foreach (var item in ManagerData.GameSyncsData)
            {
                if (item is SwitchGroup switchGroup)
                {
                    menu.AddItem(new GUIContent(switchGroup.name), currentSelectData.switchGroupID == switchGroup.id,
                        () =>
                        {
                            currentSelectData.switchGroupID = switchGroup.id;
                            //初始化output数据，用于对应switch列表
                            currentSelectData.outputIDList = new List<int>();
                            for (int i = 0; i < (AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(switchGroup.id) as SwitchGroup).SwitchListCount; i++)
                            {
                                currentSelectData.outputIDList.Add(-1);
                            }
                            FoldoutHeaderFlags.Clear();
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
            foreach (var ID in currentSelectedData.ChildrenID)
            {
                var name = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(ID).name;
                menu.AddItem(new GUIContent(name), currentSelectedData.outputIDList[outputListWaitForSelect] == ID, (x) =>
                {
                    currentSelectedData.outputIDList[(int)x] = ID;
                }, outputListWaitForSelect);
            }
            menu.DropDown(rect);
        }

        #endregion

        private void DrawEventPropertyEditor(int id)
        {
            var currentSelectData = GetCurrentSelectData(id, AEComponentType.Event) as AEEvent;

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
                        GUI.color = rectSelectedColor;
                    }
                    // GUI.color = whichEventUnitSelect == i ? rectSelectedColor : backgroundBoxColor;
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        var newEventType = (AEEventType)EditorGUILayout.Popup((int)currentSelectData.eventList[i].type, EventTpyeNames, GUILayout.Width((position.width - CurrentHorizontalSpliterHeight) / 5 - 10));
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
                                var targetId = currentSelectData.eventList[i].targetID;
                                var targetData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(targetId);
                                if (targetData == null)
                                {
                                    targetId = currentSelectData.eventList[i].targetID = -1;
                                }
                                var showName = targetId == -1 ? "null" : AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(targetId).name;
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    EditorGUILayout.TextField(showName, GUILayout.ExpandWidth(true));
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
                                if (currentSelectData.eventList[i].targetID != -1)
                                {
                                    var selectSwitch = AudioEditorManager.GetAEComponentDataByID<Switch>(switchID);
                                    showSwitchName = selectSwitch.name;
                                }
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    EditorGUILayout.TextField(showSwitchName, GUILayout.ExpandWidth(true));
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
                                if (currentSelectData.eventList[i].targetID != -1)
                                {
                                    var selectState = AudioEditorManager.GetAEComponentDataByID<State>(stateID);
                                    showStateName = selectState.name;
                                }
                                using (new EditorGUI.DisabledGroupScope(true))
                                {
                                    EditorGUILayout.TextField(showStateName, GUILayout.ExpandWidth(true));
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
                    switch (currentSelectData.eventList[whichEventUnitSelect].type)
                    {
                        case AEEventType.Play:
                            currentSelectData.eventList[whichEventUnitSelect].probability = EditorGUILayout.IntSlider(new GUIContent("Probability", "触发概率"),
                                currentSelectData.eventList[whichEventUnitSelect].probability, 0, 100);
                            break;
                        case AEEventType.Pause:
                            currentSelectData.eventList[whichEventUnitSelect].scope = (Scope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                currentSelectData.eventList[whichEventUnitSelect].scope);
                            break;
                        case AEEventType.PauseAll:
                            currentSelectData.eventList[whichEventUnitSelect].scope = (Scope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                currentSelectData.eventList[whichEventUnitSelect].scope);
                            break;
                        case AEEventType.Resume:
                            currentSelectData.eventList[whichEventUnitSelect].scope = (Scope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                currentSelectData.eventList[whichEventUnitSelect].scope);
                            break;
                        case AEEventType.ResumeAll:
                            currentSelectData.eventList[whichEventUnitSelect].scope = (Scope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                currentSelectData.eventList[whichEventUnitSelect].scope);
                            break;
                        case AEEventType.Stop:
                            currentSelectData.eventList[whichEventUnitSelect].scope = (Scope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                currentSelectData.eventList[whichEventUnitSelect].scope);
                            break;
                        case AEEventType.StopAll:
                            currentSelectData.eventList[whichEventUnitSelect].scope = (Scope)EditorGUILayout.EnumPopup(new GUIContent("Scope", "作用范围"),
                                currentSelectData.eventList[whichEventUnitSelect].scope);
                            break;
                        case AEEventType.SetSwitch:
                            EditorGUILayout.Space();
                            break;
                        case AEEventType.SetState:
                            EditorGUILayout.Space();
                            break;
                        case AEEventType.InstallBank:
                            EditorGUILayout.Space();
                            break;
                        case AEEventType.UnInstallBank:
                            EditorGUILayout.Space();
                            break;
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

            ScorllViewPosition =
                GUILayout.BeginScrollView(ScorllViewPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
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
                    currentSelectData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(id);
                    break;
                case AEComponentType.BlendContainer:
                    Debug.LogError("未完成");
                    return null;
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
                    Debug.LogError("id匹配发生错误，请检查");
                    return null;
            }
            var parent = myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectData.id).parent as MyTreeElement;
            if (currentSelectData is AEAudioComponent audioComponent)
            {
                audioComponent.isContianerChild = parent.Type != AEComponentType.Workunit;
            }

            return currentSelectData;
        }

        #region 绘制属性编辑界面的一些单元

        private void DrawPropertyEditorTitleToolBar(AEAudioComponent currentSelectData)
        {
            EditorGUILayout.BeginHorizontal();
            var newPropertyEditorTabIndex = (PropertyEditorTab)
                GUILayout.Toolbar((int)PropertyEditorTabIndex, PropertyEditorTabNames, EditorStyles.toolbarButton, GUI.ToolbarButtonSize.FitToContents, GUILayout.ExpandWidth(false));
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
                    var parentTreeElement = myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
                    var tempList = new HashSet<int> { parentTreeElement.id };
                    DeepCopypropertyEditorTabPageData(currentSelectData, tempList);
                });
                menu.AddItem(new GUIContent("复制此页数据至/所有同级"), false, () =>
                {
                    DataRegisterToUndo();
                    var parentTreeElement = myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
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
                    var parentTreeElement = myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
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
                    var parentworkUnitTreeElement = myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectData.id).parent;
                    int i = 0;
                    while ((parentworkUnitTreeElement as MyTreeElement).Type != AEComponentType.Workunit)
                    {
                        parentworkUnitTreeElement = parentworkUnitTreeElement.parent;
                        i++;
                        if (i == 100) break;
                    }
                    Debug.Log(parentworkUnitTreeElement.name);
                    if ((parentworkUnitTreeElement as MyTreeElement) == myTreeModels[data.ProjectExplorerTabIndex].root || i == 100)
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
                data.GetTreeListElementByID(currentSelectData.id).name = name;
                myTreeViews[data.ProjectExplorerTabIndex].Reload();
            }
            currentSelectData.name = data.GetTreeListElementByID(currentSelectData.id).name;
            GUILayout.Label("id:");
            EditorGUI.BeginDisabledGroup(data.ProjectExplorerTabIndex != 1);
            var newID = EditorGUILayout.DelayedIntField(currentSelectData.id);
            if (newID != currentSelectData.id)
            {
                //只能修改Event的id
                if (data.ProjectExplorerTabIndex == 1)
                {
                    if (myTreeModels[data.ProjectExplorerTabIndex].Find(newID) == null)
                    {
                        myTreeModels[data.ProjectExplorerTabIndex].Find(currentSelectData.id).id = newID;
                        currentSelectData.id = newID;
                        data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID = newID;
                        myTreeViews[data.ProjectExplorerTabIndex].Reload();
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
            EditorGUILayout.LabelField("General Setting", BoldLabelStyleSkin.label);
            EditorGUI.indentLevel++;
            currentSelectData.volume = EditorGUILayout.Slider(new GUIContent("Volume", "音量"), currentSelectData.volume, 0f, 1f);
            currentSelectData.pitch = EditorGUILayout.Slider(new GUIContent("Pitch", "音高"), currentSelectData.pitch, 0, 3);
            currentSelectData.delayTime =Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Delay Time"), currentSelectData.delayTime, GUILayout.ExpandWidth(false)), 0,999); 
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In GeneralSettingUnit");
            }
        }

        private void DrawTransitionsUnit(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Transitions", BoldLabelStyleSkin.label);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUI.indentLevel++;
                currentSelectData.fadeIn = EditorGUILayout.BeginToggleGroup("Fade In", currentSelectData.fadeIn);
                currentSelectData.fadeInTime = EditorGUILayout.FloatField(new GUIContent("FadeIn Time", "淡入时间"), currentSelectData.fadeInTime);
                currentSelectData.fadeInType = (FadeType)EditorGUILayout.EnumPopup(new GUIContent("FadeIn Type", "淡入类型"), currentSelectData.fadeInType);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndToggleGroup();

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUI.indentLevel++;
                currentSelectData.fadeOut = EditorGUILayout.BeginToggleGroup(new GUIContent("Fade Out", "淡出"), currentSelectData.fadeOut);
                currentSelectData.fadeOutTime = EditorGUILayout.FloatField(new GUIContent("FadeOut Time", "淡出时间"), currentSelectData.fadeOutTime);
                currentSelectData.fadeOutType = (FadeType)EditorGUILayout.EnumPopup(new GUIContent("FadeOut Type", "淡出类型"), currentSelectData.fadeOutType);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndToggleGroup();

            //if (currentSelectData is RandomContainer randomContainer )
            //{
            //    EditorGUILayout.Space();

            //    using (new EditorGUILayout.VerticalScope())
            //    {
            //        EditorGUI.indentLevel++;
            //        randomContainer.crossFade= EditorGUILayout.BeginToggleGroup(new GUIContent("CrossFade", "交叉淡变"), randomContainer.crossFade);
            //        randomContainer.crossFadeTime = EditorGUILayout.FloatField(new GUIContent("CrossFade Time"), randomContainer.crossFadeTime);
            //        randomContainer.crossFadeType = (CrossFadeType)EditorGUILayout.EnumPopup(new GUIContent("CrossFade Type", "淡出类型"), randomContainer.crossFadeType);
            //        EditorGUI.indentLevel--;
            //    }
            //    EditorGUILayout.EndToggleGroup();
            //}
            //if (currentSelectData is SequenceContainer sequenceContainer)
            //{

            //}
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In DrawTransitionsUnit");
            }
        }

        private void DrawGeneralSettingsSidePage(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(currentSelectData.isContianerChild);
            currentSelectData.outputMixer = EditorGUILayout.ObjectField(currentSelectData.outputMixer, typeof(AudioMixerGroup), true) as AudioMixerGroup;
            EditorGUI.EndDisabledGroup();
            currentSelectData.mute = EditorGUILayout.ToggleLeft("Mute", currentSelectData.mute);
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
                    manager.SyncPlayableData(currentSelectData.id, AudioComponentDataChangeType.LoopIndexChange);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndToggleGroup();

            //GUILayout.Box("待施工", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In DrawGeneralSettingsSidePage");
            }
        }

        private void DrawEffectsSetting(AEAudioComponent currentSelectData)
        {
            if (FoldoutHeaderFlags.Count == 0 || FoldoutHeaderFlags.Count != currentSelectData.effectSettings.Count)
            {
                FoldoutHeaderFlags.Clear();
                for (int i = 0; i < currentSelectData.effectSettings.Count; i++)
                {
                    FoldoutHeaderFlags.Add(true);
                }
            }

            EditorGUI.BeginChangeCheck();
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
                            case AEEffectType.ReverbZoneFilter:
                                currentSelectData.effectSettings.Add(new AEReverbZone());
                                break;
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
                        FoldoutHeaderFlags.Add(true);
                        manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.AddEffect);
                    }, i);
                }
                menu.DropDown(buttonRect);
            }
            GUI.color = backgroundBoxColor;
            using (new GUILayout.VerticalScope("box"))
            {
                GUI.color = preGUIColor;
                ScorllViewPosition = EditorGUILayout.BeginScrollView(ScorllViewPosition);
                for (int i = 0; i < currentSelectData.effectSettings.Count; i++)
                {
                    var effectSetting = currentSelectData.effectSettings[i];
                    FoldoutHeaderFlags[i] = EditorGUILayout.BeginFoldoutHeaderGroup(FoldoutHeaderFlags[i],
                        new GUIContent(effectSetting.type.ToString()),
                        menuAction: rect =>
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Delete"), false, () =>
                             {
                                 DataRegisterToUndo();
                                 var deleteEffectType = effectSetting.type;
                                 currentSelectData.effectSettings.Remove(effectSetting);
                                 FoldoutHeaderFlags.Clear();
                                 manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.DeleteEffect, deleteEffectType);
                             });
                            menu.AddItem(new GUIContent("ResetData"), false, () =>
                             {
                                 effectSetting.Reset();
                                 manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
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
                            FoldoutHeaderFlags.Clear();
                            manager.SyncPlayableData(currentSelectData.id, AudioComponentDataChangeType.DeleteEffect, deleteEffectType);
                        });
                        menu.AddItem(new GUIContent("ResetData"), false, () =>
                        {
                            effectSetting.Reset();
                            manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                        });
                        menu.ShowAsContext();
                    }
                    if (FoldoutHeaderFlags[i])
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
                                    var newPreset = (AudioReverbPreset)EditorGUILayout.EnumPopup(new GUIContent("Preset", "预设"),
                                  reverbFilter.preset);
                                    if (newPreset != reverbFilter.preset)
                                    {
                                        effectSettingObject.GetComponent<AudioReverbFilter>().reverbPreset = newPreset;
                                        reverbFilter.SetData(effectSettingObject.GetComponent<AudioReverbFilter>());
                                        effectSettingObject.GetComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Off;
                                    }
                                    EditorGUI.BeginDisabledGroup(reverbFilter.preset != AudioReverbPreset.User);
                                    reverbFilter.dryLevel = EditorGUILayout.IntSlider(new GUIContent("Dry Level"),
                                       (int)reverbFilter.dryLevel, -10000, 0);
                                    reverbFilter.room = EditorGUILayout.IntSlider(new GUIContent("Room"), (int)reverbFilter.room,
                                        -10000, 0);
                                    reverbFilter.roomHF = EditorGUILayout.IntSlider(new GUIContent("Room HF"),
                                        (int)reverbFilter.roomHF, -10000, 0);
                                    reverbFilter.roomLF = EditorGUILayout.IntSlider(new GUIContent("Room LF"),
                                        (int)reverbFilter.roomLF, -10000, 0);
                                    reverbFilter.decayTime = EditorGUILayout.Slider(new GUIContent("Decay Time"),
                                        reverbFilter.decayTime, 0.1f, 20f);
                                    reverbFilter.decayHFRatio = EditorGUILayout.Slider(new GUIContent("Decay HF Ratio"),
                                        reverbFilter.decayHFRatio, 0.1f, 2f);
                                    reverbFilter.reflectionsLevel = EditorGUILayout.IntSlider(new GUIContent("Reflections Level"),
                                        (int)reverbFilter.reflectionsLevel, -10000, 1000);
                                    reverbFilter.reflectionsDelay = EditorGUILayout.Slider(new GUIContent("Reflections Delay"),
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
                            case AEEffectType.ReverbZoneFilter:
                                if (effectSetting is AEReverbZone reverbZoneFilter)
                                {
                                    reverbZoneFilter.minDistance = EditorGUILayout.FloatField(new GUIContent("Min Distance"), reverbZoneFilter.minDistance);
                                    reverbZoneFilter.maxDistance = EditorGUILayout.FloatField(new GUIContent("Max Distance"), reverbZoneFilter.maxDistance);
                                    EditorGUILayout.Space();
                                    var newPreset = (AudioReverbPreset)EditorGUILayout.EnumPopup(new GUIContent("Preset", "预设"),
                                        reverbZoneFilter.preset);
                                    if (newPreset != reverbZoneFilter.preset)
                                    {
                                        effectSettingObject.GetComponent<AudioReverbZone>().reverbPreset = newPreset;
                                        reverbZoneFilter.SetData(effectSettingObject.GetComponent<AudioReverbZone>());
                                        effectSettingObject.GetComponent<AudioReverbZone>().reverbPreset = AudioReverbPreset.Off;
                                    }
                                    EditorGUI.BeginDisabledGroup(reverbZoneFilter.preset != AudioReverbPreset.User);
                                    reverbZoneFilter.room = EditorGUILayout.IntSlider(new GUIContent("Room"), reverbZoneFilter.room, -10000, 0);
                                    reverbZoneFilter.roomHF = EditorGUILayout.IntSlider(new GUIContent("Room HF"), reverbZoneFilter.roomHF, -10000, 0);
                                    reverbZoneFilter.roomLF = EditorGUILayout.IntSlider(new GUIContent("Room LF"), reverbZoneFilter.roomLF, -10000, 0);
                                    reverbZoneFilter.decayTime = EditorGUILayout.Slider(new GUIContent("Decay Time"), reverbZoneFilter.decayTime, 0.1f, 20f);
                                    reverbZoneFilter.decayHFRatio = EditorGUILayout.Slider(new GUIContent("Decay HF Ratio"), reverbZoneFilter.decayHFRatio, 0.1f, 2f);
                                    reverbZoneFilter.reflections = EditorGUILayout.IntSlider(new GUIContent("Reflections"), reverbZoneFilter.reflections, -10000, 1000);
                                    reverbZoneFilter.reflectionsDelay = EditorGUILayout.Slider(new GUIContent("Reflections Delay"), reverbZoneFilter.reflectionsDelay, 0, 0.3f);
                                    reverbZoneFilter.reverb = EditorGUILayout.IntSlider(new GUIContent("Reverb"), reverbZoneFilter.reverb, -10000, 2000);
                                    reverbZoneFilter.reverbDelay = EditorGUILayout.Slider(new GUIContent("Reverb Delay"), reverbZoneFilter.reverbDelay, 0, 0.1f);
                                    reverbZoneFilter.hfReference = EditorGUILayout.IntSlider(new GUIContent("HF Reference"),(int)reverbZoneFilter.hfReference, 1000, 20000);
                                    reverbZoneFilter.lfReference = EditorGUILayout.IntSlider(new GUIContent("LF Reference"), (int)reverbZoneFilter.lfReference, 20, 1000);
                                    reverbZoneFilter.diffusion = EditorGUILayout.Slider(new GUIContent("Diffusion"), reverbZoneFilter.diffusion, 0, 100f);
                                    reverbZoneFilter.density = EditorGUILayout.Slider(new GUIContent("Density"), reverbZoneFilter.density, 0, 100f);
                                    EditorGUI.EndDisabledGroup();
                                }
                                break;
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

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In EffectsSetting");
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
                EditorGUILayout.PrefixLabel(new GUIContent("Sterop Pan", "设置2D分量的偏移程度，-1为左声道，1为右声道"));
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
                ScorllViewPosition =
                    EditorGUILayout.BeginScrollView(ScorllViewPosition, GUILayout.Height(120));
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
                        EditorGUI.DrawRect(colorRect, ColorRankList[i]);
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
            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In 3DSetting");
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
                    manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
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
            float? yAxisMin = null;
            float? yAxisMax = null;
            if (currentSelectData.gameParameterCurveSettings.Count > 0 && whichRTPCSelect < currentSelectData.gameParameterCurveSettings.Count)
            {
                var gameParameterID = currentSelectData.gameParameterCurveSettings[whichRTPCSelect].gameParameterID;
                var gameParameter = AudioEditorManager.GetAEComponentDataByID<AEGameSyncs>(gameParameterID) as GameParameter;
                if (gameParameter != null)
                {
                    gameParameterMinValue = gameParameter.MinValue;
                    gameParameterMaxValue = gameParameter.MaxValue;
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
            GraphCurveRendering.Draw(graphRect, tempCurve, ColorRankList[whichRTPCSelect], gameParameterMinValue, gameParameterMaxValue, yAxisMin, yAxisMax);

            int Column1With = 100;
            int Column2With = 200;
            using (new GUILayout.VerticalScope("box"))
            {
                GUI.color = backgroundBoxColor;
                EditorGUILayout.BeginHorizontal();
                //GUILayout.Space(5);
                GUILayout.Box(new GUIContent("Y Axis"), GUILayout.Width(Column1With));
                GUILayout.Box(new GUIContent("X Axis"), GUILayout.Width(Column2With));
                GUILayout.Box(new GUIContent("Curve"), GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                GUI.color = preGUIColor;

                ScorllViewPosition =
                    EditorGUILayout.BeginScrollView(ScorllViewPosition, GUILayout.Height(100));

                var rect = Rect.zero;

                for (int i = 0; i < currentSelectData.gameParameterCurveSettings.Count; i++)
                {
                    if (whichRTPCSelect == i)
                    {
                        GUI.color = rectSelectedColor;
                    }
                    using (new GUILayout.HorizontalScope("box"))
                    {
                        GUI.color = preGUIColor;
                        var curveSetting = currentSelectData.gameParameterCurveSettings[i];
                        rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(5));
                        EditorGUI.DrawRect(rect, ColorRankList[i]);
                        EditorGUILayout.LabelField(new GUIContent(curveSetting.targetType.ToString()), GUILayout.Width(Column1With - 5));
                        var gameParameter = AudioEditorManager.GetAEComponentDataByID<AEGameSyncs>(curveSetting.gameParameterID) as GameParameter;
                        if (gameParameter == null)
                        {
                            rect = EditorGUILayout.GetControlRect(true, GUILayout.Width(Column2With - 20));
                            if (EditorGUI.DropdownButton(rect, new GUIContent(">>"), FocusType.Passive))
                            {
                                // GetRelatedGameParameterMenu(rect, curveSetting);
                                var menu = new GenericMenu();
                                menu.allowDuplicateNames = false;
                                for (int j = 0; j < ManagerData.GameSyncsData.Count; j++)
                                {
                                    if (ManagerData.GameSyncsData[j] is GameParameter gameParameterData)
                                        menu.AddItem(new GUIContent(gameParameterData.name), false, (id) =>
                                        {
                                            DataRegisterToUndo();
                                            curveSetting.gameParameterID = (int)id;
                                            manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                                        }, gameParameterData.id);
                                }
                                menu.DropDown(rect);
                            }
                            GUILayout.Space(20);
                        }
                        else
                        {
                            EditorGUILayout.LabelField(new GUIContent(gameParameter.name), GUILayout.Width(Column2With-40));
                            if (EditorGUILayout.DropdownButton(new GUIContent("  "), FocusType.Passive,GUILayout.Width(20)))
                            {
                                //TODO: 完成RTPC的X轴选项功能
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
                        Event.current.Use();
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
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In RTPC");
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
            menu.allowDuplicateNames = false;
            var curveTypeStr = Enum.GetNames(typeof(GameParameterTargetType));
            for (int i = 0; i < curveTypeStr.Length; i++)
            {
                menu.AddItem(new GUIContent(curveTypeStr[i]), false, (cureType) =>
                {
                    //目前根据需求只允许一个属性由一个GameParameter控制，最终实现应该可以一个属性由多个GameParameter控制
                    foreach (var curveSetting in currentSelectData.gameParameterCurveSettings)
                    {
                        if (curveSetting.targetType.CompareTo((GameParameterTargetType)cureType) == 0)
                        {
                            return;
                        }
                    }
                    DataRegisterToUndo();
                    currentSelectData.gameParameterCurveSettings.Add(new GameParameterCurveSetting((GameParameterTargetType)cureType));
                    manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                }, i);
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
                foreach (var gameSyncs in ManagerData.GameSyncsData)
                {
                    if (gameSyncs is StateGruop stateGruop)
                    {
                        var check = false;
                        foreach (var stateSetting in currentSelectData.StateSettings)
                        {
                            if (stateGruop.id == stateSetting.stateGroupId) check = true;
                            break;
                        }
                        if (check == true) continue;
                        menu.AddItem(new GUIContent(stateGruop.name), false, (selectedStateGruop) =>
                        {
                            DataRegisterToUndo();
                            currentSelectData.StateSettings.Add(new StateSetting(selectedStateGruop as StateGruop));
                            manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                        }, stateGruop);
                    }
                }
                menu.DropDown(buttonRect);
            }
            //EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            if (FoldoutHeaderFlags.Count == 0 || FoldoutHeaderFlags.Count != currentSelectData.StateSettings.Count)
            {
                FoldoutHeaderFlags.Clear();
                for (int i = 0; i < currentSelectData.StateSettings.Count; i++)
                {
                    FoldoutHeaderFlags.Add(true);
                }
            }
            GUI.color = backgroundBoxColor;
            using (new GUILayout.VerticalScope("box"))
            {
                GUI.color = preGUIColor;
                ScorllViewPosition = EditorGUILayout.BeginScrollView(ScorllViewPosition);
                for (int i = 0; i < currentSelectData.StateSettings.Count; i++)
                {
                    //检测对应group是否被删除
                    var stateGruop =
                        AudioEditorManager.GetAEComponentDataByID<StateGruop>(currentSelectData.StateSettings[i]
                            .stateGroupId) as StateGruop;
                    if (stateGruop == null)
                    {
                        currentSelectData.StateSettings.RemoveAt(i);
                        Repaint();
                    }
                    if (currentSelectData.StateSettings[i].volumeList.Count != stateGruop.StateListCount || currentSelectData.StateSettings[i].pitchList.Count != stateGruop.StateListCount)
                    {
                        //在新建和删除state时会执行同步audioComponent数据的操作，不应该会进入此步
                        Debug.LogError("[AudioEditor]: 发生错误");
                        currentSelectData.StateSettings[i].Reset(stateGruop);
                    }

                    FoldoutHeaderFlags[i] = EditorGUILayout.BeginFoldoutHeaderGroup(FoldoutHeaderFlags[i],
                        new GUIContent(stateGruop.name), menuAction: rect =>
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Delete"), false, (x) =>
                            {
                                DataRegisterToUndo();
                                currentSelectData.StateSettings.RemoveAt((int)x);
                                FoldoutHeaderFlags.Clear();
                                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                            }, i);
                            menu.DropDown(rect);
                        });
                    if (FoldoutHeaderFlags[i])
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
                            currentSelectData.StateSettings[i].volumeList[j] = EditorGUILayout.Slider(new GUIContent("Volume"),
                                currentSelectData.StateSettings[i].volumeList[j], 0, 1);
                            currentSelectData.StateSettings[i].pitchList[j] = EditorGUILayout.Slider(new GUIContent("Pitch"),
                                currentSelectData.StateSettings[i].pitchList[j], 0, 3);

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
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.General);
                Debug.Log("数据刷新 In States");
            }
        }

        private void DrawOtherSettingUnit(AEAudioComponent currentSelectData)
        {
            EditorGUI.BeginChangeCheck();
            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                EditorGUILayout.LabelField("Other Setting", BoldLabelStyleSkin.label);
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

            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                EditorGUILayout.LabelField(new GUIContent("Global Setting", "此部分选项为全局设置，与单一组件无关"), BoldLabelStyleSkin.label);
                AudioConfiguration config = AudioSettings.GetConfiguration();
                ManagerData.StaticallyLinkedAudioClips =
                    EditorGUILayout.ToggleLeft(new GUIContent("静态关联AudioClips", "如取消此选项，需调用加载AudioClip的委托方法"), ManagerData.StaticallyLinkedAudioClips);
                GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                var newRealVoiceNumer =
                    EditorGUILayout.IntSlider(new GUIContent("RealVoicesNumer", "游戏中当前可同时听到的最大声音数"),
                        config.numRealVoices, 1, 512);
                if (newRealVoiceNumer != config.numRealVoices)
                {
                    config.numRealVoices = newRealVoiceNumer;
                    AudioSettings.Reset(config);
                }
                EditorGUI.EndDisabledGroup();
            }

            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                EditorGUILayout.LabelField(new GUIContent("UI Setting", "此部分选项为编辑器UI的设定，与组件无关"), BoldLabelStyleSkin.label);
                AudioConfiguration config = AudioSettings.GetConfiguration();
                data.freshPageWhenTabInexChange =
                    EditorGUILayout.ToggleLeft(new GUIContent("当选择变更时是否刷新页面"), data.freshPageWhenTabInexChange);
                GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            }

            if (EditorGUI.EndChangeCheck())
            {
                manager.SyncPlayableData(currentSelectData.id,AudioComponentDataChangeType.OtherSetting);
                Debug.Log("数据刷新 In OtherSetting");
            }
        }

        #endregion

        #endregion

        #region 绘制右下角的预览界面

        private void DrawPreviewWindow()
        {
            if (manager.PlayableList.Count == 0)
            {
                previewState = PreviewModeState.Stoped;
            }

            if (data.ProjectExplorerTabIndex != (int)ProjectExplorerTab.Audio)
            {
                manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
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
                if (GUILayout.Button(IconUtility.GetIconContent(PreviewIconType.Stop_off, "Stop"), GUI.skin.button, buttonOption))
                {
                    previewState = PreviewModeState.Stoped;
                    manager.ApplyAudioComponentPreview(previewObject, AEEventType.StopAll);
                }

                var pauseIcontype = previewState == PreviewModeState.Paused ? PreviewIconType.Pause_on : PreviewIconType.Pause_off;
                if (GUILayout.Button(IconUtility.GetIconContent(pauseIcontype, "Pause"), GUI.skin.button, buttonOption))
                {
                    previewState = PreviewModeState.Paused;
                    manager.ApplyAudioComponentPreview(previewObject, AEEventType.Pause, data.MyTreeViewStates[0].lastClickedID);
                }

                var playIconType = previewState == PreviewModeState.playing ? PreviewIconType.Play_on : PreviewIconType.Play_off;
                var playTipLabel = previewState == PreviewModeState.Paused ? "Resume" : "Play";
                if (GUILayout.Button(IconUtility.GetIconContent(playIconType, playTipLabel), GUI.skin.button, buttonOption))
                {
                    if (previewState == PreviewModeState.Paused)
                    {
                        manager.ApplyAudioComponentPreview(previewObject, AEEventType.Resume, data.MyTreeViewStates[0].lastClickedID);
                        previewState = PreviewModeState.playing;
                    }
                    else
                    {
                        if (data.ProjectExplorerTabIndex == (int)ProjectExplorerTab.Audio &&
                            AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(data.MyTreeViewStates[0].lastClickedID) != null)
                        {
                            manager.ApplyAudioComponentPreview(previewObject, AEEventType.Play, data.MyTreeViewStates[0].lastClickedID);
                        }
                        previewState = PreviewModeState.playing;
                    }
                }
                GUI.color = color;
                previewWindowGridSelected = GUILayout.SelectionGrid(previewWindowGridSelected, previewWindowGridButtonName, 2, GUILayout.Width(150), GUILayout.ExpandWidth(false));

                EditorGUILayout.EndHorizontal();
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), 0.2f, "");
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), 0.2f, "");
                EditorGUILayout.EndVertical();
            }

            GUI.color = backgroundBoxColor;
            using (new EditorGUILayout.VerticalScope("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                GUI.color = preGUIColor;
                if (data.ProjectExplorerTabIndex == (int)ProjectExplorerTab.Audio)
                {
                    var currentSelectID = data.MyTreeViewStates[data.ProjectExplorerTabIndex].lastClickedID;
                    if (AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(currentSelectID) is AEAudioComponent audiocomponentData)
                    {
                        //TODO：补全预览窗口的功能
                        switch (previewWindowGridSelected)
                        {
                            case 0:
                                //states
                                foreach (var stateSetting in audiocomponentData.StateSettings)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    var stateGroup =
                                        AudioEditorManager
                                            .GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId) as StateGruop;
                                    EditorGUILayout.PrefixLabel(stateGroup.name);
                                    var buttonRect = EditorGUILayout.GetControlRect();
                                    if (EditorGUI.DropdownButton(buttonRect,
                                        new GUIContent(stateGroup.FindState(stateGroup.currentStateID).name),
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
                                break;
                            case 2:
                                //swithes
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
            Undo.RegisterCompleteObjectUndo(data, "AudioEditorWindow Change");
            Undo.RegisterCompleteObjectUndo(ManagerData, "AudioEditorWindow Change");
            Undo.IncrementCurrentGroup();
            //Debug.Log("DataRegisterToUndo.");
        }


        private void CheckGUIDAndPathWhenClipLoad(SoundSFX soundSFX)
        {
            var path = AssetDatabase.GetAssetPath(soundSFX.clip);
            var guid= AssetDatabase.AssetPathToGUID(path);
            if (path != soundSFX.clipAssetPath || guid != soundSFX.clipGUID)
            {
                soundSFX.clipAssetPath = path;
                soundSFX.clipGUID = guid;
            }
        }
        /// <summary>
        /// 将当前页面的数据进行深度复制
        /// </summary>
        /// <param name="resourceData"></param>
        /// <param name="targetIDs"></param>
        private void DeepCopypropertyEditorTabPageData(AEAudioComponent resourceData, HashSet<int> targetIDs)
        {
            foreach (var targetID in targetIDs)
            {
                var targetData = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(targetID) as AEAudioComponent;
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
                        Debug.Log("未完成");
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
                            var newData = new GameParameterCurveSetting(curveSetting.targetType);
                            newData.curveData.keys = curveSetting.curveData.keys;
                            newData.gameParameterID = curveSetting.gameParameterID;
                            targetData.gameParameterCurveSettings.Add(newData);
                        });
                        break;
                    case PropertyEditorTab.States:
                        Debug.LogError("未完成");
                        break;
                    case PropertyEditorTab.OtherSetting:
                        targetData.unloadClipWhenPlayEnd = resourceData.unloadClipWhenPlayEnd;
                        targetData.priority = resourceData.priority;
                        break;
                }
                manager.SyncPlayableData(targetData.id,AudioComponentDataChangeType.General);
            }
        }

        /// <summary>
        /// 水平分隔栏的位置
        /// </summary>
        float CurrentHorizontalSpliterHeight
        {
            get { return data._currentHorizontalSpliterHeight; }
            set
            {
                if (value < 60)
                {
                    data._currentHorizontalSpliterHeight = 50;
                    return; ;
                }

                if (value > position.height - 60)
                {
                    data._currentHorizontalSpliterHeight = position.height - 50;
                    return;
                }
                data._currentHorizontalSpliterHeight = value;
            }
        }
        /// <summary>
        /// 竖直分隔栏的位置
        /// </summary>
        public float CurrentVerticalSpliterWidth
        {
            get { return data._currentVerticalSpliterWidth; }
            set
            {
                if (value < 60)
                {
                    data._currentVerticalSpliterWidth = 50;
                    return;
                }

                if (value > position.width - 60)
                {
                    data._currentVerticalSpliterWidth = position.width - 50;
                    return;
                }
                data._currentVerticalSpliterWidth = value;
            }
        }

        private AudioEditorData ManagerData
        {
            get => AudioEditorManager.Data;
            set => AudioEditorManager.Data = value;
        }

        public static readonly Color[] ColorRankList =
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

        // public static readonly Dictionary<string,Color> ColorRank = new Dictionary<string,Color>
        // {
        //     {"Volume",Color.red},
        //     {"Pitch",Color.blue}
        // };
    }

}