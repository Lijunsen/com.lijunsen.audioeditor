using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace ypzxAudioEditor.Utility
{
    public class SwitchContainerPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<SwitchContainerPlayableBehaviour> switchContainerScriptPlayable;
        private List<AudioEditorPlayable> childrenPlayable;
        //  private AudioEditorManager manager;

        /// <summary>
        /// key = SwitchID,value = ChildrenPlayableID
        /// </summary>
        private Dictionary<int, int> switchAndChildrenPlayableComparisonTable;

        public SwitchContainerPlayable(SwitchContainer data, PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, List<AudioEditorPlayable> childrenPlayable, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            this.childrenPlayable = childrenPlayable;
            //  this.manager = manager;

            switchContainerScriptPlayable = ScriptPlayable<SwitchContainerPlayableBehaviour>.Create(m_Graph);
            switchContainerScriptPlayable.GetBehaviour().SetData(data, isChild, childrenPlayable);

            MixerPlayable = AudioMixerPlayable.Create(m_Graph);
            switchContainerScriptPlayable.AddInput(MixerPlayable, 0, 1);
            MixerPlayable = AddChildrenPlayableToMixer(MixerPlayable, childrenPlayable);

            Init(switchContainerScriptPlayable, eventAttachObject, audioSource);

            //检查及初始化相关联的SwitchGroup
            var switchGroup = AudioEditorManager.GetAEComponentDataByID<SwitchGroup>(data.switchGroupID) as SwitchGroup;
            if (switchGroup == null)
            {
                Debug.LogError("您生成了一个没有设置SwitchGroup的SwitchContainer实例，请检查");
                switchContainerScriptPlayable.SetDone(true);
                return;
            }
            switchAndChildrenPlayableComparisonTable = new Dictionary<int, int>();
            for (int i = 0; i < data.outputIDList.Count; i++)
            {
                switchAndChildrenPlayableComparisonTable.Add(switchGroup.FindSwitchAt(i).id, data.outputIDList[i]);
            }
            if (switchGroup.currentSwitchID != -1)
            {
                SyncSwitchChanged(data.switchGroupID, switchGroup.currentSwitchID);
                switchContainerScriptPlayable.GetBehaviour().Init();
            }
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

        public override void Replay()
        {
            switchContainerScriptPlayable.GetBehaviour().Init();
            switchContainerScriptPlayable.SetDone(false);
            switchContainerScriptPlayable.Play();
            base.Replay();
        }

        protected override void SetScriptPlayableSpeed(double value)
        {
            switchContainerScriptPlayable.SetSpeed(value);
        }

        protected override void SetScriptPlayableWeight(float newValue)
        {
            switchContainerScriptPlayable.SetInputWeight(MixerPlayable, newValue);
        }

        public override void GetTheLastTimePlayState(ref double playTime, ref double durationTime)
        {
            switchContainerScriptPlayable.GetBehaviour().GetTheLastTimePlayStateAsChild(ref playTime, ref durationTime);
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            foreach (var playable in childrenPlayable)
            {
                playable.GetAudioClips(ref clips);
            }
        }

        public override void Stop()
        {
            switchContainerScriptPlayable.SetDone(true);
        }

        public override void Destroy()
        {
            base.Destroy();
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                childrenPlayable[i].Destroy();
            }
            m_Graph.DestroySubgraph(switchContainerScriptPlayable);
            if (playableOutput.IsOutputValid())
            {
                playableOutput.SetTarget(null);
                playableOutput.SetUserData(null);
                //m_Graph.DestroyOutput(playableOutput);
                m_Graph.Stop();
                m_Graph.Destroy();
            }
            AudioEditorManager.SwitchGroupSelectChanged -= SyncSwitchChanged;
            //Debug.Log("Destroy SwitchContainerPlayable In Graph");
        }

        public void SyncSwitchChanged(int swichGroupID, int switchID)
        {
            //var data = AudioEditorManager.GetAEComponentDataByID<AEAudioComponent>(id) as SwitchContainer;
            if ((Data as SwitchContainer).switchGroupID == swichGroupID)
            {
                if (switchAndChildrenPlayableComparisonTable.TryGetValue(switchID, out var childPlayableID))
                {
                    switchContainerScriptPlayable.GetBehaviour().nextPlayIndex = childrenPlayable.FindIndex(x => x.id == childPlayableID);
                }
                // Debug.Log(switchID);
            }
            //Debug.Log("switch change");
        }

        public override bool Relate(int id)
        {
            if (base.Relate(id)) return true;
            if (childrenPlayable.FindIndex(x => x.id == id) >= 0) return true;
            return false;
        }

        protected override AudioEditorPlayableBehaviour PlayableBehaviour => switchContainerScriptPlayable.GetBehaviour();

        protected sealed override AudioMixerPlayable MixerPlayable
        {
            get => switchContainerScriptPlayable.GetBehaviour().mixerPlayable;
            set => switchContainerScriptPlayable.GetBehaviour().mixerPlayable = value;
        }

        public override AEAudioComponent Data => switchContainerScriptPlayable.GetBehaviour().data;
        public class SwitchContainerPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            public int playingIndex;
            public int nextPlayIndex = -1;
            public SwitchContainer data;
            public List<AudioEditorPlayable> childrenPlayable;

            public override void Init()
            {
                base.Init();
                if (data.switchGroupID == -1 || data.outputIDList.Count == 0)
                {
                    mixerPlayable.SetDone(true);
                }
                if (nextPlayIndex == -1)
                {
                    nextPlayIndex = data.defualtPlayIndex;
                }
                playingIndex = nextPlayIndex;
                mixerPlayable.SetTime(0f);
                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    mixerPlayable.SetInputWeight(i, i == playingIndex ? 1f : 0f);

                    if (i != playingIndex)
                    {
                        childrenPlayable[i].Pause();
                    }
                    else
                    {
                        childrenPlayable[playingIndex].Replay();
                    }
                }
                canFadeIn = true;
            }

            public override void OnBehaviourPlay(Playable playable, FrameData info)
            {
                Init();
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (data.switchGroupID == -1 || data.outputIDList.Count == 0)
                {
                    playable.SetDone(true);
                    return;
                }
                CheckDelay();
                CheckFadeIn(data, ref mixerPlayable, playingIndex, childrenPlayable[playingIndex].GetMixerTime());

                if (data.loop)
                {
                    if (!data.loopInfinite && loopIndex == 1)
                    {
                        double fadeOutTime = -1;
                        double fadeOutDurationTime = -1;
                        childrenPlayable[playingIndex].GetTheLastTimePlayState(ref fadeOutTime, ref fadeOutDurationTime);
                        CheckFadeOut(data, ref mixerPlayable, playingIndex, fadeOutTime, fadeOutDurationTime);
                    }
                }
                else
                {
                    double fadeOutTime = -1;
                    double fadeOutDurationTime = -1;
                    childrenPlayable[playingIndex].GetTheLastTimePlayState(ref fadeOutTime, ref fadeOutDurationTime);
                    CheckFadeOut(data, ref mixerPlayable, playingIndex, fadeOutTime, fadeOutDurationTime);
                }

                //检测是否有新的播放变动，同时进行循环次数上的调整
                if (nextPlayIndex != playingIndex)
                {
                    switch (data.PlayMode)
                    {
                        case ContainerPlayMode.Step:
                            if (childrenPlayable[playingIndex].IsDone())
                            {
                                if (data.loop)
                                {
                                    if (!data.loopInfinite)
                                    {
                                        if (loopIndex > 0)
                                        {
                                            loopIndex--;
                                        }
                                        if (loopIndex == 0)
                                        {
                                            return;
                                        }
                                    }
                                    Init();
                                }
                            }
                            return;
                        case ContainerPlayMode.Continuous:
                            if (data.loop && !data.loopInfinite)
                            {
                                loopIndex = data.loopTimes;
                            }
                            Init();
                            return;
                    }
                }
                //如果没有新变动则根据循环次数循环播放同一个音频
                if (childrenPlayable[playingIndex].IsDone())
                {
                    if (data.loop)
                    {
                        if (!data.loopInfinite)
                        {
                            if (loopIndex > 0)
                            {
                                loopIndex--;
                            }
                            if (loopIndex == 0)
                            {
                                return;
                            }
                        }
                        childrenPlayable[playingIndex].Replay();
                    }
                }
            }
            protected override bool CheckDelay()
            {
                if (!base.CheckDelay()) return false;
                if (canDelay)
                {
                    if (data.delayTime > 0 && mixerPlayable.GetTime() < data.delayTime)
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

            public override void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime)
            {
                if (playingIndex == nextPlayIndex)
                {
                    if (data.loop)
                    {
                        if (loopIndex == 1)
                        {
                            childrenPlayable[playingIndex].GetTheLastTimePlayState(ref playTime, ref durationTime);
                            return;
                        }
                    }
                    else
                    {
                        childrenPlayable[playingIndex].GetTheLastTimePlayState(ref playTime, ref durationTime);
                        return;
                    }
                }
                //没到最后一次播放或者循环为无限时，返回-1
                playTime = durationTime = -1;
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
                    else if (loopIndex == 0)
                    {
                        return true;
                    }
                    return false;
                }
                return childrenPlayable[playingIndex].IsDone();
            }

            public override void SetData(AEAudioComponent data, bool isChild, List<AudioEditorPlayable> childrenPlayables = null)
            {
                base.SetData(data, isChild, childrenPlayables);
                this.data = data as SwitchContainer;
                this.childrenPlayable = childrenPlayables;
                loopIndex = data.loopTimes;
            }
        }
    }
}
