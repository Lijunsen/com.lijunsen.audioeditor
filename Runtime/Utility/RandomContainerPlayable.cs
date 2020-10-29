using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Audio;

namespace ypzxAudioEditor.Utility
{
    public class RandomContainerPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<RandomContainerPlayableBehaviour> randomContainerScriptPlayable;
        List<AudioEditorPlayable> childrenPlayable;
        // RandomContainer data;

        //public AudioPlayableOutput playableOutput;

        public RandomContainerPlayable(RandomContainer data, PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, List<AudioEditorPlayable> childrenPlayable, bool isChild) : base(data.name,data.id, data.unitType, ref graph)
        {
            //  this.data = data;
            this.childrenPlayable = childrenPlayable;

            randomContainerScriptPlayable = ScriptPlayable<RandomContainerPlayableBehaviour>.Create(m_Graph);
            randomContainerScriptPlayable.GetBehaviour().SetData(data, isChild, childrenPlayable);

            MixerPlayable = AudioMixerPlayable.Create(m_Graph);
            randomContainerScriptPlayable.AddInput(MixerPlayable, 0, 1);
            MixerPlayable = AddChildrenPlayableToMixer(MixerPlayable, childrenPlayable);

            Init(randomContainerScriptPlayable, eventAttachObject, audioSource);
        }

        public ScriptPlayable<RandomContainerPlayableBehaviour> GetScriptPlayable()
        {
            return randomContainerScriptPlayable;
        }

        public override void Destroy()
        {
            base.Destroy();
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                childrenPlayable[i].Destroy();
            }

            m_Graph.DestroySubgraph(randomContainerScriptPlayable);
            if (playableOutput.IsOutputValid())
            {
                playableOutput.SetTarget(null);
                playableOutput.SetUserData(null);
                //m_Graph.DestroyOutput(playableOutput);
                m_Graph.Stop();
                m_Graph.Destroy();
            }

