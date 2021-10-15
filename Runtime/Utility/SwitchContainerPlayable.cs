using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal class SwitchContainerPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<SwitchContainerPlayableBehaviour> switchContainerScriptPlayable;
        private readonly List<AudioEditorPlayable> childrenPlayable;
        private readonly GameObject eventAttachObject;

        public override AEAudioComponent Data => switchContainerScriptPlayable.GetBehaviour()?.Data;
        public override AudioEditorPlayableBehaviour PlayableBehaviour => switchContainerScriptPlayable.GetBehaviour();
        protected override AudioMixerPlayable MixerPlayable
        {
            get
            {
                if (switchContainerScriptPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return switchContainerScriptPlayable.GetBehaviour().mixerPlayable;
            }
        }

        public SwitchContainerPlayable(SwitchContainer data, PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, List<AudioEditorPlayable> childrenPlayable, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            this.childrenPlayable = childrenPlayable;
            this.eventAttachObject = eventAttachObject;

            var switchContainerScriptPlayableBehaviour = new SwitchContainerPlayableBehaviour(data, childrenPlayable);
            switchContainerScriptPlayable = ScriptPlayable<SwitchContainerPlayableBehaviour>.Create(base.graph, switchContainerScriptPlayableBehaviour);

            Init();

            AudioEditorManager.SwitchGroupSelectChanged += SyncSwitchChanged;

        }


        public ScriptPlayable<SwitchContainerPlayableBehaviour> GetScriptPlayable()
        {
            return switchContainerScriptPlayable;
        }

        public override bool IsDone()
        {
            if (switchContainerScriptPlayable.IsDone()) return true;
            if (childrenPlayable.Count == 0) return true;
            if (switchContainerScriptPlayable.GetBehaviour().JudgeDone())
            {
                switchContainerScriptPlayable.Pause();
                switchContainerScriptPlayable.SetDone(true);
                return true;
            }
            return false;
        }

        //public override void Replay()
        //{
        //    base.Replay();
        //    switchContainerScriptPlayable.GetBehaviour().Init();
        //}

        public override void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            isTheLastTimePlay = false;
            var playingIndex = switchContainerScriptPlayable.GetBehaviour().PlayingIndex;
            if (playingIndex < 0 && playingIndex >= childrenPlayable.Count)
            {
                playTime = durationTime = 0;
                return;
            }

            childrenPlayable[playingIndex].GetPlayState(out playTime, out durationTime, out var childLastPlay);

            if (childLastPlay)
            {
                if (Data.loop)
                {
                    if (Data.loopInfinite == false && PlayableBehaviour.CompletedLoopIndex == Data.loopTimes - 1)
                    {
                        isTheLastTimePlay = true;
                    }
                }
                else
                {
                    isTheLastTimePlay = true;
                }
            }
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            foreach (var playable in childrenPlayable)
            {
                playable.GetAudioClips(ref clips);
            }
        }

        //public override void Stop()
        //{
        //    switchContainerScriptPlayable.SetDone(true);
        //}

        public override void Destroy()
        {
            base.Destroy();
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                childrenPlayable[i].Destroy();
            }

            if (switchContainerScriptPlayable.CanDestroy())
            {
                graph.DestroySubgraph(switchContainerScriptPlayable);
            }

            AudioEditorManager.SwitchGroupSelectChanged -= SyncSwitchChanged;
        }

        public override bool Relate(int id)
        {
            if (base.Relate(id)) return true;
            if (childrenPlayable.FindIndex(x => x.Relate(id)) >= 0) return true;
            return false;
        }

        /// <summary>
        /// 响应Event中SetSwitch的操作
        /// </summary>
        /// <param name="switchGroup">相关的SwitchGroup</param>
        /// <param name="scope">范围</param>
        /// <param name="gameObject">事件触发的GameObject</param>
        public void SyncSwitchChanged(SwitchGroup switchGroup, AEEventScope scope, GameObject gameObject)
        {
            if ((Data as SwitchContainer).switchGroupID == switchGroup.id)
            {
                if ((scope == AEEventScope.GameObject && eventAttachObject == gameObject) || scope == AEEventScope.Global)
                {
                    switchContainerScriptPlayable.GetBehaviour().nextPlayIndex = switchGroup.GetSwitchIndex(switchGroup.currentSwitchID);
                    if ((Data as SwitchContainer).PlayMode == ContainerPlayMode.Continuous)
                    {
                        switchContainerScriptPlayable.GetBehaviour().ToNextPlay();
                    }
                }
            }
        }

        public class SwitchContainerPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            protected int playingIndex;
            public int nextPlayIndex = -1;
            private readonly SwitchContainer data;
            protected List<AudioEditorPlayable> childrenPlayable;

            public override AEAudioComponent Data { get => data; }
            public int PlayingIndex => playingIndex;

            public SwitchContainerPlayableBehaviour()
            {

            }

            public SwitchContainerPlayableBehaviour(SwitchContainer data, List<AudioEditorPlayable> childrenPlayables)
            {
                this.data = data;
                this.childrenPlayable = childrenPlayables;
                //loopIndex = data.loopTimes;
            }


            public override void OnPlayableCreate(Playable playable)
            {
                var graph = playable.GetGraph();
                mixerPlayable = AudioMixerPlayable.Create(graph);
                playable.AddInput(mixerPlayable, 0, 1);
                AddChildrenPlayableToMixer(ref mixerPlayable, childrenPlayable);

                base.OnPlayableCreate(playable);
            }

            public override void Init(bool initLoop = true, bool initDelay = true)
            {
                base.Init(initLoop, initDelay);

                //检查及初始化相关联的SwitchGroup
                var switchGroup = AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(data.switchGroupID);
                if (switchGroup == null)
                {
                    AudioEditorDebugLog.LogError("您生成了一个没有设置SwitchGroup的SwitchContainer实例，请检查");
                    instance.SetDone(true);
                    return;
                }

                if (data.outputIDList.Count == 0)
                {
                    instance.SetDone(true);
                    return;
                }

                nextPlayIndex = switchGroup.GetSwitchIndex(switchGroup.currentSwitchID);

                if (nextPlayIndex == -1)
                {
                    if (data.defualtPlayIndex < 0 || data.defualtPlayIndex >= data.outputIDList.Count)
                    {
                        nextPlayIndex = 0;
                    }
                    else
                    {
                        nextPlayIndex = data.defualtPlayIndex;
                    }
                }
                playingIndex = nextPlayIndex;

                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    if (i == playingIndex)
                    {
                        mixerPlayable.SetInputWeight(i, data.fadeIn ? 0f : 1f);

                        childrenPlayable[i].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                        childrenPlayable[i].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                        childrenPlayable[playingIndex].Replay();
                    }
                    else
                    {
                        mixerPlayable.SetInputWeight(i, 0f);

                        childrenPlayable[i].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                        childrenPlayable[i].Pause();
                    }

                    //if (i != playingIndex)
                    //{
                    //    childrenPlayable[i].Pause();
                    //}
                    //else
                    //{
                    //    childrenPlayable[i].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                    //    childrenPlayable[i].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                    //    childrenPlayable[playingIndex].Replay();
                    //}
                }
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (mixerPlayable.IsValid() == false) return;

                if (data.switchGroupID == -1 || data.outputIDList.Count == 0)
                {
                    playable.Pause();
                    playable.SetDone(true);
                    return;
                }

                if (data.PlayMode == ContainerPlayMode.Step && childrenPlayable[playingIndex].IsDone())
                {
                    playable.Pause();
                    playable.SetDone(true);
                    return;
                }

                if (CheckDelay()) return;

                CheckCommonFadeIn(data, playingIndex);

                childrenPlayable[playingIndex].GetPlayState(out var fadeOutTime, out var fadeOutDurationTime, out var isTheLastTimePlay);
                if (isTheLastTimePlay)
                {
                    CheckCommonFadeOut(data, playingIndex, fadeOutTime, fadeOutDurationTime);
                }

                ////检测是否有新的播放变动，同时进行循环次数上的调整
                //if (nextPlayIndex != playingIndex)
                //{
                //    if (data.PlayMode == ContainerPlayMode.Continuous)
                //    {
                //        if (data.loop && !data.loopInfinite)
                //        {
                //            CompletedLoopIndex = 0;
                //        }
                //        Init(false,false);
                //    }
                //}

            }

            protected override bool CheckDelay()
            {
                if (!base.CheckDelay()) return false;
                if (canDelay)
                {
                    if (data.delayTime > 0 && instance.GetTime() < data.delayTime)
                    {
                        childrenPlayable[playingIndex].Pause();
                        return true;
                    }
                    else
                    {
                        childrenPlayable[playingIndex].Resume();
                        canDelay = false;
                        return false;
                    }
                }
                return false;
            }

            public override bool JudgeDone()
            {
                if (initialized == false)
                {
                    return false;
                }
                if (data.PlayMode == ContainerPlayMode.Continuous) return false;
                if (data.loop)
                {
                    if (data.loopInfinite)
                    {
                        return false;
                    }
                }
                return childrenPlayable[playingIndex].IsDone();
            }

            public void ToNextPlay()
            {
                if (data.PlayMode != ContainerPlayMode.Continuous || PlayingIndex == nextPlayIndex)
                {
                    return;
                }

                var preIndex = playingIndex;
                var nextIndex = nextPlayIndex;

                if (data.crossFade && data.crossFadeTime > 0)
                {
                    switch (data.crossFadeType)
                    {
                        case CrossFadeType.Linear:

                            //if (data.loop && !data.loopInfinite)
                            //{
                            //    CompletedLoopIndex = 0;
                            //}
                            base.Init(true, false);

                            var playingChildPlayable = childrenPlayable[preIndex];

                            playingChildPlayable.GetPlayState(out var playingChildPlayableTime,
                                out var palyingChildPlayableDurationTime,
                                out var palyingChildPlayableisTheLastTimePlay);
                            if (playingChildPlayableTime >= 0 && palyingChildPlayableDurationTime > 0)
                            {
                                //CrossFadeTime自适应于前后播放的durantion/2以及基础设置三者中的最小值
                                var crossFadeTime = (double)data.crossFadeTime;
                                if (crossFadeTime > palyingChildPlayableDurationTime / 2f)
                                    crossFadeTime = palyingChildPlayableDurationTime / 2f;

                                childrenPlayable[nextIndex].GetPlayState(
                                    out var nextChildPlayableTime, out var nextChildPlayableDurationTime,
                                    out var nextChildPlayableisTheLastTimePlay);

                                if (crossFadeTime > nextChildPlayableDurationTime / 2f)
                                    crossFadeTime = nextChildPlayableDurationTime / 2;

                                childrenPlayable[preIndex].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                                DoMixerInportTransition(preIndex, (float)crossFadeTime, mixerPlayable.GetInputWeight(preIndex), 0, completeFunc: () =>
                                 {
                                     childrenPlayable[preIndex].Pause();
                                 });

                                DoMixerInportTransition(nextIndex, (float)crossFadeTime, mixerPlayable.GetInputWeight(nextIndex), 1);
                                childrenPlayable[nextIndex].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                                childrenPlayable[nextIndex].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                                childrenPlayable[nextIndex].Replay();

                                playingIndex = nextPlayIndex;
                            }
                            break;
                        case CrossFadeType.Delay:
                            base.Init(true, false);
                            canFadeIn = false;

                            mixerPlayable.SetInputWeight(preIndex, 0f);
                            childrenPlayable[preIndex].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                            childrenPlayable[preIndex].Pause();

                            TransitionAction.OnSilence(data.crossFadeTime, ref onMixerPlayableUpdate, () =>
                            {
                                canFadeIn = true;
                                mixerPlayable.SetInputWeight(nextIndex, 1f);
                                childrenPlayable[nextIndex].Replay();
                                childrenPlayable[nextIndex].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                                childrenPlayable[nextIndex].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                            });

                            playingIndex = nextPlayIndex;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    Init(true, false);
                }
            }

            private void OnChildrenPlayablePlayDone(int childId)
            {
                //Debug.Log("switchContainer OnChildrenPlayablePlayDone");
                childrenPlayable[playingIndex].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;

                if (data.loop)
                {
                    if (!data.loopInfinite)
                    {
                        if (CompletedLoopIndex < data.loopTimes)
                        {
                            CompletedLoopIndex++;
                        }
                        if (CompletedLoopIndex == data.loopTimes)
                        {
                            if (data.PlayMode != ContainerPlayMode.Continuous)
                            {
                                onPlayableDone?.Invoke(data.id);
                            }
                            return;
                        }
                    }
                    Init(false, false);
                }
                else
                {
                    if (data.PlayMode != ContainerPlayMode.Continuous)
                    {
                        onPlayableDone?.Invoke(data.id);
                    }
                }

            }

        }
    }
}
