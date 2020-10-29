using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using ypzxAudioEditor.Utility;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ypzxAudioEditor
{
    [ExecuteInEditMode, AddComponentMenu("AudioEditor/AudioEditorManager")]
    public class AudioEditorManager : MonoBehaviour
    {
        #region 参数设置
        //用于读写asset文件中的数据
        [SerializeField]
        private static AudioEditorData audioEditorData;

        private static AudioEditorManager Instance;
        public static readonly string DataPath = "Assets/AudioEditor/Data/Resources/AuioEditorDataAsset.asset";
        //public static PlayableGraph m_Graph;
        private static List<AudioEditorPlayable> playableList = new List<AudioEditorPlayable>();
        /// <summary>
        /// 储存不会被卸载的音频文件
        /// </summary>
        public static List<AudioClip> GlobalStaticClips = new List<AudioClip>();

        public static event System.Action<AEEvent,GameObject> EventTriggered;
        public static event System.Action<AEEventType?, bool, string,int,string,GameObject> EventUnitTriggered;
        public static event System.Action<string, int, GameObject> PlayableGenerated;
        public static event System.Action<string, int, GameObject> PlayableDestroyed; 
        public static event System.Action<int, int> SwitchGroupSelectChanged;
        public static event System.Action<int, int,int> StateGroupSelectChanged;
        public static event System.Action<int> SyncGameParameter;
        internal static event System.Action<int,AudioComponentDataChangeType,object> AudioComponentDataChanged; 

        public static event System.Func<string, AudioEditorData> LoadAudioEditorDataFunc;
        public static event System.Func<string, AudioClip> LoadAudioClipFunc;

        #endregion

        void Awake()
        {

        }

        void OnEnable()
        {
            // var result = Resources.FindObjectsOfTypeAll(typeof(AudioEditorManager));
            // if (result.Length > 1)
            // {
            //     Debug.Log(result.Length);
            //     Debug.LogError("场景中已有Manager，请勿重复添加");
            //     DestroyImmediate(this);
            //     return;
            // }
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                if (Instance != this)
                {
                    Debug.LogError("场景中已有Manager，请勿重复添加");
                    SafeDestroy(this);
                    return;
                }
            }

#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
#else
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
#endif

            //Init PlayableGraph
            // Debug.Log("Create PlayableGraph");
            // m_Graph = PlayableGraph.Create("AudioEditorPlayableGraph");
            // GraphVisualizerClient.Show(m_Graph);
            // m_Graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            // AudioPlayableOutput.Create(m_Graph, "default", null);

            playableList = new List<AudioEditorPlayable>();

            LoadDataAsset();
        }

        private void Start()
        {
            // Init();

            //DoTransition(0, 20, 10);
        }

        private static void Init()
        {
            foreach (var item in Data.AudioComponentData)
            {
                item.Init();
            }

            foreach (var item in Data.EventData)
            {
                item.Init();
            }

            foreach (var item in Data.GameSyncsData)
            {
                item.Init();
            }

        }


        void Update()
        {
            //检测Playable是否完成并进行销毁
            for (int i = 0; i < playableList.Count;)
            {
                if (playableList[i].IsDone() || (playableList[i].Data.stopWhenGameObjectDestroy && playableList[i].playableOutput.GetUserData() == null))
                {
                    DestroyPlayable(i);
                }
                else
                {
                    i++;
                }
                //  GC.Collect();
            }
        }

        void LateUpdate()
        {
            foreach (var playable in playableList)
            {
                var attachObject = playable.playableOutput.GetUserData() as GameObject;
                if (attachObject != null)
                {
                    playable.AS.gameObject.transform.position = attachObject.transform.position;
                }
            }
        }



        public void OnDestroy()
        {
            //当前实例仅为自身的时候才进行卸载操作
            if (Instance == this)
            {
                //销毁所有正在播放的playable
                foreach (var playable in playableList)
                {
                    playable.Destroy();
                    if (playable.AS != null && playable.AS.gameObject != null)
                    {
                        SafeDestroy(playable.AS.gameObject);
                    }
                }
                playableList.Clear();
                //  GC.Collect();

                //销毁playableGragh
                // if (m_Graph.IsValid())
                // {
                //     Debug.Log("GraphDestroy");
                //     m_Graph.Stop();
                //     m_Graph.Destroy();
                // }

                //卸载所有的audioClip数据
                if (Data && Data.StaticallyLinkedAudioClips == false)
                {
                    UnlinkAllAudioClips();
                }

                // Instance = null;
                // Data = null;
            }
        }

        #region 数据操作相关

        /// <summary>
        /// 生成在ManagerData中的对应数据
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="parentID"></param>
        /// <returns>新生成的数据</returns>
        public AEComponent GenerateManagerData(string name, int id, AEComponentType type, int? parentID = null)
        {
            switch (type)
            {
                //AudioComponent
                case AEComponentType.SoundSFX:
                    var newSoundSFX = new SoundSFX(name, id, type);
                    Data.AudioComponentData.Add(newSoundSFX);
                    return newSoundSFX;

                case AEComponentType.RandomContainer:
                    var newRandomContainer = new RandomContainer(name, id, type);
                    Data.AudioComponentData.Add(newRandomContainer);
                    return newRandomContainer;

                case AEComponentType.SequenceContainer:
                    var newSequenceContainer = new SequenceContainer(name, id, type);
                    Data.AudioComponentData.Add(newSequenceContainer);
                    return newSequenceContainer;

                case AEComponentType.SwitchContainer:
                    var newSwitchContainer = new SwitchContainer(name, id, type);
                    Data.AudioComponentData.Add(newSwitchContainer);
                    return newSwitchContainer;

                case AEComponentType.BlendContainer:
                    Debug.LogError("未完成");
                    break;

                //Event
                case AEComponentType.Event:
                    var newEvent = new AEEvent(name, id);
                    Data.EventData.Add(newEvent);
                    return newEvent;

                //GameSync
                case AEComponentType.SwitchGroup:
                    var newSwitchGroup = new SwitchGroup(name, id);
                    Data.GameSyncsData.Add(newSwitchGroup);
                    return newSwitchGroup;

                case AEComponentType.Switch:
                    Switch newSwitch = null;
                    if (parentID == null)
                    {
                        Debug.LogError("未传入SwtichGoupParentID");
                        return null;
                    }
                    if (Data.GameSyncsData.Find(x => x.id == parentID) is SwitchGroup switchGroup)
                    {
                        newSwitch = switchGroup.GenerateSwitch(name, id);

                        foreach (var audioComponent in Data.AudioComponentData)
                        {
                            if (audioComponent is SwitchContainer switchContainer &&
                                switchContainer.switchGroupID == parentID)
                            {
                                switchContainer.outputIDList.Add(-1);
                                //TODO: 是否需要在此加上AudioComponentDataChanged?.invoke()
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("发生错误");
                    }
                    return newSwitch;

                case AEComponentType.StateGroup:
                    var newStateGroup = new StateGruop(name, id);
                    Data.GameSyncsData.Add(newStateGroup);

                    return newStateGroup;

                case AEComponentType.State:
                    State newState = null;
                    if (parentID == null)
                    {
                        Debug.LogError("未传入SwtichGoupParentID");
                        return null;
                    }
                    if (Data.GameSyncsData.Find(x => x.id == parentID) is StateGruop stateGroup)
                    {
                        newState = stateGroup.GenerateState(name, id, name == "None" ? true : false);

                        foreach (var audioComponent in Data.AudioComponentData)
                        {
                            var relateSettings =
                                audioComponent.StateSettings.FindAll((x) => x.stateGroupId == stateGroup.id);
                            foreach (var stateSetting in relateSettings)
                            {
                                stateSetting.volumeList.Add(1);
                                stateSetting.pitchList.Add(1);
                                AudioComponentDataChanged?.Invoke(audioComponent.id,AudioComponentDataChangeType.General,null);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("发生错误");
                    }
                    return newState;

                case AEComponentType.GameParameter:
                    var newGameParameter = new GameParameter(name, id);
                    Data.GameSyncsData.Add(newGameParameter);
                    return newGameParameter;

                default:
                    Debug.Log("发生逻辑错误，请检查");
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            return null;
        }

        /// <summary>
        /// 获取数据在数组中的序列
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        [Obsolete]
        public static int GetDataElementIndexByID<T>(int id) where T : AEComponent
        {

            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                return Data.AudioComponentData.FindIndex(x => x.id == id);
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                return Data.EventData.FindIndex(x => x.id == id);
            }

            if (typeof(AEGameSyncs).IsAssignableFrom(typeof(T)))
            {
                return Data.GameSyncsData.FindIndex(x => x.id == id);
            }
            return -1;
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public static AEComponent GetAEComponentDataByID<T>(int id) where T : AEComponent
        {
            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                var data = Data.AudioComponentData.Find(x => x.id == id);
                if (data is T typeCorrectData)
                {
                    return typeCorrectData;
                }
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                var data = Data.EventData.Find(x => x.id == id);
                if (data is T typeCorrectData)
                {
                    return typeCorrectData;
                }
            }

            if (typeof(Switch).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.GameSyncsData)
                {
                    if (gameSyncsData is SwitchGroup switchGroup)
                    {
                        var getSwitch = switchGroup.FindSwitch(id);
                        if (getSwitch != null)
                        {
                            return getSwitch;
                        }
                    }
                }
                return null;
            }

            if (typeof(State).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.GameSyncsData)
                {
                    if (gameSyncsData is StateGruop stateGroup)
                    {
                        var getState = stateGroup.FindState(id);
                        if (getState != null)
                        {
                            return getState;
                        }
                    }
                }
                return null;
            }

            if (typeof(AEGameSyncs).IsAssignableFrom(typeof(T)))
            {
                var gameSync = Data.GameSyncsData.Find(x => x.id == id);
                if (gameSync is T typeCorrectData)
                {
                    return typeCorrectData;
                }
                //当T为AEGameSyncs时也应可以获取到Switch和State（为了层级上与AEAudioComponent结构相同），但不推荐这么做
                gameSync = (AEGameSyncs)GetAEComponentDataByID<Switch>(id);
                if (gameSync != null)
                {
                    return gameSync;
                }
                gameSync = (AEGameSyncs)GetAEComponentDataByID<State>(id);
                if (gameSync != null)
                {
                    return gameSync;
                }
            }
            return null;
        }

        public bool RemoveAEComponentDataByID<T>(int id) where T : AEComponent
        {

            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                var index = Data.AudioComponentData.FindIndex(x => x.id == id);
                if (index != -1)
                {
                    Data.AudioComponentData.RemoveAt(index);
                    return true;
                }
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                var index = Data.EventData.FindIndex(x => x.id == id);
                if (index != -1)
                {
                    Data.EventData.RemoveAt(index);
                    return true;
                }
            }
            //Swtich和State并未储存在GameSyncsData列表中，而是在其父级Group中，所以删除算法有所不同，排在GameSync前面
            if (typeof(Switch).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.GameSyncsData)
                {
                    if (gameSyncsData is SwitchGroup switchGroup)
                    {
                        //删除相关SwitchContainer中OutputList中的相关内容
                        var switchIndex = switchGroup.FindSwitchIndex(id);
                        if (switchIndex != -1)
                        {
                            foreach (var audioComponent in Data.AudioComponentData)
                            {
                                if (audioComponent is SwitchContainer switchContainer)
                                {
                                    if (switchContainer.switchGroupID == switchGroup.id)
                                    {
                                        if (switchContainer.defualtPlayIndex == switchIndex)
                                        {
                                            switchContainer.defualtPlayIndex = 0;
                                        }
                                        switchContainer.outputIDList.RemoveAt(switchIndex);
                                    }
                                }
                            }
                            if (switchGroup.RemoveSwitch(id))
                            {
                                return true;
                            }
                            else
                            {
                                Debug.LogError("[AudioEditor]: 发生错误");
                            }
                        }
                    }
                }
                return false;
            }

            if (typeof(State).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.GameSyncsData)
                {
                    if (gameSyncsData is StateGruop stateGroup)
                    {
                        var stateIndex = stateGroup.FindStateIndex(id);
                        if (stateIndex != -1)
                        {
                            //删除AudioComponent中相关的state设置
                            foreach (var audioComponent in Data.AudioComponentData)
                            {
                                var stateSetting =
                                    audioComponent.StateSettings.Find(x => x.stateGroupId == stateGroup.id);
                                if (stateSetting != null)
                                {
                                    stateSetting.volumeList.RemoveAt(stateIndex);
                                    stateSetting.pitchList.RemoveAt(stateIndex);
                                }
                            }
                            if (stateGroup.RemoveState(id))
                            {
                                return true;
                            }
                            Debug.LogError("[AudioEditor]: 发生错误");
                        }
                    }
                }
                return false;
            }


            if (typeof(AEGameSyncs).IsAssignableFrom(typeof(T)))
            {
                var index = Data.GameSyncsData.FindIndex(x => x.id == id);

                if (index != -1)
                {
                    //检测AudioComponent中是否有相关的数据，进行删除
                    if (Data.GameSyncsData[index] is StateGruop stateGruop)
                    {
                        foreach (var audioComponent in Data.AudioComponentData)
                        {
                            var stateSettingIndex =
                                audioComponent.StateSettings.FindIndex(x => x.stateGroupId == stateGruop.id);
                            if (stateSettingIndex != -1)
                            {
                                audioComponent.StateSettings.RemoveAt(stateSettingIndex);
                            }
                        }
                    }

                    if (Data.GameSyncsData[index] is SwitchGroup switchGroup)
                    {
                        foreach (var audioComponent in Data.AudioComponentData)
                        {
                            if (audioComponent is SwitchContainer switchContainer && switchContainer.switchGroupID == switchGroup.id)
                            {
                                switchContainer.switchGroupID = -1;
                            }
                        }
                    }

                    Data.GameSyncsData.RemoveAt(index);
                    return true;
                }

                //Switch和State也算作是GameSyncs的一部分，当T为AEGameSyncs时也应该检索Switch和State的部分，以删除对应的数据
                if (RemoveAEComponentDataByID<Switch>(id) == true) return true;
                if (RemoveAEComponentDataByID<State>(id) == true) return true;
            }
            return false;
        }

        #endregion

        #region 事件处理相关

        public static void ApplyEvent(GameObject eventAttachObject, int eventID, AEEventType? overrideEventType = null)
        {
            if (Data == null)
            {
                Debug.LogWarning("[AudioEditor]：数据未加载");
                return;
            }
            var applyEvent = GetAEComponentDataByID<AEEvent>(eventID) as AEEvent;
            if (applyEvent == null)
            {
                //Debug.LogWarning($"无法寻找到对应AudioEvent事件，请检查，传入事件id为:{id},GameObjectName:{eventAttachObject.name}");
                return;
            }

            EventTriggered?.Invoke(applyEvent, eventAttachObject);
            // Debug.Log($"AuioEvent触发,name:{applyEvent.name}，id:{id}");
            for (int i = 0; i < applyEvent.eventList.Count; i++)
            {
                var isOverride = false;
                AEEventType? eventType = applyEvent.eventList[i].type;
                if (overrideEventType != null)
                {
                    //只有原本的事件为以下几种才能覆盖
                    if (applyEvent.eventList[i].type == AEEventType.Play ||
                        applyEvent.eventList[i].type == AEEventType.Pause ||
                        applyEvent.eventList[i].type == AEEventType.Resume ||
                        applyEvent.eventList[i].type == AEEventType.Stop ||
                        applyEvent.eventList[i].type == AEEventType.StopAll)
                    {
                        eventType = overrideEventType;
                        isOverride = true;
                    }
                }

                AEComponent triggerTarget = null;
                if (eventType == AEEventType.SetSwitch || eventType == AEEventType.SetState)
                {
                    triggerTarget = GetAEComponentDataByID<AEGameSyncs>(applyEvent.eventList[i].targetID);
                }
                else
                {
                    triggerTarget = GetAEComponentDataByID<AEAudioComponent>(applyEvent.eventList[i].targetID);
                }

                var name = triggerTarget?.name ?? "null";
                var id = triggerTarget?.id ?? -1;
                EventUnitTriggered?.Invoke(eventType, isOverride, name, id, triggerTarget.GetType().ToString(), eventAttachObject);

                switch (eventType)
                {
                    case AEEventType.Play:
                        //Debug.Log("Play");
                        if (applyEvent.eventList[i].targetID == -1) break;
                        if (playableList.Count >= AudioSettings.GetConfiguration().numVirtualVoices) break;
                        if (applyEvent.eventList[i].SimulationProbability() == false) break;
                        var currentPlayExamples = playableList.FindAll(x => x.id == applyEvent.eventList[i].targetID);
                        if (currentPlayExamples.Count > 0 &&
                            currentPlayExamples.Count >= currentPlayExamples[0].Data.limitPlayNumber)
                        {
                            break;
                        }
                        //修改为生成子object后没有进行同事件优化
                        var graph = GeneratePlayableGraph(applyEvent.name + "_" + playableList.Count.ToString());
                        var playableObject = InstantiatePlayableGameObject(eventAttachObject, applyEvent.name + "_" + playableList.Count.ToString());
                        var newPlayable = GeneratePlayable(playableObject, eventAttachObject, ref graph, applyEvent.eventList[i].targetID, false);
                        PlayableGenerated?.Invoke(newPlayable.name, newPlayable.id, eventAttachObject);
                        break;
                    case AEEventType.Pause:
                        //Debug.Log("Pause");
                        var tempPlayableList = playableList.FindAll(x => x.id == applyEvent.eventList[i].targetID);
                        switch (applyEvent.eventList[i].scope)
                        {
                            case Scope.GameObject:
                                //暂停同ID下同GameObject的相应实例
                                foreach (var playable in tempPlayableList)
                                {
                                    if (playable.playableOutput.GetUserData() == eventAttachObject)
                                    {
                                        playable.Pause();
                                    }
                                }
                                break;
                            case Scope.Global:
                                //暂停所有同ID的实例
                                foreach (var playable in tempPlayableList)
                                {
                                    playable.Pause();
                                }
                                break;
                        }
                        break;
                    case AEEventType.PauseAll:
                        switch (applyEvent.eventList[i].scope)
                        {
                            case Scope.GameObject:
                                tempPlayableList = playableList.FindAll(x => x.playableOutput.GetUserData() == eventAttachObject);
                                foreach (var playable in tempPlayableList)
                                {
                                    playable.Pause();
                                }
                                break;
                            case Scope.Global:
                                foreach (var playable in playableList)
                                {
                                    playable.Pause();
                                }
                                break;
                        }
                        break;
                    case AEEventType.Resume:
                        //Debug.Log("Resume");
                        tempPlayableList = playableList.FindAll(x => x.id == applyEvent.eventList[i].targetID);
                        switch (applyEvent.eventList[i].scope)
                        {
                            case Scope.GameObject:
                                //恢复同ID同GameObject下的实例
                                foreach (var playable in tempPlayableList)
                                {
                                    if (playable.playableOutput.GetUserData() == eventAttachObject)
                                    {
                                        playable.Resume();
                                    }
                                }
                                break;
                            case Scope.Global:
                                //恢复所有同ID的实例
                                foreach (var playable in tempPlayableList)
                                {
                                    playable.Resume();
                                }
                                break;
                        }
                        break;
                    case AEEventType.ResumeAll:
                        switch (applyEvent.eventList[i].scope)
                        {
                            case Scope.GameObject:
                                tempPlayableList = playableList.FindAll(x => x.playableOutput.GetUserData() == eventAttachObject);
                                foreach (var playable in tempPlayableList)
                                {
                                    playable.Resume();
                                }
                                break;
                            case Scope.Global:
                                foreach (var playable in playableList)
                                {
                                    playable.Resume();
                                }
                                break;
                        }
                        break;
                    case AEEventType.Stop:
                        //根据ID停止传入的GameObject下的同ID实例
                        switch (applyEvent.eventList[i].scope)
                        {
                            case Scope.GameObject:
                                //仅停止同一GameObject上的同ID实例
                                tempPlayableList = playableList.FindAll(x => x.id == applyEvent.eventList[i].targetID);
                                for (int j = 0; j < tempPlayableList.Count; j++)
                                {
                                    if (tempPlayableList[j].playableOutput.GetUserData() == eventAttachObject)
                                    {
                                        DestroyPlayable(tempPlayableList[j]);
                                    }
                                }
                                break;
                            case Scope.Global:
                                //停止所有的同ID实例
                                tempPlayableList = playableList.FindAll(x => x.id == applyEvent.eventList[i].targetID);
                                DestroyPlayable(tempPlayableList);
                                break;
                        }
                        break;
                    case AEEventType.StopAll:
                        switch (applyEvent.eventList[i].scope)
                        {
                            case Scope.GameObject:
                                //只停止传入的GameObject下的实例
                                tempPlayableList = playableList.FindAll(x =>
                                    x.playableOutput.GetUserData() == eventAttachObject);
                                DestroyPlayable(tempPlayableList);
                                break;
                            case Scope.Global:
                                //停止所有实例
                                DestroyPlayable(playableList);
                                break;
                        }
                        break;
                    case AEEventType.InstallBank:
                        Debug.Log("InstallBank");
                        Debug.LogError("未完成");
                        break;
                    case AEEventType.UnInstallBank:
                        Debug.Log("UnInstallBank");
                        Debug.LogError("未完成");
                        break;
                    case AEEventType.SetSwitch:
                        foreach (var gameSyncsData in Data.GameSyncsData)
                        {
                            if (gameSyncsData is SwitchGroup switchGroup)
                            {
                                if (switchGroup.ContainsSwitch(applyEvent.eventList[i].targetID))
                                {
                                    switchGroup.currentSwitchID = applyEvent.eventList[i].targetID;
                                    SwitchGroupSelectChanged?.Invoke(switchGroup.id, switchGroup.currentSwitchID);
                                    break;
                                }
                            }
                        }
                        break;
                    case AEEventType.SetState:
                        foreach (var gameSyncsData in Data.GameSyncsData)
                        {
                            if (gameSyncsData is StateGruop stateGruop)
                            {
                                if (stateGruop.ContainsState(applyEvent.eventList[i].targetID))
                                {
                                    var preStateId = stateGruop.currentStateID;
                                    stateGruop.currentStateID = applyEvent.eventList[i].targetID;
                                    StateGroupSelectChanged?.Invoke(stateGruop.id, preStateId,stateGruop.currentStateID);
                                    break;
                                }
                            }
                        }
                        break;
                    case null:
                    default:
                        Debug.LogError("[AudioEditor]：功能未完成");
                        break;
                }
            }
        }

        private static PlayableGraph GeneratePlayableGraph(string name)
        {
            var graph = PlayableGraph.Create(name);
            //graph.Play();
            //GraphVisualizerClient.Show(graph);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            return graph;
        }

        private static GameObject InstantiatePlayableGameObject(GameObject eventAttachObject, string name)
        {
            var playableGameObject = new GameObject();
#if UNITY_EDITOR
            playableGameObject.name = name;
#endif
            playableGameObject.transform.parent = Instance.gameObject.transform;
            playableGameObject.transform.position = eventAttachObject.transform.position;
            playableGameObject.hideFlags = HideFlags.DontSave;
            playableGameObject.AddComponent<AudioSource>();
            return playableGameObject;
        }

        public static AudioEditorPlayable GeneratePlayable(GameObject playableObject, GameObject eventAttachObject, ref PlayableGraph graph, int id, bool isGenerateChild)
        {
            var audioComponent = GetAEComponentDataByID<AEAudioComponent>(id) as AEAudioComponent;
            var audioSource = isGenerateChild ? null : playableObject.GetComponent<AudioSource>();
            if (audioComponent == null)
            {
                var emptyPlayable = new EmptyPlayable(ref graph, eventAttachObject, audioSource);
                playableList.Add(emptyPlayable);
                return emptyPlayable;
            }

            switch (audioComponent.unitType)
            {
                case AEComponentType.SoundSFX:
                    var sound = audioComponent as SoundSFX;
                    //在此加载AudioClip
                    if (LoadAuioClip(sound) == false)
                    {
                        Debug.LogWarning("[AudioEditor]: 加载对应AudioClip失败");
                        var newEmptyPlayable = new EmptyPlayable(ref graph, eventAttachObject, audioSource);
                        if (!isGenerateChild)
                        {
                            playableList.Add(newEmptyPlayable);
                        }
                        return newEmptyPlayable;
                    }
                    var newSoundSFXPlayable = new SoundSFXPlayable(sound, ref graph, eventAttachObject, audioSource, isGenerateChild);
                    if (!isGenerateChild)
                    {
                        playableList.Add(newSoundSFXPlayable);
                    }
                    return newSoundSFXPlayable;

                case AEComponentType.RandomContainer:
                    var randomContainer = audioComponent as RandomContainer;
                    List<int> childrenIDList = randomContainer.ChildrenID;
                    List<AudioEditorPlayable> childrenPlayable = new List<AudioEditorPlayable>();
                    for (int i = 0; i < childrenIDList.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, childrenIDList[i], true));
                    }
                    var newRandomContainerPlayable = new RandomContainerPlayable(randomContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    if (!isGenerateChild)
                    {
                        playableList.Add(newRandomContainerPlayable);
                    }
                    return newRandomContainerPlayable;

                case AEComponentType.SequenceContainer:
                    var sequenceContainer = audioComponent as SequenceContainer;
                    childrenPlayable = new List<AudioEditorPlayable>();
                    childrenIDList = sequenceContainer.GetSequenceContainerChildrenIDList();
                    for (int i = 0; i < childrenIDList.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, childrenIDList[i], true));
                    }
                    var newSequenceContainerPlayable = new SequenceContainerPlayable(sequenceContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    if (!isGenerateChild)
                    {
                        playableList.Add(newSequenceContainerPlayable);
                    }
                    return newSequenceContainerPlayable;

                case AEComponentType.SwitchContainer:
                    var switchContainer = audioComponent as SwitchContainer;
                    childrenPlayable = new List<AudioEditorPlayable>();
                    childrenIDList = switchContainer.GetSwitchContainerChildrenIDList();
                    for (int i = 0; i < childrenIDList.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, childrenIDList[i], true));
                    }
                    var newSwitchContainerPlayable = new SwitchContainerPlayable(switchContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    if (!isGenerateChild)
                    {
                        playableList.Add(newSwitchContainerPlayable);
                    }
                    // Debug.LogError("未完成");
                    return newSwitchContainerPlayable;

                case AEComponentType.BlendContainer:
                    Debug.LogError("未完成");
                    return null;

                default:
                    Debug.LogError(string.Format("发生id无法匹配情况，请排查,当前类型为:{0},id为{1}", audioComponent.unitType, id));
                    var emptyPlayable = new EmptyPlayable(ref graph, eventAttachObject, audioSource);
                    if (!isGenerateChild)
                    {
                        playableList.Add(emptyPlayable);
                    }
                    return emptyPlayable;
            }
        }

        [Obsolete]
        private AudioSource GetAudioSource(GameObject gameObject, int id)
        {
            var audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                return audioSource;
            }
            for (int i = 0; i < playableList.Count; i++)
            {
                //如果与相同gameObject中播放的Playable的id相同，则返回相同的AudioSource（因为设置相同可以共用一个AS）
                if (playableList[i].AS.gameObject == gameObject && playableList[i].id == id)
                {
                    return playableList[i].AS;
                }
            }
            return gameObject.AddComponent<AudioSource>();
            //暂时没做销毁功能
        }

        public static void SetGameParameter(int id, float newValue)
        {
            if (Data == null)
            {
                Debug.LogWarning("[AudioEditor]：数据未加载");
                return;
            }
            foreach (var data in Data.GameSyncsData)
            {
                if (data is GameParameter gameParameterData)
                {
                    if (gameParameterData.id == id)
                    {
                        gameParameterData.Value = newValue;
                        SyncGameParameter?.Invoke(id);
                        return;
                    }
                }
            }
            Debug.LogWarning("RTPC匹配id失败");
        }

        /// <summary>
        /// 预览组件相关内容
        /// </summary>
        /// <param name="previewObject"></param>
        /// <param name="ComponentID"></param>
        /// <param name="type"></param>
        public void ApplyAudioComponentPreview(GameObject previewObject, AEEventType type, int ComponentID = -1)
        {
            if (Data == null)
            {
                Debug.LogWarning("[AudioEditor]：数据未加载");
                return;
            }
            if (ComponentID == -1 && type != AEEventType.StopAll)
            {
                return;
            }
            //TODO : 将预览触发的功能与ApplyEvent()整合成一个统一的入口
            switch (type)
            {
                case AEEventType.Play:
                    var name = GetAEComponentDataByID<AEAudioComponent>(ComponentID).name +"_"+ playableList.Count.ToString();
                    var graph = GeneratePlayableGraph(name);
                    //TODO：PreviewObject似乎已经没必要，或者将预览的组件同步到Listener下
                    var playableObject = InstantiatePlayableGameObject(previewObject, name);
                    var newPlayable =  GeneratePlayable(playableObject, previewObject, ref graph, ComponentID, false);
                    PlayableGenerated?.Invoke(newPlayable.name, newPlayable.id, previewObject);
                    //myObject.GetComponent<AudioSource>().bypassListenerEffects = true;
                    //playableObject.GetComponent<AudioSource>().spatialBlend = 0;
                    break;
                case AEEventType.Pause:
                    for (int j = 0; j < playableList.Count; j++)
                    {
                        if (playableList[j].id == ComponentID)
                        {
                            playableList[j].Pause();
                        }
                    }
                    break;
                case AEEventType.Resume:
                    for (int j = 0; j < playableList.Count; j++)
                    {
                        if (playableList[j].id == ComponentID)
                        {
                            playableList[j].Resume();
                        }
                    }
                    break;
                case AEEventType.StopAll:
                    var tempPlayableList = playableList.FindAll(x => x.playableOutput.GetUserData() == previewObject);
                    DestroyPlayable(tempPlayableList);
                    break;
                case AEEventType.SetSwitch:
                    break;
                case AEEventType.SetState:
                    foreach (var gameSyncsData in Data.GameSyncsData)
                    {
                        if (gameSyncsData is StateGruop stateGruop)
                        {
                            if (stateGruop.ContainsState(ComponentID))
                            {
                                var preStateId = stateGruop.currentStateID;
                                stateGruop.currentStateID = ComponentID;
                                StateGroupSelectChanged?.Invoke(stateGruop.id, preStateId,stateGruop.currentStateID);
                                break;
                            }
                        }
                    }
                    break;
                case AEEventType.PauseAll:
                case AEEventType.ResumeAll:
                case AEEventType.Stop:
                case AEEventType.InstallBank:
                case AEEventType.UnInstallBank:
                default:
                    Debug.LogError("[AudioEditor]: 预览组件发生错误");
                    break;
            }
        }
        /// <summary>
        /// 停止并销毁Playable实例组件
        /// </summary>
        /// <param name="indexInPlayableList">playable在List中的位置</param>
        private static void DestroyPlayable(int indexInPlayableList)
        {
            if (indexInPlayableList < 0 || indexInPlayableList >= playableList.Count) return;
            PlayableDestroyed?.Invoke(playableList[indexInPlayableList].name, playableList[indexInPlayableList].id, playableList[indexInPlayableList].playableOutput.GetUserData() as GameObject);
            playableList[indexInPlayableList].Stop();
            //卸载实例所用的音频文件
            if (playableList[indexInPlayableList].Data != null && playableList[indexInPlayableList].Data.unloadClipWhenPlayEnd)
            {
                UnLoadAudioClip(playableList[indexInPlayableList]);
            }
            //卸载实例
            playableList[indexInPlayableList].Destroy();
            //卸载实例的gameObject
            if (playableList[indexInPlayableList].AS != null && playableList[indexInPlayableList].AS.gameObject != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(playableList[indexInPlayableList].AS.gameObject);
#else
                Destroy(playableList[indexInPlayableList].AS.gameObject);
#endif
            }
            playableList.RemoveAt(indexInPlayableList);
        }
        /// <summary>
        /// 停止并销毁Playable实例组件
        /// </summary>
        /// <param name="playable"></param>
        private static void DestroyPlayable(AudioEditorPlayable playable)
        {
            if (playableList.Contains(playable) == false)
            {
                Debug.LogError("[AudioEditor]：发生未知错误,发现未知Playable");
                return;
            }
            PlayableDestroyed?.Invoke(playable.name, playable.id, playable.playableOutput.GetUserData() as GameObject);
            playable.Stop();
            //卸载实例所用的音频文件
            if (playable.Data != null && playable.Data.unloadClipWhenPlayEnd)
            {
                UnLoadAudioClip(playable);
            }
            //卸载实例
            playable.Destroy();
            //卸载实例的gameObject
            if (playable.AS != null && playable.AS.gameObject != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(playable.AS.gameObject);
#else
                Destroy(playable.AS.gameObject);
#endif
            }
            playableList.Remove(playable);
        }
        /// <summary>
        /// 停止并销毁Playable实例组件
        /// </summary>
        /// <param name="prepareToDestroyList"></param>
        private static void DestroyPlayable(List<AudioEditorPlayable> prepareToDestroyList)
        {
            if (prepareToDestroyList == playableList)
            {
                for (int i = 0; i < playableList.Count;)
                {
                    DestroyPlayable(i);
                }
            }
            else
            {
                for (int i = 0; i < prepareToDestroyList.Count; i++)
                {
                    DestroyPlayable(prepareToDestroyList[i]);
                }
            }
        }

        #endregion

        #region 资源操作相关

        private static bool LoadAuioClip(SoundSFX soundSFX)
        {
            if (soundSFX.clip != null)
            {
                if (soundSFX.clip.loadType == AudioClipLoadType.Streaming ||
                    soundSFX.clip.loadState == AudioDataLoadState.Loaded)
                {
                    return true;
                }
                return soundSFX.clip.LoadAudioData();
            }

            soundSFX.clip = LoadAudioClipFunc?.Invoke(soundSFX.clipAssetPath);
            if (soundSFX.clip != null)
            {
                soundSFX.clip.LoadAudioData();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 卸载某个Playable的ClipsData，如果其他Playable组件正在使用此Clip，则不卸载
        /// </summary>
        /// <param name="targetPlayable"></param>
        private static void UnLoadAudioClip(AudioEditorPlayable targetPlayable)
        {
            var clips = new List<AudioClip>();
            targetPlayable.GetAudioClips(ref clips);
            var otherPlayableClips = new List<AudioClip>();
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] == null || clips[i].loadState != AudioDataLoadState.Loaded || clips[i].loadType == AudioClipLoadType.Streaming) continue;

                //检查clip是否在不卸载列表内
                bool clipPrepareToUnload = !GlobalStaticClips.Contains(clips[i]);
                //检查clip是否在被其他playable使用
                if (clipPrepareToUnload == true)
                {
                    foreach (var playable in playableList)
                    {
                        if (playable == targetPlayable)
                        {
                            continue;
                        }
                        otherPlayableClips.Clear();
                        playable.GetAudioClips(ref otherPlayableClips);
                        if (otherPlayableClips.Contains(clips[i]))
                        {
                            clipPrepareToUnload = false;
                            break;
                        }
                    }
                }
                //卸载clip
                if (clipPrepareToUnload == true)
                {
                    clips[i].UnloadAudioData();
                }
            }
        }

        /// <summary>
        /// 卸载所有已经缓存了的AudioClip，如果有Playable组件正在使用此Clip，则不卸载
        /// </summary>
        public static void UnloadAllAudioClip()
        {
            if (Data == null)
            {
                Debug.LogWarning("[AudioEditor]：数据未加载");
                return;
            }
            Debug.Log("[AudioEditor]: UnloadAllAudioClip");
            var otherPlayableClips = new List<AudioClip>();
            for (int i = 0; i < Data.AudioComponentData.Count; i++)
            {
                if (Data.AudioComponentData[i] is SoundSFX soundSFXData)
                {
                    var clip = soundSFXData.clip;
                    if (clip == null || clip.loadState != AudioDataLoadState.Loaded || clip.loadType == AudioClipLoadType.Streaming) continue;
                    //检查clip是否在不卸载列表内
                    bool clipPrepareToUnload = !GlobalStaticClips.Contains(clip);
                    //检查clip是否在被其他playable使用
                    if (clipPrepareToUnload == true)
                    {
                        foreach (var playable in playableList)
                        {
                            otherPlayableClips.Clear();
                            playable.GetAudioClips(ref otherPlayableClips);
                            if (otherPlayableClips.Contains(clip))
                            {
                                clipPrepareToUnload = false;
                                break;
                            }
                        }
                    }
                    if (clipPrepareToUnload == true)
                    {
                        clip.UnloadAudioData();
                        //Debug.Log("[AudioEditor]: 卸载AuioClip:" + clip.name);
                    }
                }
            }
        }

        public static void UnlinkAllAudioClips()
        {
            if (Data != null && Data.StaticallyLinkedAudioClips == false)
            {
                for (int i = 0; i < Data.AudioComponentData.Count; i++)
                {
                    if (Data.AudioComponentData[i] is SoundSFX soundSFXData)
                    {
                        if (soundSFXData.clip && soundSFXData.clip.loadState == AudioDataLoadState.Loaded)
                        {
                            soundSFXData.clip.UnloadAudioData();
                        }
                        soundSFXData.clip = null;
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// 获取Scene中的Manager实例
        /// </summary>
        public static AudioEditorManager FindManager()
        {
            return Instance;
            // var result = Resources.FindObjectsOfTypeAll(typeof(AudioEditorManager));
            // if (result.Length != 0)
            // {
            //     var manager = (AudioEditorManager)result[0];
            //     LoadDataAsset();
            //     return manager;
            // }
            // else
            // {
            //     //Debug.LogError("场景中无法搜索到AudioEditorManager");
            //     return null;
            // }
        }

        public static void LoadDataAsset()
        {
            if (audioEditorData != null)
            {
                return;
            }
            //加载project中的assets文件

#if UNITY_EDITOR
            audioEditorData = AssetDatabase.LoadAssetAtPath<AudioEditorData>(DataPath);
#else
            audioEditorData = Resources.Load<AudioEditorData>("AuioEditorDataAsset");
#endif
            if (Data == null)
            {
                audioEditorData = LoadAudioEditorDataFunc?.Invoke(DataPath);
            }
            if (Data == null)
            {
                Debug.LogError("[AudioEditor]：加载数据失败，请检查资源目录");
            }
            else
            {
                Init();
            }
        }

        /// <summary>
        /// 当编辑器中修改数据时将数据同步到正在播放的实例
        /// </summary>
        /// <param name="audioComponentId"></param>
        /// <param name="type"></param>
        /// <param name="data"></param>
        public void SyncPlayableData(int audioComponentId,AudioComponentDataChangeType type,object data = null)
        {
            if (type == AudioComponentDataChangeType.AudioClipChange)
            {
                foreach (var audioEditorPlayable in playableList)
                {
                    if (audioEditorPlayable.id == audioComponentId && audioEditorPlayable is SoundSFXPlayable soundSFXPlayable)
                    {
                        soundSFXPlayable.ClipPlayable.SetClip((soundSFXPlayable.Data as SoundSFX).clip);
                        soundSFXPlayable.ClipPlayable.SetDuration((soundSFXPlayable.Data as SoundSFX).clip.length);
                        soundSFXPlayable.Replay();
                    }
                }
            }

            if (type == AudioComponentDataChangeType.OtherSetting)
            {
                //检测LimitPlayNumber是否更改（减小的情况需要停止多余的正在播放的实例）
                var componentData = GetAEComponentDataByID<AEAudioComponent>(audioComponentId) as AEAudioComponent;
                if (componentData == null) Debug.LogError("[AudioEditor]: 发生错误");
                else
                {
                    var playables = playableList.FindAll(x => x.id == audioComponentId);
                    if (playables.Count > componentData.limitPlayNumber)
                    {
                        var PlayablesPrepareToStop = playables.GetRange(componentData.limitPlayNumber, playables.Count - componentData.limitPlayNumber);
                        foreach (var playable in PlayablesPrepareToStop)
                        {
                            playable.Stop();
                        }
                    }
                }
            }
            AudioComponentDataChanged?.Invoke(audioComponentId,type,data);
        }

        public static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(obj);
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }

        public static AudioEditorData Data
        {
            get
            {
                return audioEditorData;
            }
            set => audioEditorData = value;
        }

        public List<AudioEditorPlayable> PlayableList
        {
            get => playableList;
        }
    }

    public enum AudioComponentDataChangeType
    {
        General,
        AudioClipChange,
        ContainerSettingChange,
        ContainerChildrenChange,
        LoopIndexChange,
        AddEffect,
        DeleteEffect,
        _3DSetting,
        OtherSetting
    }
}