            //Debug.Log("Destroy RandomContainerPlayable In Graph");
        }

        public override bool IsDone()
        {
            if (randomContainerScriptPlayable.IsDone()) return true;
            if (childrenPlayable.Count == 0) return true;
            if (randomContainerScriptPlayable.GetBehaviour().JudgeDone())
            {
                randomContainerScriptPlayable.Pause();
                randomContainerScriptPlayable.SetDone(true);
                return true;
            }
            return false;
        }


        public override void Replay()
        {
            //if (childrenPlayable.Count != 0)
            //{
            //    childrenPlayable[0].Replay();
            //}
            randomContainerScriptPlayable.GetBehaviour().Init();
            randomContainerScriptPlayable.SetDone(false);
            randomContainerScriptPlayable.Play();
            base.Replay();
        }


        protected override void SetScriptPlayableSpeed(double value)
        {
            randomContainerScriptPlayable.SetSpeed(value);
        }

        protected override void SetScriptPlayableWeight(float newValue)
        {
            randomContainerScriptPlayable.SetInputWeight(MixerPlayable, newValue);
        }

        public override void GetTheLastTimePlayState(ref double playTime, ref double durationTime)
        {
            randomContainerScriptPlayable.GetBehaviour().GetTheLastTimePlayStateAsChild(ref playTime, ref durationTime);
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
            //if fadeOut
            Pause();
            randomContainerScriptPlayable.SetDone(true);
        }


        protected override AudioEditorPlayableBehaviour PlayableBehaviour => randomContainerScriptPlayable.GetBehaviour();

        protected sealed override AudioMixerPlayable MixerPlayable
        {
            get => randomContainerScriptPlayable.GetBehaviour().mixerPlayable;
            set => randomContainerScriptPlayable.GetBehaviour().mixerPlayable = value;
        }


        public override AEAudioComponent Data { get => randomContainerScriptPlayable.GetBehaviour().data; }

        public class RandomContainerPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            private int playingIndex;
            /// <summary>
            /// 储存Child的播放序列(非ChildID),内部元素数量由PlayMode决定
            /// </summary>
            private List<int> playListOrder;
            public RandomContainer data;
            public List<AudioEditorPlayable> childrenPlayable;

            public override void Init()
            {
                base.Init();
                playingIndex = 0;
                if (playListOrder == null)
                {
                    playListOrder = new List<int>();
                    // for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                    // {
                    //     playListOrder.Add(i);
                    // }
                    ResetChildrenPlayableList();
                }
                mixerPlayable.SetDone(false);
                mixerPlayable.SetTime(0f);
                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    mixerPlayable.SetInputWeight(i, i == playListOrder[playingIndex] ? 1f : 0f);

                    if (i != playListOrder[playingIndex])
                    {
                        //childrenPlayable[i].Replay();
                        childrenPlayable[i].Pause();
                    }
                    else
                    {
                        childrenPlayable[playListOrder[playingIndex]].Replay();
                    }
                }
            }

            public override void OnBehaviourPlay(Playable playable, FrameData info)
            {
                //Debug.Log("RandomContainer OnBehaviourPlay");
                Init();
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (playingIndex == playListOrder.Count)
                {
                    playable.SetDone(true);
                    return;
                }
                CheckDelay();
                CheckFadeIn(data, ref mixerPlayable, playListOrder[0], childrenPlayable[playListOrder[0]].GetMixerTime());
                if (playingIndex == playListOrder.Count - 1)
                {
                    double fadeOutTime = -1;
                    double fadeOutDurationTime = -1;
                    childrenPlayable[playListOrder[playingIndex]].GetTheLastTimePlayState(ref fadeOutTime, ref fadeOutDurationTime);
                    CheckFadeOut(data, ref mixerPlayable, playListOrder[playingIndex], fadeOutTime, fadeOutDurationTime);
                }
                //顺序从第一首播放至最后一首
                if (childrenPlayable[playListOrder[playingIndex]].IsDone())
                {
                    mixerPlayable.SetInputWeight(playListOrder[playingIndex], 0f);
                    playingIndex++;

                    if (playingIndex < playListOrder.Count)
                    {
                        mixerPlayable.SetTime(0f);
                        mixerPlayable.SetInputWeight(playListOrder[playingIndex], 1f);
                        childrenPlayable[playListOrder[playingIndex]].Replay();
                    }
                }

                //判断loop模式下是否执行重组及重播
                if (data.loop)
                {
                    if (playingIndex == playListOrder.Count)
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
                        ResetChildrenPlayableList();
                        Init();
                    }
                }
            }

            protected override bool CheckDelay()
            {
                if (!base.CheckDelay()) return false;
                if (canDelay)
                {
                    if (playingIndex == 0 && data.delayTime > 0 && mixerPlayable.GetTime() < data.delayTime)
                    {
                        childrenPlayable[playListOrder[playingIndex]].Pause();
                        return true;
                    }
                    else
                    {
                        childrenPlayable[playListOrder[playingIndex]].Resume();
                        canDelay = false;
                        return false;
                    }
                }
                return false;
            }

            public override void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime)
            {
                if (playingIndex == playListOrder.Count - 1)
                {
                    if (data.loop)
                    {
                        if (loopIndex == 1)
                        {
                            childrenPlayable[playListOrder[playingIndex]].GetTheLastTimePlayState(ref playTime, ref durationTime);
                            return;
                        }
                    }
                    else
                    {
                        childrenPlayable[playListOrder[playingIndex]].GetTheLastTimePlayState(ref playTime, ref durationTime);
                        return;
                    }
                }
                playTime = durationTime = -1;
            }

            public override bool JudgeDone()
            {
                if (initialized == false)
                {
                    return false;
                }
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
                switch (data.PlayMode)
                {
                    case ContainerPlayMode.Step:
                        return childrenPlayable[playListOrder[0]].IsDone();
                    case ContainerPlayMode.Continuous:
                        return childrenPlayable[playListOrder[playListOrder.Count - 1]].IsDone();
                    default:
                        return false;
                }
            }

            private void ResetChildrenPlayableList()
            {
                //此处可考虑性能优化
                // for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                // {
                //     mixerPlayable.DisconnectInput(i);
                // }
                // mixerPlayable.SetInputCount(0);
                // var newIDList = data.GetRandomContainerChildrenIDList();
                // var tempPlayableList = new List<AudioEditorPlayable>();
                // foreach (var ID in newIDList)
                // {
                //     foreach (var playable in childrenPlayable)
                //     {
                //         if (playable.id == ID)
                //         {
                //             tempPlayableList.Add(playable);
                //         }
                //     }
                // }
                // childrenPlayable.Clear();
                // childrenPlayable.AddRange(tempPlayableList);
                // mixerPlayable = AudioEditorPlayable.AddChildrenPlayableToMixer(mixerPlayable, childrenPlayable);
                var preIdList = new List<int>();
                if (playListOrder.Count != 0)
                {
                    foreach (var index in playListOrder)
                    {
                        preIdList.Add(childrenPlayable[index].id);
                    }
                }
                var newIdList = data.GetRandomContainerChildrenIDList(preIdList);
                if (newIdList.Count == 0)
                {
                    mixerPlayable.SetDone(true);
                }
                playListOrder.Clear();
                //Debug.Log("----------转化结果：");
                for (int i = 0; i < newIdList.Count; i++)
                {
                    if (data.PlayMode == ContainerPlayMode.Step && i != 0) break;
                    var index = childrenPlayable.FindIndex((x) => x.id == newIdList[i]);
                    if (index == -1)
                    {
                        Debug.LogError("[AudioEditor]: 发生预料之外的错误，ContainerChildren与实例生成的PlayeableChidren不一致，导致组件无法初始化或重置,id为" + data.id);
                    }
                    else
                    {
                        playListOrder.Add(index);
                    }
                    //Debug.Log(index);
                }
            }

            public override void SetData(AEAudioComponent data, bool isChild, List<AudioEditorPlayable> childrenPlayables = null)
            {
                base.SetData(data, isChild, childrenPlayables);
                this.data = data as RandomContainer;
                this.childrenPlayable = childrenPlayables;
                loopIndex = data.loopTimes;
            }
        }
    }
}

