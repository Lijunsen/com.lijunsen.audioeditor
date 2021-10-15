using AudioEditor.Runtime.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace AudioEditor.Runtime
{
    
    [ExecuteInEditMode, AddComponentMenu("AudioEditor/AudioEditorManager")]
    public class AudioEditorManager : MonoBehaviour
    {
        #region 参数设置
        internal static bool debugMode = false;

        private static AudioEditorManager instance;

        /// <summary>
        /// 用于读写asset文件中的数据,初始化时会将文件中的数据整合到此字段中，保存数据时会读取次字段值覆写入文件中
        /// </summary>
        private static AudioEditorData audioEditorData;

        [SerializeField]
        public bool autoLoadDataAsset = true;
        [SerializeField]
        public bool staticallyLinkedAudioClips = true;

        [SerializeField]
        internal string programPath = "Assets";

        //[SerializeField]
        //internal string testPath = "Assets/";
        
        //private static List<AudioEditorPlayable> playableList = new List<AudioEditorPlayable>();

        private static List<AudioPlayableInstance> playableInstanceList = new List<AudioPlayableInstance>();
        /// <summary>
        /// 储存不会被卸载的音频文件
        /// </summary>
        public static List<AudioClip> globalStaticClips = new List<AudioClip>();

        #region EventLogWindow相关
        internal static event Action<AEEvent, GameObject> EventTriggered;
        internal static event Action<AEEventType?, bool, string, int, string, GameObject> EventUnitTriggered;
        internal static event Action<string, int, GameObject> PlayableGenerated;
        internal static event Action<string, int, GameObject> PlayableDestroyed;
        #endregion

        internal static event Action<SwitchGroup, AEEventScope, GameObject> SwitchGroupSelectChanged;
        internal static event Action<int, int, int, AEEventScope,GameObject> StateGroupSelectChanged;
        internal static event Action<GameParameter, float> GameParameterChanged;
        /// <summary>
        /// 将编辑器中的修改操作同步到正在播放的Playable（包括子级的PlayableChildren）
        /// </summary>
        internal static event Action<int, AudioComponentDataChangeType, object> AudioComponentDataChanged;

        internal static event Action<float> OnUpdateAction; 
        
        public static event Func<string, AudioEditorData> LoadAudioEditorDataFunc;
        public static event Func<string, AudioClip> LoadAudioClipFunc;

        #endregion

        internal static AudioEditorData Data
        {
            get => audioEditorData;
            set => audioEditorData = value;
        }

        internal List<AudioPlayableInstance> PlayableInstanceList
        {
            get => playableInstanceList;
        }

        internal static string RuntimeDataFolder => GetProgramPath() + "/AudioEditor/Data/Resources";
#if UNITY_EDITOR
        internal static string EditorDataFolder => GetProgramPath() + "/AudioEditor/Data/EditorData";
        internal static string EditorViewDataPath => $"{EditorDataFolder}/AudioEditor_ViewData_{Environment.UserName}_{SystemInfo.deviceUniqueIdentifier}.asset";
        internal static string EditorTempFilePath => $"{EditorDataFolder}/AudioEditor_TempFile_{Environment.UserName}_{SystemInfo.deviceUniqueIdentifier}.asset";
#endif

        void Awake()
        {

        }
        
        void OnEnable()
        {
            //TODO 进入PlayMode时会OnEnable()两次，其原因暂时不明
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                if (instance != this)
                {
                    Debug.LogError("[AudioEditor]: 场景中已有Manager，请勿重复添加");
                    SafeDestroy(this);
                    return;
                }
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
#else
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
#endif

            playableInstanceList = new List<AudioPlayableInstance>();


            if (autoLoadDataAsset)
            {
                LoadDataAsset();

#if  UNITY_EDITOR
                //在游戏开始时不应该带入已经加载的音频
                if (UnityEditor.EditorApplication.isPlaying)
                {
                    UnloadAllAudioClip();
                }
#endif
            }

        }

        private void Start()
        {
            //编辑后unity刷新在Editor Mode下不会进入Start
        }

        private static void Init()
        {
            foreach (var item in Data.audioComponentData)
            {
                item.Init();
            }

            foreach (var item in Data.eventData)
            {
                item.Init();
            }

            foreach (var item in Data.gameSyncsData)
            {
                item.Init();
            }

        }


        void Update()
        {
            //检测Playable是否完成并进行销毁
            for (int i = 0; i < playableInstanceList.Count;)
            {
                if (playableInstanceList[i].IsDone() || (playableInstanceList[i].IsStopWhenGameObjectDestroy() && playableInstanceList[i].playableOutput.GetUserData() == null))
                {
                    DestroyPlayable(i);
                }
                else
                {
                    i++;
                }
            }

            OnUpdateAction?.Invoke(Time.deltaTime);

            if (instance == null)
            {
                instance = this;
            }
        }

        void LateUpdate()
        {
            foreach (var playableInstance in playableInstanceList)
            {
                var attachObject = playableInstance.playableOutput.GetUserData() as GameObject;
                if (attachObject != null)
                {
                    playableInstance.audioSource.gameObject.transform.position = attachObject.transform.position;
                }
            }
        }

        void OnGUI()
        {
        }


        void OnDestroy()
        {
            //当前实例仅为自身的时候才进行卸载操作
            if (instance == this)
            {
                //销毁所有正在播放的playable
                DestroyPlayable(playableInstanceList);
                playableInstanceList.Clear();

                //  GC.Collect();
                
                //卸载所有的audioClip数据
                if (Data != null && staticallyLinkedAudioClips == false)
                {
                    UnlinkAllAudioClips();
                }

                //清除GlobalStaticClips中的音频文件,防止在Edior时依旧生效导致内存占用
                globalStaticClips.Clear();

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
        internal AEComponent GenerateManagerData(string name, int id, AEComponentType type, int? parentID = null)
        {
            switch (type)
            {
                case AEComponentType.WorkUnit:
                    return null;
                //AudioComponent
                case AEComponentType.ActorMixer:
                    var newActorMixer = new ActorMixer(name, id, type);
                    Data.audioComponentData.Add(newActorMixer);
                    return newActorMixer;

                case AEComponentType.SoundSFX:
                    var newSoundSFX = new SoundSFX(name, id, type);
                    Data.audioComponentData.Add(newSoundSFX);
                    return newSoundSFX;

                case AEComponentType.RandomContainer:
                    var newRandomContainer = new RandomContainer(name, id, type);
                    Data.audioComponentData.Add(newRandomContainer);
                    return newRandomContainer;

                case AEComponentType.SequenceContainer:
                    var newSequenceContainer = new SequenceContainer(name, id, type);
                    Data.audioComponentData.Add(newSequenceContainer);
                    return newSequenceContainer;

                case AEComponentType.SwitchContainer:
                    var newSwitchContainer = new SwitchContainer(name, id, type);
                    Data.audioComponentData.Add(newSwitchContainer);
                    return newSwitchContainer;

                case AEComponentType.BlendContainer:
                    var newBlendContainer = new BlendContainer(name, id, type);
                    Data.audioComponentData.Add(newBlendContainer);
                    return newBlendContainer;

                //Event
                case AEComponentType.Event:
                    var newEvent = new AEEvent(name, id);
                    Data.eventData.Add(newEvent);
                    return newEvent;

                //GameSync
                case AEComponentType.SwitchGroup:
                    var newSwitchGroup = new SwitchGroup(name, id);
                    Data.gameSyncsData.Add(newSwitchGroup);
                    return newSwitchGroup;

                case AEComponentType.Switch:
                    Switch newSwitch = null;
                    if (parentID == null)
                    {
                        Debug.LogError("未传入SwtichGoupParentID");
                        return null;
                    }
                    if (Data.gameSyncsData.Find(x => x.id == parentID) is SwitchGroup switchGroup)
                    {
                        newSwitch = switchGroup.GenerateSwitch(name, id);

                        foreach (var audioComponent in Data.audioComponentData)
                        {
                            if (audioComponent is SwitchContainer switchContainer &&
                                switchContainer.switchGroupID == parentID)
                            {
                                switchContainer.outputIDList.Add(-1);
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
                    Data.gameSyncsData.Add(newStateGroup);

                    return newStateGroup;

                case AEComponentType.State:
                    State newState = null;
                    if (parentID == null)
                    {
                        Debug.LogError("未传入SwtichGoupParentID");
                        return null;
                    }
                    if (Data.gameSyncsData.Find(x => x.id == parentID) is StateGruop stateGroup)
                    {
                        newState = stateGroup.GenerateState(name, id, name == "None" ? true : false);

                        foreach (var audioComponent in Data.audioComponentData)
                        {
                            var relateSettings =
                                audioComponent.stateSettings.FindAll((x) => x.stateGroupId == stateGroup.id);
                            foreach (var stateSetting in relateSettings)
                            {
                                stateSetting.volumeList.Add(1);
                                stateSetting.pitchList.Add(1);
                                AudioComponentDataChanged?.Invoke(audioComponent.id, AudioComponentDataChangeType.General, null);
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
                    Data.gameSyncsData.Add(newGameParameter);
                    return newGameParameter;
                    
                default:
                    Debug.Log("发生逻辑错误，请检查");
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        /// <summary>
        /// 获取数据在数组中的序列
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        [Obsolete]
        internal static int GetDataElementIndexByID<T>(int id) where T : AEComponent
        {

            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                return Data.audioComponentData.FindIndex(x => x.id == id);
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                return Data.eventData.FindIndex(x => x.id == id);
            }

            if (typeof(AEGameSyncs).IsAssignableFrom(typeof(T)))
            {
                return Data.gameSyncsData.FindIndex(x => x.id == id);
            }
            return -1;
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        internal static T GetAEComponentDataByID<T>(int id) where T : AEComponent
        {
            if (Data == null)
            {
                AudioEditorDebugLog.LogWarning("配置文件未加载");
                return null;
            }
            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                var data = Data.audioComponentData.Find(x => x.id == id);
                if (data is T typeCorrectData)
                {
                    return typeCorrectData;
                }
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                var data = Data.eventData.Find(x => x.id == id);
                if (data is T typeCorrectData)
                {
                    return typeCorrectData;
                }
            }

            if (typeof(Switch).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.gameSyncsData)
                {
                    if (gameSyncsData is SwitchGroup switchGroup)
                    {
                        var getSwitch = switchGroup.GetSwitch(id);
                        if (getSwitch != null)
                        {
                            if (getSwitch is T typeCorrectSwitch)
                            {
                                return typeCorrectSwitch;
                            }
                        }
                    }
                }
                return null;
            }

            if (typeof(State).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.gameSyncsData)
                {
                    if (gameSyncsData is StateGruop stateGroup)
                    {
                        var getState = stateGroup.GetState(id);
                        if (getState != null)
                        {
                            if (getState is T typeCorrectState)
                            {
                                return typeCorrectState;
                            }
                        }
                    }
                }
                return null;
            }

            if (typeof(AEGameSyncs).IsAssignableFrom(typeof(T)))
            {
                var gameSync = Data.gameSyncsData.Find(x => x.id == id);
                if (gameSync is T typeCorrectData)
                {
                    return typeCorrectData;
                }
                //当T为AEGameSyncs时也应可以获取到Switch和State（为了层级上与AEAudioComponent结构相同），但不推荐这么做
                gameSync = (AEGameSyncs)GetAEComponentDataByID<Switch>(id);
                if (gameSync != null)
                {
                    if (gameSync is T typeCorrectGameSync)
                    {
                        return typeCorrectGameSync;
                    }
                }
                gameSync = (AEGameSyncs)GetAEComponentDataByID<State>(id);
                if (gameSync != null)
                {
                    if (gameSync is T typeCorrectGameSync)
                    {
                        return typeCorrectGameSync;
                    }
                }
            }
            return null;
        }

        internal static MyTreeElement GetTreeElementByID<T>(int id) where T : AEComponent
        {

            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                return Data.myTreeLists[0].Find(x=>x.id == id);
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                return Data.myTreeLists[1].Find(x => x.id == id);
            }

            if (typeof(AEGameSyncs).IsAssignableFrom(typeof(T)))
            {
                return Data.myTreeLists[2].Find(x => x.id == id);
            }
            
            AudioEditorDebugLog.LogError("无法获取相关Id的TreeElement");
            return null;
        }

        internal bool RemoveAEComponentDataByID<T>(int id) where T : AEComponent
        {

            if (typeof(AEAudioComponent).IsAssignableFrom(typeof(T)))
            {
                var index = Data.audioComponentData.FindIndex(x => x.id == id);
                if (index != -1)
                {
                    Data.audioComponentData.RemoveAt(index);
                    return true;
                }
            }

            if (typeof(AEEvent).IsAssignableFrom(typeof(T)))
            {
                var index = Data.eventData.FindIndex(x => x.id == id);
                if (index != -1)
                {
                    Data.eventData.RemoveAt(index);
                    return true;
                }
            }
            //Swtich和State并未储存在GameSyncsData列表中，而是在其父级Group中，所以删除算法有所不同，排在GameSync前面
            if (typeof(Switch).IsAssignableFrom(typeof(T)))
            {
                foreach (var gameSyncsData in Data.gameSyncsData)
                {
                    if (gameSyncsData is SwitchGroup switchGroup)
                    {
                        //删除相关SwitchContainer中OutputList中的相关内容
                        var switchIndex = switchGroup.GetSwitchIndex(id);
                        if (switchIndex != -1)
                        {
                            foreach (var audioComponent in Data.audioComponentData)
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
                foreach (var gameSyncsData in Data.gameSyncsData)
                {
                    if (gameSyncsData is StateGruop stateGroup)
                    {
                        var stateIndex = stateGroup.GetStateIndex(id);
                        if (stateIndex != -1)
                        {
                            //删除AudioComponent中相关的state设置
                            foreach (var audioComponent in Data.audioComponentData)
                            {
                                var stateSetting =
                                    audioComponent.stateSettings.Find(x => x.stateGroupId == stateGroup.id);
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
                var index = Data.gameSyncsData.FindIndex(x => x.id == id);

                if (index != -1)
                {
                    //检测AudioComponent中是否有相关的数据，进行删除
                    if (Data.gameSyncsData[index] is StateGruop stateGruop)
                    {
                        foreach (var audioComponent in Data.audioComponentData)
                        {
                            var stateSettingIndex =
                                audioComponent.stateSettings.FindIndex(x => x.stateGroupId == stateGruop.id);
                            if (stateSettingIndex != -1)
                            {
                                audioComponent.stateSettings.RemoveAt(stateSettingIndex);
                            }
                        }
                    }

                    if (Data.gameSyncsData[index] is SwitchGroup switchGroup)
                    {
                        foreach (var audioComponent in Data.audioComponentData)
                        {
                            if (audioComponent is SwitchContainer switchContainer && switchContainer.switchGroupID == switchGroup.id)
                            {
                                switchContainer.switchGroupID = -1;
                            }
                        }
                    }

                    if (Data.gameSyncsData[index] is GameParameter gameParameter)
                    {
                        foreach (var audioComponent in audioEditorData.audioComponentData)
                        {
                            //同步删除所有组件中相关RTPC的内容（gameparameterId改为-1）
                            foreach (var gameParameterCurveSetting in audioComponent.gameParameterCurveSettings)
                            {
                                if (gameParameterCurveSetting.gameParameterId == gameParameter.id)
                                {
                                    gameParameterCurveSetting.gameParameterId = -1;
                                }
                            }

                            //同步删除BlendContainer中的内容
                            if (audioComponent is BlendContainer blendContainer)
                            {
                                foreach (var blendContainerTrack in blendContainer.blendContainerTrackList)
                                {
                                    if (blendContainerTrack.gameParameterId == gameParameter.id)
                                    {
                                        blendContainerTrack.gameParameterId = -1;
                                    }
                                }
                            }
                        }
                    }

                    Data.gameSyncsData.RemoveAt(index);
                    return true;
                }

                //Switch和State也算作是GameSyncs的一部分，当T为AEGameSyncs时也应该检索Switch和State的部分，以删除对应的数据
                if (RemoveAEComponentDataByID<Switch>(id) == true) return true;
                if (RemoveAEComponentDataByID<State>(id) == true) return true;
            }
            return false;
        }

        /// <summary>
        /// 遍历ChildrenIDList获取某个Container组件下的所有子类
        /// </summary>
        /// <param name="container"></param>
        /// <param name="componentList"></param>
        internal static void GetContainerAllChildren(IAEContainer container, ref HashSet<AEAudioComponent> componentList)
        {
            if (container.ChildrenID.Count == 0) return;
            foreach (var childrenId in container.ChildrenID)
            {
                var component = GetAEComponentDataByID<AEAudioComponent>(childrenId);
                if (component == null)
                {
                    AudioEditorDebugLog.LogError("发生数据错误");
                    continue;
                }
                if (component is IAEContainer childContianer)
                {
                    GetContainerAllChildren(childContianer, ref componentList);
                }
                componentList.Add(component);
            }

        }

        #endregion

        #region 事件处理相关

        internal static void ApplyEvent(GameObject eventAttachObject, string eventName, AEEventType? overrideEventType = null, Action completeCallBack = null)
        {
            if (Data == null)
            {
                AudioEditorDebugLog.LogWarning("数据未加载");
                return;
            }
            var eventList = Data.eventData.FindAll(x => x.name == eventName);
            if (eventList.Count > 1)
            {
                AudioEditorDebugLog.LogWarning($"发现有复数“{eventName}”事件，无法触发相关事件，请检查");
                return;
            }

            if (eventList.Count == 1)
            {
                ApplyEvent(eventAttachObject, eventList[0].id, overrideEventType, completeCallBack);
            }

            if (eventList.Count == 0)
            {
                AudioEditorDebugLog.LogWarning($"无法寻找到对应AudioEvent事件，请检查，传入事件为:“{eventName}”,GameObjectName:{eventAttachObject.name}");
                return;
            }
        }

        internal static void ApplyEvent(GameObject eventAttachObject, int eventId, AEEventType? overrideEventType = null, Action completeCallBack = null)
        {
            if (Data == null)
            {
                AudioEditorDebugLog.LogWarning("数据未加载");
                return;
            }
            var applyEvent = GetAEComponentDataByID<AEEvent>(eventId) as AEEvent;
            if (applyEvent == null)
            {
                AudioEditorDebugLog.LogWarning($"无法寻找到对应AudioEvent事件，请检查，传入事件id为:{eventId},GameObjectName:{eventAttachObject.name}");
                return;
            }

            EventTriggered?.Invoke(applyEvent, eventAttachObject);
            // Debug.Log($"AuioEvent触发,name:{applyEvent.name}，id:{id}");

            foreach (var eventUnit in applyEvent.eventList)
            {
                DoEventAction(eventAttachObject, eventUnit, applyEvent.name, overrideEventType, completeCallBack);
            }
        }

        /// <summary>
        /// 执行Event下的动作
        /// </summary>
        /// <param name="eventAttachObject">所链接的GameObject，用于实现判定范围等</param>
        /// <param name="eventUnit">执行动作的的数据</param>
        /// <param name="actionName">用于生成Graph和GameObject时的名字</param>
        /// <param name="overrideEventType">覆盖类型，只有Play，Paus，Resume，Stop可覆盖</param>
        /// <param name="completeCallBack"></param>
        private static void DoEventAction(GameObject eventAttachObject, AEEventUnit eventUnit, string actionName, AEEventType? overrideEventType = null, Action completeCallBack = null)
        {
            var isOverride = false;
            AEEventType? eventType = eventUnit.type;
            var scope = eventUnit.scope;
            if (overrideEventType != null)
            {
                //只有原本的事件为以下几种才能覆盖
                if (eventType == AEEventType.Play ||
                    eventType == AEEventType.Pause ||
                    eventType == AEEventType.Resume ||
                    eventType == AEEventType.Stop)
                {
                    //因为跨组件页设置的TargetID不同，因此不允许这么做
                    if (overrideEventType != AEEventType.SetSwitch || overrideEventType != AEEventType.SetState)
                    {
                        eventType = overrideEventType;
                        isOverride = true;
                        //出于谨慎性原则，无论原来事件设置如何，在执行覆盖事件操作时scope永远为gameObject
                        //不然可能会一个play事件其scope为global时，覆盖其行为会导致执行范围过大，产生不可预料的结果
                        scope = AEEventScope.GameObject;
                    }
                }
            }

            AEComponent triggerTarget = null;
            if (eventType == AEEventType.SetSwitch || eventType == AEEventType.SetState)
            {
                triggerTarget = GetAEComponentDataByID<AEGameSyncs>(eventUnit.targetID);
            }
            else
            {
                triggerTarget = GetAEComponentDataByID<AEAudioComponent>(eventUnit.targetID);
            }

            var name = triggerTarget?.name ?? "null";
            var id = triggerTarget?.id ?? -1;
            var targetType = triggerTarget == null ? "null" : triggerTarget.GetType().ToString();
            EventUnitTriggered?.Invoke(eventType, isOverride, name, id, targetType, eventAttachObject);

            switch (eventType)
            {
                case AEEventType.Play:
                    //Debug.Log("Play");
                    if (eventUnit.targetID == -1) break;
                    if (playableInstanceList.Count >= AudioSettings.GetConfiguration().numVirtualVoices) break;
                    if (eventUnit.SimulationProbability() == false) break;
                    var currentPlayExamples = playableInstanceList.FindAll(x => x.ID == eventUnit.targetID);
                    if (currentPlayExamples.Count > 0 &&
                        currentPlayExamples.Count >= currentPlayExamples[0].GetLimitPlayNumber())
                    {
                        break;
                    }

                    switch (eventUnit.triggerType)
                    {
                        case AEEventTriggerType.Instance:

                            var newPlayable = GeneratePlayableInstance(actionName, eventUnit.targetID,
                                eventAttachObject, completeCallBack);

                            if (!isOverride && eventUnit.FadeTime > 0)
                            {
                                newPlayable.Replay(eventUnit.FadeTime, eventUnit.fadeType);
                            }
                            break;
                        case AEEventTriggerType.Delay:

                            newPlayable = GeneratePlayableInstance(actionName, eventUnit.targetID,
                                eventAttachObject, completeCallBack);

                            newPlayable.Delay(eventUnit.triggerData, () =>
                            {
                                if (!isOverride && eventUnit.FadeTime > 0)
                                {
                                    newPlayable.Replay(eventUnit.FadeTime, eventUnit.fadeType);
                                }
                                else
                                {
                                    newPlayable.Replay();
                                }
                            });

                            break;
                        case AEEventTriggerType.Trigger:
                            List<AudioPlayableInstance> targetPlayableInstanceList;
                            switch (scope)
                            {
                                case AEEventScope.GameObject:
                                    targetPlayableInstanceList =
                                        playableInstanceList.FindAll(x => x.ID == Mathf.RoundToInt(eventUnit.triggerData) && x.playableOutput.GetUserData() == eventAttachObject);
                                    break;
                                case AEEventScope.Global:
                                    targetPlayableInstanceList =
                                        playableInstanceList.FindAll(x => x.ID == Mathf.RoundToInt(eventUnit.triggerData));
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            foreach (var targetPlayableInstance in targetPlayableInstanceList)
                            {
                                if (targetPlayableInstance.CompareType<EmptyPlayable>() == false)
                                {
                                    targetPlayableInstance.GetPlayableState(out var playTime, out var durationTime, out var isTheLastTimePlay);
                                    if (playTime >= 0 && playTime < durationTime)
                                    {
                                        var timeDifference = targetPlayableInstance.GetPlayableNextBeatsTime(eventUnit.triggerOccasion);

                                        newPlayable = GeneratePlayableInstance(actionName, eventUnit.targetID,
                                            eventAttachObject, completeCallBack);

                                        var playable = newPlayable;
                                        newPlayable.Delay(timeDifference, () =>
                                        {
                                            if (!isOverride && eventUnit.FadeTime > 0)
                                            {
                                                playable.Replay(eventUnit.FadeTime, eventUnit.fadeType);
                                            }
                                            else
                                            {
                                                playable.Replay();
                                            }
                                        });
                                    }
                                }
                            }
                            
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                case AEEventType.Pause:
                    //Debug.Log("Pause");
                    var tempPlayableInstanceList = playableInstanceList.FindAll(x => x.ID == eventUnit.targetID);
                    switch (scope)
                    {
                        case AEEventScope.GameObject:
                            //暂停同ID下同GameObject的相应实例
                            foreach (var playableInstance in tempPlayableInstanceList)
                            {
                                if (playableInstance.playableOutput.GetUserData() == eventAttachObject)
                                {
                                    if (!isOverride)
                                        playableInstance.Pause(eventUnit.FadeTime, eventUnit.fadeType);
                                    else
                                        playableInstance.Pause();
                                }
                            }
                            break;
                        case AEEventScope.Global:
                            //暂停所有同ID的实例
                            foreach (var playableInstance in tempPlayableInstanceList)
                            {
                                if (!isOverride)
                                    playableInstance.Pause(eventUnit.FadeTime, eventUnit.fadeType);
                                else
                                    playableInstance.Pause();
                            }
                            break;
                    }
                    break;
                case AEEventType.PauseAll:
                    switch (scope)
                    {
                        case AEEventScope.GameObject:
                            tempPlayableInstanceList = playableInstanceList.FindAll(x => x.playableOutput.GetUserData() == eventAttachObject);
                            foreach (var playableInstance in tempPlayableInstanceList)
                            {
                                if (!isOverride)
                                    playableInstance.Pause(eventUnit.FadeTime, eventUnit.fadeType);
                                else
                                    playableInstance.Pause();
                            }
                            break;
                        case AEEventScope.Global:
                            foreach (var playableInstance in playableInstanceList)
                            {
                                if (!isOverride)
                                    playableInstance.Pause(eventUnit.FadeTime, eventUnit.fadeType);
                                else
                                    playableInstance.Pause();
                            }
                            break;
                    }
                    break;
                case AEEventType.Resume:
                    //Debug.Log("Resume");
                    tempPlayableInstanceList = playableInstanceList.FindAll(x => x.ID == eventUnit.targetID);
                    switch (scope)
                    {
                        case AEEventScope.GameObject:
                            //恢复同ID同GameObject下的实例
                            foreach (var playableInstance in tempPlayableInstanceList)
                            {
                                if (playableInstance.playableOutput.GetUserData() == eventAttachObject)
                                {
                                    if (!isOverride)
                                        playableInstance.Resume(eventUnit.FadeTime, eventUnit.fadeType);
                                    else
                                        playableInstance.Resume();
                                }
                            }
                            break;
                        case AEEventScope.Global:
                            //恢复所有同ID的实例
                            foreach (var playableInstance in tempPlayableInstanceList)
                            {
                                if (!isOverride)
                                    playableInstance.Resume(eventUnit.FadeTime, eventUnit.fadeType);
                                else
                                    playableInstance.Resume();
                            }
                            break;
                    }
                    break;
                case AEEventType.ResumeAll:
                    switch (scope)
                    {
                        case AEEventScope.GameObject:
                            tempPlayableInstanceList = playableInstanceList.FindAll(x => x.playableOutput.GetUserData() == eventAttachObject);
                            foreach (var playableInstance in tempPlayableInstanceList)
                            {
                                if (!isOverride)
                                    playableInstance.Resume(eventUnit.FadeTime, eventUnit.fadeType);
                                else
                                    playableInstance.Resume();
                            }
                            break;
                        case AEEventScope.Global:
                            foreach (var playableInstance in playableInstanceList)
                            {
                                if (!isOverride)
                                    playableInstance.Resume(eventUnit.FadeTime, eventUnit.fadeType);
                                else
                                    playableInstance.Resume();
                            }
                            break;
                    }
                    break;
                case AEEventType.Stop:
                    //根据ID停止传入的GameObject下的同ID实例
                    tempPlayableInstanceList = null;
                    switch (scope)
                    {
                        case AEEventScope.GameObject:
                            //仅停止同一GameObject上的同ID实例
                            tempPlayableInstanceList = playableInstanceList.FindAll(x =>
                                x.ID == eventUnit.targetID && x.playableOutput.GetUserData() == eventAttachObject);

                            break;
                        case AEEventScope.Global:
                            //停止所有的同ID实例
                            tempPlayableInstanceList = playableInstanceList.FindAll(x => x.ID == eventUnit.targetID);
                            break;
                    }

                    var time = 0f;
                    switch (eventUnit.triggerType)
                    {
                        case AEEventTriggerType.Instance:
                            time = 0f;
                            break;
                        case AEEventTriggerType.Delay:
                            time = eventUnit.triggerData;
                            break;
                        case AEEventTriggerType.Trigger:
                            var targetPlayableInstance = playableInstanceList.Find(x =>
                                x.ID == Mathf.RoundToInt(eventUnit.triggerData));
                            time = targetPlayableInstance.GetPlayableNextBeatsTime(eventUnit.triggerOccasion);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    AETimer.CreateNewTimer(time, () =>
                    {
                        foreach (var tempPlayableInstance in tempPlayableInstanceList)
                        {
                            if (!isOverride)
                            {
                                tempPlayableInstance.Stop(eventUnit.FadeTime, eventUnit.fadeType, () =>
                                {
                                    DestroyPlayable(tempPlayableInstance);
                                });
                            }
                            else
                            {
                                DestroyPlayable(tempPlayableInstance);
                            }
                        }
                    });
                    break;
                case AEEventType.StopAll:
                    switch (scope)
                    {
                        case AEEventScope.GameObject:
                            //只停止传入的GameObject下的实例
                            tempPlayableInstanceList = playableInstanceList.FindAll(x =>
                                x.playableOutput.GetUserData() == eventAttachObject);
                            foreach (var tempPlayableInstance in tempPlayableInstanceList)
                            {
                                if (!isOverride)
                                {
                                    tempPlayableInstance.Stop(eventUnit.FadeTime, eventUnit.fadeType, () =>
                                    {
                                        DestroyPlayable(tempPlayableInstance);
                                    });
                                }
                                else
                                {
                                    DestroyPlayable(tempPlayableInstance);
                                }
                            }

                            break;
                        case AEEventScope.Global:
                            //停止所有实例

                            var tempPlayableList2 = playableInstanceList.ToArray();
                            foreach (var playableInstance in tempPlayableList2)
                            {
                                if (!isOverride)
                                {
                                    playableInstance.Stop(eventUnit.FadeTime, eventUnit.fadeType, () =>
                                    {
                                        DestroyPlayable(playableInstance);
                                    });
                                }
                                else
                                {
                                    DestroyPlayable(playableInstance);
                                }
                            }

                            break;
                    }
                    break;
                case AEEventType.SetSwitch:
                    foreach (var gameSyncsData in Data.gameSyncsData)
                    {
                        if (gameSyncsData is SwitchGroup switchGroup)
                        {
                            if (switchGroup.ContainsSwitch(eventUnit.targetID))
                            {
                                time = 0f;
                                switch (eventUnit.triggerType)
                                {
                                    case AEEventTriggerType.Instance:
                                        time = 0f;
                                        break;
                                    case AEEventTriggerType.Delay:
                                        time = eventUnit.triggerData;
                                        break;
                                    case AEEventTriggerType.Trigger:
                                        var targetPlayableInstance = playableInstanceList.Find(x =>
                                            x.ID == Mathf.RoundToInt(eventUnit.triggerData));
                                        time = targetPlayableInstance.GetPlayableNextBeatsTime(eventUnit.triggerOccasion);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }
                                AETimer.CreateNewTimer(time, () =>
                                {
                                    switchGroup.currentSwitchID = eventUnit.targetID;
                                    SwitchGroupSelectChanged?.Invoke(switchGroup, scope, eventAttachObject);
                                });
                                break;
                            }
                        }
                    }
                    break;
                case AEEventType.SetState:
                    foreach (var gameSyncsData in Data.gameSyncsData)
                    {
                        if (gameSyncsData is StateGruop stateGruop)
                        {
                            if (stateGruop.ContainsState(eventUnit.targetID))
                            {
                                time = 0f;
                                switch (eventUnit.triggerType)
                                {
                                    case AEEventTriggerType.Instance:
                                        time = 0f;
                                        break;
                                    case AEEventTriggerType.Delay:
                                        time = eventUnit.triggerData;
                                        break;
                                    case AEEventTriggerType.Trigger:
                                        var targetPlayableInstance = playableInstanceList.Find(x =>
                                            x.ID == Mathf.RoundToInt(eventUnit.triggerData));
                                        time = targetPlayableInstance.GetPlayableNextBeatsTime(eventUnit.triggerOccasion);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }

                                AETimer.CreateNewTimer(time, () =>
                                {
                                    var preStateId = stateGruop.currentStateID;
                                    stateGruop.currentStateID = eventUnit.targetID;
                                    StateGroupSelectChanged?.Invoke(stateGruop.id, preStateId, stateGruop.currentStateID,scope, eventAttachObject);
                                });
                                break;
                            }
                        }
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
                // case AEEventType.SetToPreviousStep:
                //     var data = GetAEComponentDataByID<SequenceContainer>(eventUnit.targetID);
                //     if (data != null)
                //     {
                //         data.SetToPlayPreviousChild();
                //     }
                //     break;
                case null:
                //TODO 应添加SetGameParameter选项，有scope的功能
                default:
                    Debug.LogError("[AudioEditor]：功能未完成");
                    break;
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
            playableGameObject.transform.parent = instance.gameObject.transform;
            playableGameObject.transform.position = eventAttachObject.transform.position;
            playableGameObject.hideFlags = debugMode ? HideFlags.DontSave : HideFlags.HideAndDontSave;
            playableGameObject.AddComponent<AudioSource>();
            return playableGameObject;
        }

        private static AudioEditorPlayable GeneratePlayable(GameObject playableObject, GameObject eventAttachObject, ref PlayableGraph graph, int id, bool isGenerateChild)
        {
            var audioComponent = GetAEComponentDataByID<AEAudioComponent>(id);
            var audioSource = isGenerateChild ? null : playableObject.GetComponent<AudioSource>();
            if (audioComponent == null)
            {
                var emptyPlayable = new EmptyPlayable(ref graph, eventAttachObject, audioSource);
                return emptyPlayable;
            }

            switch (audioComponent)
            {
                case SoundSFX soundSFX:
                    //在此加载AudioClip
                    if (LoadAuioClip(soundSFX) == false)
                    {
                        AudioEditorDebugLog.LogWarning("加载对应AudioClip失败");
                        var newEmptyPlayable = new EmptyPlayable(ref graph, eventAttachObject, audioSource, soundSFX.id);
                        return newEmptyPlayable;
                    }
                    var newSoundSFXPlayable = new SoundSFXPlayable(soundSFX, ref graph, eventAttachObject, audioSource, isGenerateChild);
                    return newSoundSFXPlayable;

                case RandomContainer randomContainer:
                    List<int> childrenIDList = randomContainer.ChildrenID;
                    List<AudioEditorPlayable> childrenPlayable = new List<AudioEditorPlayable>();
                    for (int i = 0; i < childrenIDList.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, childrenIDList[i], true));
                    }
                    var newRandomContainerPlayable = new RandomContainerPlayable(randomContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    return newRandomContainerPlayable;

                case SequenceContainer sequenceContainer:
                    childrenPlayable = new List<AudioEditorPlayable>();
                    childrenIDList = sequenceContainer.ChildrenID;
                    for (int i = 0; i < childrenIDList.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, childrenIDList[i], true));
                    }
                    var newSequenceContainerPlayable = new SequenceContainerPlayable(sequenceContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    return newSequenceContainerPlayable;

                case SwitchContainer switchContainer:
                    childrenPlayable = new List<AudioEditorPlayable>();
                    var outputIDList = switchContainer.GetSwitchContainerOutputIDList();
                    for (int i = 0; i < outputIDList.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, outputIDList[i], true));
                    }
                    var newSwitchContainerPlayable = new SwitchContainerPlayable(switchContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    return newSwitchContainerPlayable;

                case BlendContainer blendContainer:
                    childrenPlayable = new List<AudioEditorPlayable>();
                    for (int i = 0; i < blendContainer.ChildrenID.Count; i++)
                    {
                        childrenPlayable.Add(GeneratePlayable(playableObject, eventAttachObject, ref graph, blendContainer.ChildrenID[i], true));
                    }
                    var newBlendContainerPlayable = new BlendContainerPlayable(blendContainer, graph, eventAttachObject, audioSource, childrenPlayable, isGenerateChild);
                    return newBlendContainerPlayable;
                default:
                    AudioEditorDebugLog.LogError($"发生id无法匹配情况，请排查,当前类型为:{audioComponent.unitType},id为{id}");
                    var emptyPlayable = new EmptyPlayable(ref graph, eventAttachObject, audioSource);
                    return emptyPlayable;
            }
        }

        private static AudioEditorPlayable GeneratePlayableInstance(string actionName,int componentId, GameObject eventAttachObject, Action completeCallBack = null)
        {
            var graph = GeneratePlayableGraph(actionName + "_" + playableInstanceList.Count);
            var playableObject = InstantiatePlayableGameObject(eventAttachObject, actionName + "_" + playableInstanceList.Count);
            var newPlayable = GeneratePlayable(playableObject, eventAttachObject, ref graph, componentId, false);

            playableInstanceList.Add(new AudioPlayableInstance(newPlayable.id,ref graph,newPlayable,eventAttachObject,playableObject));

            newPlayable.OnPlayableDestroy += completeCallBack;
            PlayableGenerated?.Invoke(newPlayable.name, newPlayable.id, eventAttachObject);

            return newPlayable;
        }
        
        public static void SetGameParameter(int id, float newValue)
        {
            if (Data == null)
            {
                AudioEditorDebugLog.LogWarning("数据未加载");
                return;
            }
            foreach (var data in Data.gameSyncsData)
            {
                if (data is GameParameter gameParameterData)
                {
                    if (gameParameterData.id == id)
                    {
                        var preNormalizeValue = gameParameterData.ValueAtNormalized;
                        gameParameterData.Value = newValue;
                        GameParameterChanged?.Invoke(gameParameterData, preNormalizeValue);
                        return;
                    }
                }
            }
            AudioEditorDebugLog.LogWarning($"RTPC同步失败,传入GameParameterId为：“{id}”");
        }

        public static void SetGameParameter(string name, float newValue)
        {
            if (Data == null)
            {
                AudioEditorDebugLog.LogWarning("数据未加载");
                return;
            }

            var gameParameterList =
                Data.gameSyncsData.FindAll(x => x is GameParameter && x.name == name);
            if (gameParameterList.Count > 1)
            {
                AudioEditorDebugLog.LogWarning($"发现有复数GameParameter “{name}”，无法进行同步，请检查");
                return;
            }

            if (gameParameterList.Count == 1 && gameParameterList[0] is GameParameter gameParameter)
            {
                var preNormalizeValue = gameParameter.ValueAtNormalized;
                gameParameter.Value = newValue;
                GameParameterChanged?.Invoke(gameParameter, preNormalizeValue);
                return;
            }
            AudioEditorDebugLog.LogWarning($"RTPC同步失败,传入GameParameterName为：“{name}”");
        }

        /// <summary>
        /// 预览组件相关内容
        /// </summary>
        /// <param name="previewObject"></param>
        /// <param name="componentID"></param>
        /// <param name="type"></param>
        internal void ApplyAudioComponentPreview(GameObject previewObject, AEEventType type, int componentID = -1)
        {
            if (Data == null)
            {
                Debug.LogWarning("[AudioEditor]：数据未加载");
                return;
            }
            if (componentID == -1 && type != AEEventType.StopAll)
            {
                return;
            }
            if (type == AEEventType.Play && GetAEComponentDataByID<AEAudioComponent>(componentID).unitType ==
                AEComponentType.ActorMixer)
            {
                return;
            }

            //只有Play才会用到name来为生成的GameObject和Graph等命名
            var name = type == AEEventType.Play ? GetAEComponentDataByID<AEAudioComponent>(componentID).name : "";
            var eventUnit = new AEEventUnit
            {
                type = type,
                targetID = componentID
            };
            DoEventAction(previewObject, eventUnit, name);
        }

        /// <summary>
        /// 停止并销毁Playable实例组件
        /// </summary>
        /// <param name="indexInPlayableList">playable在List中的位置</param>
        private static void DestroyPlayable(int indexInPlayableList)
        {
            if (indexInPlayableList < 0 || indexInPlayableList >= playableInstanceList.Count) return;
            var playableInstance = playableInstanceList[indexInPlayableList];

            playableInstanceList.RemoveAt(indexInPlayableList);

            PlayableDestroyed?.Invoke(playableInstance.Name, playableInstance.ID, playableInstance.playableOutput.GetUserData() as GameObject);

            playableInstance.Stop();
            //卸载实例所用的音频文件
            if (playableInstance.Data != null && playableInstance.GetActualData().unloadClipWhenPlayEnd)
            {
                UnLoadAudioClip(playableInstance);
            }
            //卸载实例
            playableInstance.Destroy();
            //卸载实例的gameObject
            if (playableInstance.audioSource != null && playableInstance.audioSource.gameObject != null)
            {
                SafeDestroy(playableInstance.audioSource.gameObject);
            }
        }

        /// <summary>
        /// 停止并销毁Playable实例组件
        /// </summary>
        /// <param name="playable"></param>
        private static void DestroyPlayable(AudioPlayableInstance playableInstance)
        {
            if (playableInstanceList.Contains(playableInstance) == false)
            {
                Debug.LogError("[AudioEditor]：发生未知错误,发现未知Playable");
                return;
            }
            playableInstanceList.Remove(playableInstance);

            PlayableDestroyed?.Invoke(playableInstance.Name, playableInstance.ID, playableInstance.playableOutput.GetUserData() as GameObject);
            playableInstance.Stop();
            //卸载实例所用的音频文件
            if (playableInstance.Data != null && playableInstance.GetActualData(AEComponentDataOverrideType.OtherSetting).unloadClipWhenPlayEnd)
            {
                UnLoadAudioClip(playableInstance);
            }
            //卸载实例
            playableInstance.Destroy();
            
            //卸载实例的gameObject
            if (playableInstance.audioSource != null && playableInstance.audioSource.gameObject != null)
            {
                SafeDestroy(playableInstance.audioSource.gameObject);
            }
        }

        /// <summary>
        /// 停止并销毁Playable实例组件
        /// </summary>
        /// <param name="prepareToDestroyList"></param>
        private static void DestroyPlayable(List<AudioPlayableInstance> prepareToDestroyList)
        {
            if (prepareToDestroyList == playableInstanceList)
            {
                for (int i = 0; i < playableInstanceList.Count;)
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
            // if (soundSFX.clip != null)
            // {
            //     if (soundSFX.clip.loadType == AudioClipLoadType.Streaming ||
            //         soundSFX.clip.loadState == AudioDataLoadState.Loaded)
            //     {
            //         return true;
            //     }
            //
            //     return soundSFX.clip.LoadAudioData();
            // }

            if (soundSFX.clipAssetPath != "")
            {
                //优先使用明面的资源加载流程
                if (soundSFX.clip == null)
                {
                    soundSFX.clip = LoadAudioClipFunc?.Invoke(soundSFX.clipAssetPath);
                }

#if UNITY_EDITOR
                //在EditorMode模式下优先保证能获取到audioClip
                if (soundSFX.clip == null && UnityEditor.EditorApplication.isPlaying == false)
                {
                    soundSFX.clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(soundSFX.clipAssetPath);
                }
#endif
            }

            if (soundSFX.clip != null)
            {
                if (soundSFX.clip.loadType == AudioClipLoadType.Streaming ||
                    soundSFX.clip.loadState == AudioDataLoadState.Loaded)
                {
                    return true;
                }
                return soundSFX.clip.LoadAudioData();
            }
            return false;
        }

        /// <summary>
        /// 卸载某个Playable的ClipsData，如果其他Playable组件正在使用此Clip，则不卸载
        /// </summary>
        /// <param name="targetPlayableInstance"></param>
        private static void UnLoadAudioClip(AudioPlayableInstance targetPlayableInstance)
        {
            var clips = new List<AudioClip>();
            targetPlayableInstance.GetAudioClips(ref clips);
            var otherPlayableClips = new List<AudioClip>();
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] == null || clips[i].loadState != AudioDataLoadState.Loaded || clips[i].loadType == AudioClipLoadType.Streaming) continue;

                //检查clip是否在不卸载列表内
                bool clipPrepareToUnload = !globalStaticClips.Contains(clips[i]);
                //检查clip是否在被其他playable使用
                if (clipPrepareToUnload == true)
                {
                    foreach (var playableInstance in playableInstanceList)
                    {
                        if (playableInstance == targetPlayableInstance)
                        {
                            continue;
                        }
                        otherPlayableClips.Clear();
                        playableInstance.GetAudioClips(ref otherPlayableClips);
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
                AudioEditorDebugLog.LogWarning("数据未加载");
                return;
            }
            AudioEditorDebugLog.Log("UnloadAllAudioClip");
            var otherPlayableClips = new List<AudioClip>();
            for (int i = 0; i < Data.audioComponentData.Count; i++)
            {
                if (Data.audioComponentData[i] is SoundSFX soundSFXData)
                {
                    var clip = soundSFXData.clip;
                    if (clip == null || clip.loadState != AudioDataLoadState.Loaded || clip.loadType == AudioClipLoadType.Streaming) continue;
                    //检查clip是否在不卸载列表内
                    bool clipPrepareToUnload = !globalStaticClips.Contains(clip);
                    //检查clip是否在被其他playable使用
                    if (clipPrepareToUnload == true)
                    {
                        foreach (var playable in playableInstanceList)
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

        /// <summary>
        /// 取消链接所有SoundSFX中的AudioClip
        /// </summary>
        internal void UnlinkAllAudioClips()
        {
            if (Data != null && staticallyLinkedAudioClips == false)
            {
                for (int i = 0; i < Data.audioComponentData.Count; i++)
                {
                    if (Data.audioComponentData[i] is SoundSFX soundSFXData)
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
            return instance;
        }

        public static void LoadDataAsset(bool enforce = false)
        {
            if (enforce == false && Data != null)
            {
                return;
            }

            //加载project中的assets文件
            AudioEditorDebugLog.Log("LoadDataAsset");
#if UNITY_EDITOR

            if (System.IO.File.Exists(EditorTempFilePath))
            {
                Debug.Log("LoadTempFile");
                Data = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioEditorData>(EditorTempFilePath);
            }
#endif
            if (Data == null)
            {
                var fileDataList = new List<AudioEditorData>();

                //将所有文件的数据整合入audioEditorData字段中
                Data = ScriptableObject.CreateInstance<AudioEditorData>();
                var audioEditorDataTypeNames = Enum.GetNames(typeof(AudioEditorDataType));
                for (int i = 0; i < audioEditorDataTypeNames.Length; i++)
                {
                    LoadFileData($"{RuntimeDataFolder}/AudioEditorData_{audioEditorDataTypeNames[i]}/root.asset", ref fileDataList);
                }

            }
            
            Init();
        }

        /// <summary>
        /// 加载文件的数据
        /// </summary>
        /// <param name="filePath">文件路径，例如AudioEditor/Data/Resources/AudioEditorData_Audio/root.asset</param>
        /// <param name="audioEditorDataList"></param>
        private static void LoadFileData(string filePath, ref List<AudioEditorData> audioEditorDataList)
        {
            var fileData = LoadAudioEditorDataFunc?.Invoke(filePath);

            if (fileData == null)
            {

#if UNITY_EDITOR
                fileData = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioEditorData>(filePath);
#else
                if (filePath.Contains("Resources"))
                {
                    fileData = Resources.Load<AudioEditorData>(filePath.Split(new[] { "Resources/" }, StringSplitOptions.None).Last());
                }
#endif
                if (fileData != null)
                {
                    audioEditorDataList.Add(fileData);
                    if (debugMode)
                    {
                        Debug.Log("AddFileData:" + filePath);
                    }
                    //自身首位的不算
                    for (int i = 0; i < MyTreeLists.TreeListCount; i++)
                    {
                        switch ((AudioEditorDataType)i)
                        {
                            case AudioEditorDataType.Audio:
                                Data.audioComponentData.AddRange(fileData.audioComponentData);
                                break;
                            case AudioEditorDataType.Events:
                                Data.eventData.AddRange(fileData.eventData);
                                break;
                            case AudioEditorDataType.GameSyncs:
                                Data.gameSyncsData.AddRange(fileData.gameSyncsData);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }


                        if (fileData.myTreeLists[i].Count > 1)
                        {
                            for (int j = 0; j < fileData.myTreeLists[i].Count; j++)
                            {
                                if (Data.myTreeLists[i].Exists(x => x.id == fileData.myTreeLists[i][j].id) == false)
                                {
                                    Data.myTreeLists[i].Add(fileData.myTreeLists[i][j]);
                                }
                                //首位是自身，跳过检测
                                if (j == 0) continue;
                                if (fileData.myTreeLists[i][j].type == AEComponentType.WorkUnit)
                                {
                                    LoadFileData(filePath.Remove(filePath.LastIndexOf('/')) + "/" + fileData.myTreeLists[i][j].name + ".asset", ref audioEditorDataList);
                                }
                            }
                        }
                    }
                }
            }

            if (fileData == null)
            {
                AudioEditorDebugLog.LogError($"加载数据 {filePath.Split('/').Last()} 失败，请检查资源目录");
            }
        }


        /// <summary>
        /// 当编辑器中修改数据时将数据同步到正在播放的实例
        /// </summary>
        /// <param name="audioComponentId"></param>
        /// <param name="type"></param>
        /// <param name="data"></param>
        internal void SyncDataToPlayable(int audioComponentId, AudioComponentDataChangeType type, object data = null)
        {
            if (type == AudioComponentDataChangeType.OtherSetting)
            {
                //检测LimitPlayNumber是否更改（减小的情况需要停止多余的正在播放的实例）
                var componentData = GetAEComponentDataByID<AEAudioComponent>(audioComponentId);
                if (componentData == null) AudioEditorDebugLog.LogError("发生错误");
                else
                {
                    var playablesInstances = playableInstanceList.FindAll(x => x.ID == audioComponentId);
                    if (playablesInstances.Count > componentData.limitPlayNumber)
                    {
                        var playablesPrepareToStop = playablesInstances.GetRange(componentData.limitPlayNumber, playablesInstances.Count - componentData.limitPlayNumber);
                        foreach (var playableInstance in playablesPrepareToStop)
                        {
                            playableInstance.Stop();
                        }
                    }
                }
            }
            AudioComponentDataChanged?.Invoke(audioComponentId, type, data);
        }

        // public static void ReInstanatiatePlayable(int id)
        // {
        //     var targetPlayableList = playableList.FindAll(x => x.Relate(id));
        //     foreach (var playable in targetPlayableList)
        //     {
        //         ReInstanatiatePlayable(playable);
        //     }
        // }

        /// <summary>
        /// 重新生成指定的Playable，如果在某父级的ChildrenPlayable中，则重新生成与其相关的Playable
        /// </summary>
        /// <param name="playableInstance"></param>
        internal static void ReInstanatiatePlayable(AudioPlayableInstance playableInstance)
        {
            var targetPlayableInstanceList = playableInstanceList.FindAll(x => x.Relate(playableInstance.ID));
            foreach (var targetPlayableInstance in targetPlayableInstanceList)
            {
                var actionName = playableInstance.playableOutput.GetUserData().name;
                var eventUnit = new AEEventUnit(AEEventType.Play, playableInstance.ID);
                var playableEventAttachGameObject = (GameObject)playableInstance.playableOutput.GetUserData();
                DestroyPlayable(targetPlayableInstance);
                DoEventAction(playableEventAttachGameObject, eventUnit, actionName);
            }
        }

        internal static string GetProgramPath()
        {
            if (instance == null)
            {
                Debug.LogError("instance == null");
            }
            return instance.programPath;
        }

        internal static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
#else
            UnityEngine.Object.Destroy(obj);
#endif
        }
    }

    internal enum AudioComponentDataChangeType
    {
        General,
        AudioClipChange,
        ContainerSettingChange,
        ContainerChildrenChange,
        SwitchContainerGroupSettingChange,
        LoopIndexChange,
        AddEffect,
        DeleteEffect,
        _3DSetting,
        OtherSetting
    }
}
