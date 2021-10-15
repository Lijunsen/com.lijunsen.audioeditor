using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using AudioEditor.Runtime.Utility.EasingCore;

namespace AudioEditor.Runtime.Utility
{
    internal abstract class AudioEditorPlayable
    {
        public readonly string name;
        //同步Audio层级中的ID
        public readonly int id;
        //public AEComponentType type;
        //记录依附于gameObject中的AudioSource
        //public AudioSource audioSource;

        public bool enableState = true;
        private bool isStoping = false;

        //计时器相关
        private AETimer delayClock;
        //private Stopwatch delayStopwatch;

        protected PlayableGraph graph;
        public AudioPlayableOutput playableOutput;

        public ActorMixerPlayable parentActorMixer;

        public abstract AudioEditorPlayableBehaviour PlayableBehaviour { get; }
        protected abstract AudioMixerPlayable MixerPlayable { get; }
         
        public event Action OnPlayableDestroy;

        /// <summary>
        ///   对应的数据，注意只能在PlayableBehaviour生成实例后使用使用，使用前请检测null
        /// </summary>
        public abstract AEAudioComponent Data { get; }

        public AudioEditorPlayable(string name, int id, AEComponentType type, ref PlayableGraph graph)
        {
            this.name = name;
            this.id = id;
            //this.type = type;
            this.graph = graph;
        }

        /// <summary>
        /// 初始化数据，生成PlayableOutput并同步AudioSource和GameParameter的值
        /// </summary>
        protected void Init()
        {
            PlayableBehaviour.Init();

            if (Data != null)
            {
                AudioEditorManager.GameParameterChanged += SyncGameParameter;
                AudioEditorManager.StateGroupSelectChanged += SyncState;
            }
        }


        public void Pause()
        {
            if (isStoping) return;
            if (delayClock != null && delayClock.IsEnd == false)
            {
                Debug.Log($"{DateTime.Now:h:mm:ss.fff}");
                delayClock.PauseTimer();
            }
            else
            {
                PlayableBehaviour.instance.Pause();
            }
        }

        public void Pause(float fadeTime, EaseType easeType = EaseType.Linear)
        {
            if (isStoping) return;

            if (fadeTime > 0)
            {
                var chainNode = PlayableBehaviour.playableVolumeChain.GetLinkNodeFitrst(PlayableBehaviour);
                if (chainNode == null)
                {
                    chainNode = PlayableBehaviour.playableVolumeChain.AddLinkNode(PlayableBehaviour);
                }

                var startValue = chainNode.value;
                TransitionAction.OnUpdate(chainNode, fadeTime, startValue, 0, ref PlayableBehaviour.onMixerPlayableUpdate,
                    (newValue) =>
                    {
                        chainNode.value = newValue;
                        PlayableBehaviour.SyncPlayableVolume();
                    }, () =>
                    {
                        PlayableBehaviour.playableVolumeChain.DeleteLinkNode(chainNode);
                        Pause();
                    }, easeType: easeType);

            }
            else
            {
                Pause();
            }
        }

        public void Resume()
        {
            if (isStoping) return;

            if (delayClock != null && delayClock.IsEnd == false)
            {
                Debug.Log($"{DateTime.Now:h:mm:ss.fff}");
                delayClock.ContinueTimer();
            }
            else
            {
                PlayableBehaviour.instance.Play();
            }
        }

        public void Resume(float fadeTime, EaseType easeType = EaseType.Linear)
        {
            if (isStoping) return;

            if (fadeTime > 0)
            {
                var chainNode = PlayableBehaviour.playableVolumeChain.GetLinkNodeFitrst(PlayableBehaviour);
                if (chainNode == null)
                {
                    //如果chianNode为空（说明不在暂停缓动中），且正在播放，则不进行resume缓动
                    if (PlayableBehaviour.instance.GetPlayState() == PlayState.Playing)
                    {
                        return;
                    }

                    chainNode = PlayableBehaviour.playableVolumeChain.AddLinkNode(PlayableBehaviour, 0);

                }

                var startValue = chainNode.value;
                TransitionAction.OnUpdate(chainNode, fadeTime, startValue, 1, ref PlayableBehaviour.onMixerPlayableUpdate,
                    (newValue) =>
                    {
                        chainNode.value = newValue;
                        PlayableBehaviour.SyncPlayableVolume();
                    }, () =>
                    {
                        PlayableBehaviour.playableVolumeChain.DeleteLinkNode(chainNode);
                    }, easeType: easeType);

                Resume();
            }
            else
            {
                Resume();
            }
        }

        public void Stop()
        {
            Pause();
            PlayableBehaviour.instance.SetDone(true);
        }

