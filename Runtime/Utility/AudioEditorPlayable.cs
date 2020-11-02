using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace ypzxAudioEditor.Utility
{
    public abstract class AudioEditorPlayable
    {
        public readonly string name;
        //同步Audio层级中的ID
        public readonly int id;
        public AEComponentType type;
        //记录依附于gameObject中的AudioSource
        public AudioSource AS;
        public bool enableState = true; 

        /// <summary>
        /// int=>StateGroupId, float=>currentStateVolume
        /// </summary>
        protected Dictionary<int, float> currentStateVolumeDictionary;
        protected Dictionary<int, float> currentStatePitchDictionary;

        protected PlayableGraph m_Graph;
        public AudioPlayableOutput playableOutput;

        protected abstract AudioEditorPlayableBehaviour PlayableBehaviour { get; }
        protected abstract AudioMixerPlayable MixerPlayable { get; set; }

        /// <summary>
        ///   对应的数据，注意只能在PlayableBehaviour生成实例后使用使用，使用前请检测null
        /// </summary>
        public abstract AEAudioComponent Data { get; }

        public AudioEditorPlayable(string name, int id, AEComponentType type, ref PlayableGraph graph)
        {
            this.name = name;
            this.id = id;
            this.type = type;
            this.m_Graph = graph;
            currentStateVolumeDictionary = new Dictionary<int, float>();
            currentStatePitchDictionary = new Dictionary<int, float>();
        }

        /// <summary>
        /// 初始化数据，生成PlayableOutput并同步AudioSource和GameParameter的值
        /// </summary>
        protected void Init<T>(T value, GameObject eventAttachObject, AudioSource audioSource) where T : struct, IPlayable
        {

            //初始化State实例化的数值
            if (Data != null)
            {
                foreach (var stateSetting in Data.StateSettings)
                {
                    var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId) as StateGruop;
                    currentStateVolumeDictionary.Add(stateGroup.id, stateSetting.volumeList[stateGroup.GetCurrentStateIndex()]);
                    currentStatePitchDictionary.Add(stateGroup.id, stateSetting.pitchList[stateGroup.GetCurrentStateIndex()]);
                }

                SyncPlayableVolume();
                SyncPlayablePitch();

                AudioEditorManager.SyncGameParameter += SyncGameParameter;
                AudioEditorManager.StateGroupSelectChanged += SyncStateChanged;
                AudioEditorManager.AudioComponentDataChanged += SyncAudioComponentData;
            }

            //作为子组件时AudioSource传入为null
            this.AS = audioSource;
            if (AS != null)
            {
                if (Data != null)
                {
                    SyncAudioSource(audioSource);
                }

                playableOutput = AudioPlayableOutput.Create(m_Graph, name, AS);
                playableOutput.SetSourcePlayable(value);
                playableOutput.SetUserData(eventAttachObject);
                m_Graph.Play();
                //m_Graph.Evaluate();
                //Replay();
            }
        }

        // public ScriptPlayable<T> GetScriptPlayable<T>() where T : class, IPlayableBehaviour, new()
        // {
        //
        // }

        public virtual void Pause()
        {
            MixerPlayable.Pause();
        }

        public virtual void Resume()
        {
            MixerPlayable.Play();
        }

        public abstract void Stop();

        public virtual void Replay()
        {
            MixerPlayable.SetTime(0);
            MixerPlayable.SetDone(false);
            MixerPlayable.Play();
        }
        public abstract bool IsDone();


        public virtual void Destroy()
        {
            AudioEditorManager.SyncGameParameter -= SyncGameParameter;
            AudioEditorManager.StateGroupSelectChanged -= SyncStateChanged;
            AudioEditorManager.AudioComponentDataChanged -= SyncAudioComponentData;
        }

        public virtual double GetMixerTime()
        {
            return MixerPlayable.GetTime();
        }

        public virtual bool Relate(int id)
        {
            if (this.id == id)
            {
                return true;
            }
            return false;
        }

        protected abstract void SetScriptPlayableSpeed(double value);

        protected abstract void SetScriptPlayableWeight(float newValue);


        /// <summary>
        /// 若当前为最后一次播放，则获取正在播放的时间和总时间，否则参数返回-1
        /// </summary>
        public abstract void GetTheLastTimePlayState(ref double playTime, ref double durationTime);

        public abstract void GetAudioClips(ref List<AudioClip> clips);



        public static AudioMixerPlayable AddChildrenPlayableToMixer(AudioMixerPlayable mixer, List<AudioEditorPlayable> childrenPlayable)
        {
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                switch (childrenPlayable[i])
                {
                    case SoundSFXPlayable soundSFX:
                        mixer.AddInput(soundSFX.GetScriptPlayable(), 0, 1);
                        break;
                    case RandomContainerPlayable randomContainerPlayable:
                        mixer.AddInput(randomContainerPlayable.GetScriptPlayable(), 0, 1);
                        break;
                    case SequenceContainerPlayable sequenceContainerPlayable:
                        mixer.AddInput(sequenceContainerPlayable.GetScriptPlayable(), 0, 1);
                        break;
                    case SwitchContainerPlayable switchContainerPlayable:
                        mixer.AddInput(switchContainerPlayable.GetScriptPlayable(), 0, 1);
                        break;
                    case EmptyPlayable emptyPlayable:
                        mixer.AddInput(emptyPlayable.GetScriptPlayable(), 0, 1);
                        break;
                    default:
                        break;
                }
            }
            return mixer;
        }

        public void SyncAudioSource(AudioSource AS)
        {
            AS.playOnAwake = false;
            AS.outputAudioMixerGroup = Data.outputMixer;
            AS.mute = Data.mute;
            AS.bypassEffects = Data.bypassEffects;
            AS.loop = Data.loop;
            AS.priority = Data.priority;
            //音量实际修改为CustomPlayable的权重
            // AS.volume = Data.volume;
            //测试pitch对于playable系统无效，需要调用playable的speed
            // AS.pitch = Data.pitch;
            AS.panStereo = Data.panStereo;
            AS.spatialBlend = Data.spatialBlend;
            AS.reverbZoneMix = Data.reverbZoneMix;
            AS.dopplerLevel = Data.dopplerLevel;
            AS.spread = Data.spread;
            AS.minDistance = Data.MinDistance;
            AS.maxDistance = Data.MaxDistance;

            SyncEffect();

            foreach (var animationCurveSetting in Data.attenuationCurveSettings)
            {
                switch (animationCurveSetting.attenuationCurveType)
                {
                    case AttenuationCurveType.OutputVolume:
                        AS.rolloffMode = AudioRolloffMode.Custom;
                        AS.SetCustomCurve(AudioSourceCurveType.CustomRolloff, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.SpatialBlend:
                        AS.SetCustomCurve(AudioSourceCurveType.SpatialBlend, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.Spread:
                        AS.SetCustomCurve(AudioSourceCurveType.Spread, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.ReverbZoneMix:
                        AS.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, animationCurveSetting.curveData);
                        break;
                    case AttenuationCurveType.LowPass:
                        var lowPassFilter = AS.gameObject.GetComponent<AudioLowPassFilter>();
                        if (lowPassFilter == null)
                        {
                            lowPassFilter = AS.gameObject.AddComponent<AudioLowPassFilter>();
                        }
                        lowPassFilter.customCutoffCurve = animationCurveSetting.curveData;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// 同步编辑器中修改的数据
        /// </summary>
        /// <param name="id"></param>
        protected virtual void SyncAudioComponentData(int id,AudioComponentDataChangeType type,object data)
        {
            if (this.id != id) return;
            switch (type)
            {
                case AudioComponentDataChangeType.AudioClipChange:
                    //如果是container内的子类的音频文件发生替换？
                    if (this is SoundSFXPlayable soundSFXPlayable)
                    {
                        soundSFXPlayable.ClipPlayable.SetClip((Data as SoundSFX).clip);
                        soundSFXPlayable.ClipPlayable.SetDuration((Data as SoundSFX).clip.length);
                        Replay();
                    }
                    Debug.LogError("未完成");
                    break;
                case AudioComponentDataChangeType.ContainerSettingChange:
                    Debug.LogError("未完成");
                    break;
                case AudioComponentDataChangeType.LoopIndexChange:
                    //每当循环次数改变都更新实例的实际循环数
                    PlayableBehaviour.loopIndex = Data.loopTimes;
                    break;
                case AudioComponentDataChangeType.AddEffect:
                    SyncEffect();
                    break;
                case AudioComponentDataChangeType.DeleteEffect:
                    if (data == null || AS ==null)
                    {
                        Debug.LogError("[AudioEditor]: 未传递要删除的Effect类型");
                        break;
                    }
                    switch ((AEEffectType)data)
                    {
                        case AEEffectType.LowPassFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioLowPassFilter>());
                            break;
                        case AEEffectType.HighPassFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioHighPassFilter>());
                            break;
                        case AEEffectType.ReverbFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioReverbFilter>());
                            break;
                        case AEEffectType.ReverbZoneFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioReverbZone>());
                            break;
                        case AEEffectType.EchoFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioEchoFilter>());
                            break;
                        case AEEffectType.ChorusFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioChorusFilter>());
                            break;
                        case AEEffectType.DistortionFilter:
                            AudioEditorManager.SafeDestroy(AS.gameObject.GetComponent<AudioDistortionFilter>());
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(data), data, null);
                    }
                    break;
                case AudioComponentDataChangeType._3DSetting:
                case AudioComponentDataChangeType.OtherSetting:
                case AudioComponentDataChangeType.General:
                default:
                    if (AS != null)
                    {
                        SyncAudioSource(AS);
                    }
                    SyncStateDirectly();
                    SyncPlayableVolume();
                    SyncPlayablePitch();
                    break;
            }
        }

        protected virtual void SyncGameParameter(int gameParameterID)
        {
            var curveSettings = Data.gameParameterCurveSettings.FindAll(x => x.gameParameterID == gameParameterID);
            if (curveSettings.Count == 0) return;
            var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(gameParameterID) as GameParameter;
            if (gameParameter == null)
            {
                Debug.LogWarning("[AudioEditor]: RTPC未设置X轴属性，请检查，AudioComponentID：" + id + ",name：" + Data.name);
                return;
            }

            switch (curveSettings[0].targetType)
            {
                case GameParameterTargetType.Volume:
                    SyncPlayableVolume();
                    break;
                case GameParameterTargetType.Pitch:
                    SyncPlayablePitch();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected virtual void SyncPlayableVolume()
        {
            //GeneralSetting中的设置
            float resultNumber = Data.volume;

            //RTPC中的设置
            var volumeRTPCList =
                Data.gameParameterCurveSettings.FindAll(x => x.targetType == GameParameterTargetType.Volume);
            foreach (var volumeRTPC in volumeRTPCList)
            {
                var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(volumeRTPC.gameParameterID) as GameParameter;
                if (gameParameter == null)
                {
                    Debug.LogWarning("RTPC未设置X轴属性，请检查，AudioComponentID：" + id + ",name：" + Data.name);
                    return;
                }
                //TODO：连乘算法(0.5*2=1，0.25*4 = 1)是否需要修改为分部加算算法(0.5*0.5+1.5*0.5 = 1，0*0.5+2*0.5=1)
                resultNumber *= volumeRTPC.GetYxisValue(gameParameter.ValueAtNormalized);
            }

            //State中的设置
            if (enableState)
            {
                foreach (var volumeData in currentStateVolumeDictionary.Values)
                {
                    resultNumber *= volumeData;
                }
            }

            SetScriptPlayableWeight(resultNumber);
        }

        protected virtual void SyncPlayablePitch()
        {
            //GeneralSetting中的设置
            float resultNumber = Data.pitch;

            //RTPC中的设置
            var pitchRTPCList = Data.gameParameterCurveSettings.FindAll(x => x.targetType == GameParameterTargetType.Pitch);
            foreach (var pitchRTPC in pitchRTPCList)
            {
                var gameParameter =
                    AudioEditorManager
                        .GetAEComponentDataByID<GameParameter>(pitchRTPC.gameParameterID) as GameParameter;
                if (gameParameter == null)
                {
                    Debug.LogWarning("RTPC未设置X轴属性，请检查，AudioComponentID：" + id + ",name：" + Data.name);
                    return;
                }

                resultNumber *= pitchRTPC.GetYxisValue(gameParameter.ValueAtNormalized);
            }

            //State中的设置
            if (enableState)
            {
                foreach (var pitchData in currentStatePitchDictionary.Values)
                {
                    resultNumber *= pitchData;
                }
            }
            SetScriptPlayableSpeed(resultNumber);
        }

        protected virtual void SyncStateChanged(int stateGroupId, int preStateId, int newStateId)
        {
            if (!enableState) return;
            var stateSetting = Data.StateSettings.Find(x => x.stateGroupId == stateGroupId);
            if (stateSetting == null)
            {
                return;
            }
            CheckStateDictionary();
            //执行缓动
            var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateGroupId) as StateGruop;
            var transition = stateGroup.GetTransition(preStateId, newStateId);
            var transitionTime = transition?.transitionTime ?? stateGroup.defaultTransitionTime;
            if (currentStateVolumeDictionary.TryGetValue(stateGroupId, out var startTransitionValue) == false)
            {
                Debug.LogError("[AudioEditor]: 发生错误");
            }
            else
            {
                var endTransitionValue = stateSetting.volumeList[stateGroup.FindStateIndex(newStateId)];
                TransitionAction.OnUpdate(stateSetting.volumeList, transitionTime, startTransitionValue, endTransitionValue, ref PlayableBehaviour.onPlayableBehaviourUpdate,
                    (x) =>
                    {
                        currentStateVolumeDictionary[stateGroupId] = x;
                        SyncPlayableVolume();
                    });
            }

            if (currentStatePitchDictionary.TryGetValue(stateGroupId, out startTransitionValue) == false)
            {
                Debug.LogError("[AudioEditor]: 发生错误");
            }
            else
            {
                var endTransitionValue = stateSetting.pitchList[stateGroup.FindStateIndex(newStateId)];
                TransitionAction.OnUpdate(stateSetting.pitchList, transitionTime, startTransitionValue, endTransitionValue, ref PlayableBehaviour.onPlayableBehaviourUpdate,
                    (x) =>
                    {
                        currentStatePitchDictionary[stateGroupId] = x;
                        SyncPlayablePitch();
                    });
            }
        }

        /// <summary>
        /// 停止state的所有过渡，直接同步当前值
        /// </summary>
        private void SyncStateDirectly()
        {
            CheckStateDictionary();
            foreach (var stateSetting in Data.StateSettings)
            {
                TransitionAction.EndTransition(stateSetting.volumeList);
                TransitionAction.EndTransition(stateSetting.pitchList);
                var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId) as StateGruop;
                if (stateGroup == null)
                {
                    Debug.LogError($"[AudioEdtior]:发生错误,无法获取id为{stateSetting.stateGroupId}StateGroup数据");
                    continue;
                }
                var index = stateGroup.GetCurrentStateIndex();
                currentStateVolumeDictionary[stateSetting.stateGroupId] = stateSetting.volumeList[index];
                currentStatePitchDictionary[stateSetting.stateGroupId] = stateSetting.pitchList[index];
            }
            SyncPlayableVolume();
            SyncPlayablePitch();
        }

        /// <summary>
        /// 检测Dctionary的数据长度与Data中的StateSetting数据长度是否一致
        /// </summary>
        private void CheckStateDictionary()
        {
            //Volume
            //检查是否有相应的StateGroup设置被删除
            int[] arr = new int[currentStateVolumeDictionary.Keys.Count];
            currentStateVolumeDictionary.Keys.CopyTo(arr, 0);
            for (int i = 0; i < arr.Length; i++)
            {
                if (Data.StateSettings.Find(x => x.stateGroupId == arr[i]) == null)
                {
                    Debug.Log("删除");
                    currentStateVolumeDictionary.Remove(arr[i]);
                }
            }
            //检查是否有新增的StateGroup设置
            if (currentStateVolumeDictionary.Count != Data.StateSettings.Count)
            {
                foreach (var stateSetting in Data.StateSettings)
                {
                    if (currentStateVolumeDictionary.ContainsKey(stateSetting.stateGroupId) == false)
                    {
                        var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId) as StateGruop;
                        if (stateGroup == null)
                        {
                            Debug.LogError($"[AudioEdtior]:发生错误,无法获取id为{stateSetting.stateGroupId}StateGroup数据");
                            continue;
                        }
                        Debug.Log("新增");
                        currentStateVolumeDictionary.Add(stateSetting.stateGroupId, stateSetting.volumeList[stateGroup.GetCurrentStateIndex()]);
                    }
                }
            }
            //Pitch
            //检查是否有相应的State设置被删除
            arr = new int[currentStatePitchDictionary.Keys.Count];
            currentStatePitchDictionary.Keys.CopyTo(arr, 0);
            for (int i = 0; i < arr.Length; i++)
            {
                if (Data.StateSettings.Find(x => x.stateGroupId == arr[i]) == null)
                {
                    currentStatePitchDictionary.Remove(arr[i]);
                }
            }
            //检查是否有新增的StateGroup设置
            if (currentStatePitchDictionary.Count != Data.StateSettings.Count)
            {
                foreach (var stateSetting in Data.StateSettings)
                {
                    if (!currentStatePitchDictionary.ContainsKey(stateSetting.stateGroupId))
                    {
                        var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId) as StateGruop;
                        if (stateGroup == null)
                        {
                            Debug.LogError($"[AudioEdtior]:发生错误,无法获取id为{stateSetting.stateGroupId}StateGroup数据");
                            continue;
                        }
                        currentStatePitchDictionary.Add(stateSetting.stateGroupId, stateSetting.pitchList[stateGroup.GetCurrentStateIndex()]);
                    }
                }
            }
        }
        private void SyncEffect()
        {
            if (AS == null || AS.gameObject == null || Data == null) return;

            // var components =  AS.gameObject.GetComponents(typeof(Behaviour));
            // foreach (var component in components)
            // {
            //     if (component is AudioLowPassFilter)
            //     {
            //
            //     }
            // }
            //TODO 当特效设置删除时没同步实例中删除
            foreach (var effectSetting in Data.effectSettings)
            {
                switch (effectSetting.type)
                {
                    case AEEffectType.LowPassFilter:
                        var lowPassFilter = AS.gameObject.GetComponent<AudioLowPassFilter>();
                        if (lowPassFilter == null)
                        {
                            lowPassFilter = AS.gameObject.AddComponent<AudioLowPassFilter>();
                        }
                        effectSetting.SyncDataToFilter(lowPassFilter);
                        break;
                    case AEEffectType.HighPassFilter:
                        var highPassFilter = AS.gameObject.GetComponent<AudioHighPassFilter>();
                        if (highPassFilter == null)
                        {
                            highPassFilter = AS.gameObject.AddComponent<AudioHighPassFilter>();
                        }
                        effectSetting.SyncDataToFilter(highPassFilter);
                        break;
                    case AEEffectType.ReverbFilter:
                        var reverbFilter = AS.gameObject.GetComponent<AudioReverbFilter>();
                        if (reverbFilter == null)
                        {
                            reverbFilter = AS.gameObject.AddComponent<AudioReverbFilter>();
                        }
                        effectSetting.SyncDataToFilter(reverbFilter);
                        break;
                    case AEEffectType.ReverbZoneFilter:
                        var reverZoneFilter = AS.GetComponent<AudioReverbZone>();
                        if (reverZoneFilter == null)
                        {
                            reverZoneFilter = AS.gameObject.AddComponent<AudioReverbZone>();
                        }
                        effectSetting.SyncDataToFilter(reverZoneFilter);
                        break;
                    case AEEffectType.EchoFilter:
                        var echoFilter = AS.GetComponent<AudioEchoFilter>();
                        if (echoFilter == null)
                        {
                            echoFilter = AS.gameObject.AddComponent<AudioEchoFilter>();
                        }
                        effectSetting.SyncDataToFilter(echoFilter);
                        break;
                    case AEEffectType.ChorusFilter:
                        var chorusFilter = AS.GetComponent<AudioChorusFilter>();
                        if (chorusFilter == null)
                        {
                            chorusFilter = AS.gameObject.AddComponent<AudioChorusFilter>();
                        }
                        effectSetting.SyncDataToFilter(chorusFilter);
                        break;
                    case AEEffectType.DistortionFilter:
                        var distortionFilter = AS.GetComponent<AudioDistortionFilter>();
                        if (distortionFilter == null)
                        {
                            distortionFilter = AS.gameObject.AddComponent<AudioDistortionFilter>();
                        }
                        effectSetting.SyncDataToFilter(distortionFilter);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        // protected event Action onPlayableBehaviourPrepareFrame;
        // protected void DoTransition(ref float target,float duration,Action updateFunc,Action completedFuc)
        // {
        //
        // }
    }



    public enum PlayableState
    {
        Play,
        Pause,
        Stop,
        FadeIn,
        FadeOut,
        StateTransitioning,
        EventFadeOut
    }

}