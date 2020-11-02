using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Audio;

namespace ypzxAudioEditor.Utility
{
    public class SequenceContainerPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<SequenceContainerPlayableBehaviour> sequenceContainerScriptPlayable;
        private List<AudioEditorPlayable> childrenPlayable;
        //SequenceContainer data;



        public SequenceContainerPlayable(SequenceContainer data, PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, List<AudioEditorPlayable> childrenPlayable, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            //this.data = data;
            this.childrenPlayable = childrenPlayable;

            sequenceContainerScriptPlayable = ScriptPlayable<SequenceContainerPlayableBehaviour>.Create(graph);
            sequenceContainerScriptPlayable.GetBehaviour().SetData(data, isChild, childrenPlayable);

            MixerPlayable = AudioMixerPlayable.Create(graph);
            sequenceContainerScriptPlayable.AddInput(MixerPlayable, 0, 1);
            MixerPlayable = AddChildrenPlayableToMixer(MixerPlayable, childrenPlayable);


            Init(sequenceContainerScriptPlayable, eventAttachObject, audioSource);
        }

        public ScriptPlayable<SequenceContainerPlayableBehaviour> GetScriptPlayable()
        {
            return sequenceContainerScriptPlayable;
        }

        public override void Destroy()
        {
            base.Destroy();
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                childrenPlayable[i].Destroy();
            }
            //m_Graph.DestroySubgraph(MixerPlayable);
            m_Graph.DestroySubgraph(sequenceContainerScriptPlayable);
            if (playableOutput.IsOutputValid())
            {
                playableOutput.SetTarget(null);
                playableOutput.SetUserData(null);
                //m_Graph.DestroyOutput(playableOutput);
                m_Graph.Stop();
                m_Graph.Destroy();
            }
            //Debug.Log("Destroy sequenceContainerPlayable In Graph");
        }

        public override bool IsDone()
        {
            if (sequenceContainerScriptPlayable.IsDone()) return true;
            if (childrenPlayable.Count == 0) return true;
            if (sequenceContainerScriptPlayable.GetBehaviour().JudgeDone())
            {
                sequenceContainerScriptPlayable.Pause();
                sequenceContainerScriptPlayable.SetDone(true);
                return true;
            }
            return false;
        }

        public override void Replay()
        {
            sequenceContainerScriptPlayable.GetBehaviour().Init();
            sequenceContainerScriptPlayable.SetDone(false);
            sequenceContainerScriptPlayable.Play();
            base.Replay();
        }


        protected override void SetScriptPlayableSpeed(double value)
        {
            sequenceContainerScriptPlayable.SetSpeed(value);
        }

        protected override void SetScriptPlayableWeight(float newValue)
        {
            sequenceContainerScriptPlayable.SetInputWeight(MixerPlayable, newValue);
        }

        public override void GetTheLastTimePlayState(ref double playTime, ref double durationTime)
        {
            sequenceContainerScriptPlayable.GetBehaviour().GetTheLastTimePlayStateAsChild(ref playTime, ref durationTime);
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
            sequenceContainerScriptPlayable.SetDone(true);
        }

        public override bool Relate(int id)
        {
            if (base.Relate(id)) return true;
            if (childrenPlayable.FindIndex(x => x.id == id) >= 0) return true;
            return false;
        }

        protected override AudioEditorPlayableBehaviour PlayableBehaviour => sequenceContainerScriptPlayable.GetBehaviour();

        protected sealed override AudioMixerPlayable MixerPlayable
        {
            get => sequenceContainerScriptPlayable.GetBehaviour().mixerPlayable;
            set => sequenceContainerScriptPlayable.GetBehaviour().mixerPlayable = value;
        }

        public override AEAudioComponent Data { get => sequenceContainerScriptPlayable.GetBehaviour().data; }
        public class SequenceContainerPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            private int playingIndex;
            public SequenceContainer data;
            /// <summary>
            /// 储存Child的播放序列(非ChildID),内部元素数量由PlayMode决定
            /// </summary>
            private List<int> playListOrder;
            private List<AudioEditorPlayable> childrenPlayable;
            public override void Init()
            {
                base.Init();
                playingIndex = 0;
                mixerPlayable.SetTime(0f);
                if (playListOrder == null)
                {
                    playListOrder = new List<int>();
                    //此处应获取根据算法获取IDList
                    for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                    {
                        playListOrder.Add(i);
                    }
                }
                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    mixerPlayable.SetInputWeight(i, i == playListOrder[playingIndex] ? 1f : 0f);
                    if (i != playListOrder[playingIndex])
                    {
                        childrenPlayable[i].Pause();
                    }
                    else
                    {
                        childrenPlayable[i].Replay();
                    }
                }
            }



            public override void OnBehaviourPlay(Playable playable, FrameData info)
            {
                //Debug.Log("SequenceContainer OnBehaviourPlay");
                Init();
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (playingIndex == childrenPlayable.Count)
                {
                    playable.SetDone(true);
                    return;
                }

                CheckDelay();
                CheckFadeIn(data, ref mixerPlayable, 0, childrenPlayable[0].GetMixerTime());
                //设置fadeOut
                if (playingIndex == playListOrder.Count - 1)
                {
                    double fadeOutTime = -1;
                    double fadeOutDurationTime = -1;
                    childrenPlayable[playingIndex].GetTheLastTimePlayState(ref fadeOutTime, ref fadeOutDurationTime);
                    CheckFadeOut(data, ref mixerPlayable, playingIndex, fadeOutTime, fadeOutDurationTime);
                }
                //子类播放完毕执行下一首轮换
                if (childrenPlayable[playListOrder[playingIndex]].IsDone())
                {
                    mixerPlayable.SetInputWeight(playListOrder[playingIndex], 0f);
                    playingIndex++;

                    if (playingIndex < childrenPlayable.Count)
                    {
                        mixerPlayable.SetInputWeight(playListOrder[playingIndex], 1f);
                        Debug.Log("replay " + playListOrder[playingIndex]);
                        childrenPlayable[playListOrder[playingIndex]].Replay();
                    }
                }

                //判断loop模式下是否执行重组及重播
                if (data.loop)
                {
                    //step模式下播放完毕的标志
                    var index = 1;
                    //continuous模式下播放完毕的标志(playingIndex越界)
                    if (data.PlayMode == ContainerPlayMode.Continuous) index = playListOrder.Count;

                    if (playingIndex == index)
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
                playListOrder.Clear();
                var newIDList = data.GetSequenceContainerChildrenIDList();
                for (int i = 0; i < newIDList.Count; i++)
                {
                    for (int j = 0; j < childrenPlayable.Count; j++)
                    {
                        if (childrenPlayable[j].id == newIDList[i])
                        {
                            playListOrder.Add(j);
                            Debug.Log(j);
                        }
                    }
                }
                //   Debug.Log("reset" + newIDList.Count);
            }
            public override void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime)
            {
                var theLastPlayIndex = data.PlayMode == ContainerPlayMode.Step ? 0 : childrenPlayable.Count - 1;
                if (playingIndex == theLastPlayIndex)
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
                playTime = durationTime = -1;
            }

            public override void SetData(AEAudioComponent data, bool isChild, List<AudioEditorPlayable> childrenPlayables = null)
            {
                base.SetData(data, isChild, childrenPlayables);
                this.data = data as SequenceContainer;
                this.childrenPlayable = childrenPlayables;
                loopIndex = data.loopTimes;
            }
        }
    }

}