        public void Stop(float fadeTime, EaseType easeType = EaseType.Linear, Action fadeCompletedCallBack = null)
        {
            if (fadeTime > 0)
            {
                var chainNode = PlayableBehaviour.playableVolumeChain.GetLinkNodeFitrst(PlayableBehaviour);
                if (chainNode == null)
                {
                    chainNode = PlayableBehaviour.playableVolumeChain.AddLinkNode(PlayableBehaviour);
                }

                isStoping = true;

                var startValue = chainNode.value;
                TransitionAction.OnUpdate(chainNode, fadeTime, startValue, 0, ref PlayableBehaviour.onMixerPlayableUpdate,
                    (newValue) =>
                    {
                        chainNode.value = newValue;
                        PlayableBehaviour.SyncPlayableVolume();
                    }, () =>
                    {
                        isStoping = false;
                        PlayableBehaviour.playableVolumeChain.DeleteLinkNode(chainNode);
                        Stop();

                        fadeCompletedCallBack?.Invoke();
                    }, easeType: easeType);

            }
            else
            {
                Stop();
                fadeCompletedCallBack?.Invoke();
            }
        }

        public void Replay()
        {
            PlayableBehaviour.Init();
        }

        public void Replay(float fadeTime, EaseType easeType = EaseType.Linear)
        {
            Replay();
            if (fadeTime > 0)
            {
                var chainNode = PlayableBehaviour.playableVolumeChain.GetLinkNodeFitrst(PlayableBehaviour);
                if (chainNode == null)
                {
                    chainNode = PlayableBehaviour.playableVolumeChain.AddLinkNode(PlayableBehaviour, 0);
                }
                TransitionAction.OnUpdate(chainNode, fadeTime, 0, 1, ref PlayableBehaviour.onMixerPlayableUpdate,
                    (newValue) =>
                    {
                        chainNode.value = newValue;
                        PlayableBehaviour.SyncPlayableVolume();
                    }, () =>
                    {
                        PlayableBehaviour.playableVolumeChain.DeleteLinkNode(chainNode);
                    }, easeType: easeType);

            }
        }

        public void Delay(float delayTime, Action callback)
        {
            Pause();

            delayClock?.Destroy();

            // Debug.Log($"{DateTime.Now:h:mm:ss.fff}");
            //delayStopwatch = Stopwatch.StartNew();
            // delayClock = AETimer.CreateNewTimer(delayTime, () =>
            // {
            //     delayStopwatch.Stop();
            //     var timespan = delayStopwatch.Elapsed;
            //     delayStopwatch = null;
            //     Debug.Log(timespan.TotalSeconds);
            //
            //     Debug.Log($"{DateTime.Now:h:mm:ss.fff}");
            //     Debug.Log("/////////////////////////");
            //
            //     callback?.Invoke();
            // });

            delayClock = AETimer.CreateNewTimer(delayTime, callback);

            delayClock?.Start();

        }

        public abstract bool IsDone();

        public virtual void Destroy()
        {
            AudioEditorManager.GameParameterChanged -= SyncGameParameter;
            AudioEditorManager.StateGroupSelectChanged -= SyncState;

            OnPlayableDestroy?.Invoke();
            OnPlayableDestroy = null;

            if (delayClock != null && delayClock.IsDestroyed == false)
            {
                delayClock.Destroy();
            }
        }

        /// <summary>
        /// 自身或者子级组件是否与某个ID相关
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual bool Relate(int id)
        {
            if (this.id == id)
            {
                return true;
            }
            return false;
        }



        /// <summary>
        /// 获取正在播放的SoundSFX中clipPlayable的时间和总时间
        /// </summary>
        /// <param name="playTime">正在播放的时间</param>
        /// <param name="durationTime">对应ClipPlayable的总时间</param>
        /// <param name="isTheLastTimePlay">当前音频播放完后是否完全结束播放,即满足3个条件：1.最后一次循环 2.播放到最后一个组件 3.最后一个组件满足前两个条件</param>
        public abstract void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay);

        public abstract void GetAudioClips(ref List<AudioClip> clips);


        protected void SyncGameParameter(GameParameter gameParameter, float preNormalizeValue)
        {
            var curveSettings = Data.gameParameterCurveSettings.FindAll(x => x.gameParameterId == gameParameter.id);
            if (curveSettings.Count == 0) return;
            //var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(gameParameterID) ;
            //if (gameParameter == null)
            //{
            //    AudioEditorDebugLog.LogWarning("RTPC未设置X轴属性，请检查，AudioComponentID：" + id + ",name：" + Data.name);
            //    return;
            //}

            foreach (var gameParameterCurveSetting in curveSettings)
            {
                switch (gameParameterCurveSetting.targetType)
                {
                    case GameParameterTargetType.Volume:
                        var newValue = gameParameterCurveSetting.GetYxisValue(gameParameter.ValueAtNormalized);
                        PlayableBehaviour.playableVolumeChain.SetTargetValue(gameParameterCurveSetting, newValue);
                        PlayableBehaviour.SyncPlayableVolume();
                        break;
                    case GameParameterTargetType.Pitch:
                        newValue = gameParameterCurveSetting.GetYxisValue(gameParameter.ValueAtNormalized);
                        PlayableBehaviour.playablePitchChain.SetTargetValue(gameParameterCurveSetting, newValue);
                        PlayableBehaviour.SyncPlayablePitch();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }


        /// <summary>
        /// 当State切换的时候调用的方法，执行相关的过渡
        /// </summary>
        /// <param name="stateGroupId">相关的StateGroupId</param>
        /// <param name="preStateId">原来的StateId</param>
        /// <param name="newStateId">新的StateId</param>
        /// <param name="scope">作用范围</param>
        /// <param name="gameObject">作用对象</param>
        protected virtual void SyncState(int stateGroupId, int preStateId, int newStateId,AEEventScope scope,GameObject gameObject)
        {
            if (!enableState) return;
            StateSetting stateSetting = null;
            switch (scope)
            {
                case AEEventScope.GameObject:
                    stateSetting = Data.stateSettings.Find(x =>
                        x.stateGroupId == stateGroupId && playableOutput.GetUserData() == gameObject);
                    break;
                case AEEventScope.Global:
                    stateSetting = Data.stateSettings.Find(x => x.stateGroupId == stateGroupId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
            }
            if (stateSetting == null)
            {
                return;
            }
            //执行缓动
            var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateGroupId);
            var transition = stateGroup.GetTransition(preStateId, newStateId);
            var transitionTime = transition?.transitionTime ?? stateGroup.defaultTransitionTime;

            var stateVolumeChainNode = PlayableBehaviour.playableVolumeChain.GetLinkNodeFitrst(stateSetting);
            if (stateVolumeChainNode == null)
            {
                AudioEditorDebugLog.LogWarning("发生错误");
                stateVolumeChainNode = PlayableBehaviour.playableVolumeChain.AddLinkNode(stateSetting);
            }

            var startTransitionValue = stateVolumeChainNode.value;
            var endTransitionValue = stateSetting.volumeList[stateGroup.GetStateIndex(newStateId)];
            TransitionAction.OnUpdate(stateVolumeChainNode, transitionTime, startTransitionValue, endTransitionValue, ref PlayableBehaviour.onMixerPlayableUpdate,
                (newValue) =>
                {
                    stateVolumeChainNode.value = newValue;
                    PlayableBehaviour.SyncPlayableVolume();
                });

            var statePitchChainNode = PlayableBehaviour.playablePitchChain.GetLinkNodeFitrst(stateSetting);
            if (statePitchChainNode == null)
            {
                AudioEditorDebugLog.LogWarning("发生错误");
                statePitchChainNode = PlayableBehaviour.playablePitchChain.AddLinkNode(stateSetting);
            }

            startTransitionValue = statePitchChainNode.value;
            endTransitionValue = stateSetting.pitchList[stateGroup.GetStateIndex(newStateId)];
            TransitionAction.OnUpdate(statePitchChainNode, transitionTime, startTransitionValue, endTransitionValue, ref PlayableBehaviour.onMixerPlayableUpdate,
                (newValue) =>
                {
                    statePitchChainNode.value = newValue;
                    PlayableBehaviour.SyncPlayablePitch();
                });

        }


        /// <summary>
        /// 停止state的所有过渡，直接同步当前值
        /// </summary>
        public void SyncStateDirectly()
        {
            foreach (var stateSetting in Data.stateSettings)
            {
                TransitionAction.EndTransition(PlayableBehaviour.playableVolumeChain.GetLinkNodeFitrst(stateSetting));
                TransitionAction.EndTransition(PlayableBehaviour.playablePitchChain.GetLinkNodeFitrst(stateSetting));
                var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId);
                if (stateGroup == null)
                {
                    AudioEditorDebugLog.LogError($"发生错误,无法获取id为{stateSetting.stateGroupId}StateGroup数据");
                    continue;
                }
                var index = stateGroup.GetCurrentStateIndex();
                PlayableBehaviour.playableVolumeChain.SetTargetValue(stateSetting, stateSetting.volumeList[index]);
                PlayableBehaviour.playablePitchChain.SetTargetValue(stateSetting, stateSetting.pitchList[index]);
            }
            PlayableBehaviour.SyncPlayableVolume();
            PlayableBehaviour.SyncPlayablePitch();
        }



        protected static void AddChildrenPlayableToMixer(ref AudioMixerPlayable mixer, List<AudioEditorPlayable> childrenPlayable)
        {
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                mixer.AddInput(childrenPlayable[i].PlayableBehaviour.instance, 0, 1);
            }
        }
    }

    //public enum PlayableState
    //{
    //    Play,
    //    Pause,
    //    Stop,
    //    FadeIn,
    //    FadeOut,
    //    StateTransitioning,
    //    EventFadeOut
    //}


}