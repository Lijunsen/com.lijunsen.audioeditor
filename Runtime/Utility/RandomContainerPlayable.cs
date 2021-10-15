using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal class RandomContainerPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<RandomContainerPlayableBehaviour> randomContainerScriptPlayable;
        private readonly List<AudioEditorPlayable> childrenPlayable;

        public override AEAudioComponent Data => randomContainerScriptPlayable.GetBehaviour()?.Data;
        public override AudioEditorPlayableBehaviour PlayableBehaviour => randomContainerScriptPlayable.GetBehaviour();
        protected override AudioMixerPlayable MixerPlayable
        {
            get
            {
                if (randomContainerScriptPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return randomContainerScriptPlayable.GetBehaviour().mixerPlayable;
            }
        }

        public RandomContainerPlayable(RandomContainer data, PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, List<AudioEditorPlayable> childrenPlayable, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            this.childrenPlayable = childrenPlayable;

            var randomContainerScriptPlayableBehaviour = new RandomContainerPlayableBehaviour(data, childrenPlayable);
            randomContainerScriptPlayable = ScriptPlayable<RandomContainerPlayableBehaviour>.Create(base.graph, randomContainerScriptPlayableBehaviour);

            Init();
        }

        public ScriptPlayable<RandomContainerPlayableBehaviour> GetScriptPlayable()
        {
            return randomContainerScriptPlayable;
        }

        public override bool IsDone()
        {
            if (randomContainerScriptPlayable.IsDone())
            {
                return true;
            }
            if (childrenPlayable.Count == 0)
            {
                return true;
            }
            if (randomContainerScriptPlayable.GetBehaviour().JudgeDone())
            {
                randomContainerScriptPlayable.Pause();
                randomContainerScriptPlayable.SetDone(true);
                return true;
            }
            return false;
        }

        //public override void Replay()
        //{
        //    base.Replay();
        //    randomContainerScriptPlayable.GetBehaviour().Init();
        //}

        public override void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            isTheLastTimePlay = false;
            var playListOrder = randomContainerScriptPlayable.GetBehaviour().PlayListOrder;
            var playingIndex = randomContainerScriptPlayable.GetBehaviour().PlayingIndex;

            if (playingIndex < 0 || playingIndex >= playListOrder.Count)
            {
                playTime = durationTime = 0;
                return;
            }

            childrenPlayable[playListOrder[playingIndex]].GetPlayState(out playTime, out durationTime, out var childLastPlay);

            if (childLastPlay)
            {
                if (playingIndex == playListOrder.Count - 1)
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
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            foreach (var playable in childrenPlayable)
            {
                playable.GetAudioClips(ref clips);
            }
        }

        public override void Destroy()
        {
            base.Destroy();
            for (int i = 0; i < childrenPlayable.Count; i++)
            {
                childrenPlayable[i].Destroy();
            }

            if (randomContainerScriptPlayable.CanDestroy())
            {
                graph.DestroySubgraph(randomContainerScriptPlayable);
            }

        }

        public override bool Relate(int id)
        {
            if (base.Relate(id)) return true;
            if (childrenPlayable.FindIndex(x => x.Relate(id)) >= 0) return true;
            return false;
        }

        public class RandomContainerPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            protected int playingIndex;
            private readonly RandomContainer data;
            private readonly List<AudioEditorPlayable> childrenPlayable;
            private bool canCrossFade = true;

            public int PlayingIndex => playingIndex;

            public override AEAudioComponent Data { get => data; }
            /// <summary>
            /// 储存Child的播放序列(非ChildID),内部元素数量由PlayMode决定
            /// </summary>
            public List<int> PlayListOrder { get; private set; }

            public RandomContainerPlayableBehaviour()
            {

            }

            public RandomContainerPlayableBehaviour(RandomContainer data, List<AudioEditorPlayable> childrenPlayables)
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
                canCrossFade = true;
                playingIndex = 0;
                if (PlayListOrder == null)
                {
                    PlayListOrder = new List<int>();
                }
                ResetChildrenPlayableList();

                if (PlayListOrder.Count == 0)
                {
                    return;
                }
                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    if (i == PlayListOrder[playingIndex])
                    {
                        mixerPlayable.SetInputWeight(i, data.fadeIn ? 0f : 1f);

                        childrenPlayable[i].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                        childrenPlayable[i].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                        childrenPlayable[i].Replay();
                    }
                    else
                    {
                        mixerPlayable.SetInputWeight(i, 0f);

                        childrenPlayable[i].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                        childrenPlayable[i].Pause();
                    }

                }
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (mixerPlayable.IsValid() == false) return;

                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    //setDone(true)如果是在PlayableCreate()里设置的话，playable的time没到duration且有前面有MixerPalyable
                    //则会被MixerPalyable驱动重新变为Done(false)，所以在Init中设置无效，故在此设置
                    if (playingIndex == PlayListOrder.Count)
                    {
                        //对于不在PlayListOrder中的序列，直接setDone
                        if (!PlayListOrder.Contains(i) && !childrenPlayable[i].IsDone())
                        {
                            childrenPlayable[i].Stop();
                        }
                    }

                    if (childrenPlayable[i].IsDone())
                    {
                        mixerPlayable.SetInputWeight(i, 0f);
                    }
                }

                if (childrenPlayable.FindAll(x => x.IsDone() == false).Count == 0)
                {
                    playable.SetDone(true);
                    playable.Pause();
                    return;
                }

                if (CheckDelay()) return;

                CheckCommonFadeIn(data, PlayListOrder[0]);

                if (playingIndex == PlayListOrder.Count - 1)
                {
                    childrenPlayable[PlayListOrder[playingIndex]].GetPlayState(out var fadeOutTime, out var fadeOutDurationTime, out var isTheLastTimePlay);
                    if (isTheLastTimePlay)
                    {
                        CheckCommonFadeOut(data, PlayListOrder[playingIndex], fadeOutTime, fadeOutDurationTime);
                    }
                }

                if (data.crossFade && data.crossFadeTime > 0 && data.PlayMode == ContainerPlayMode.Continuous)
                {
                    if (canCrossFade && data.crossFadeType == CrossFadeType.Linear)
                    {
                        //有两首及以上并且不是末尾才能进行交叉淡变
                        if (playingIndex + 1 < PlayListOrder.Count)
                        {
                            var currentPlayingChildrenIndex = PlayListOrder[playingIndex];
                            var playingChildPlayable = childrenPlayable[currentPlayingChildrenIndex];

                            playingChildPlayable.GetPlayState(out var playingChildPlayableTime,
                                out var palyingChildPlayableDurationTime,
                                out var palyingChildPlayableisTheLastTimePlay);
                            if (palyingChildPlayableisTheLastTimePlay && palyingChildPlayableDurationTime > 0)
                            {
                                //CrossFadeTime自适应于前后播放的durantion/2以及基础设置三者中的最小值
                                var crossFadeTime = (double)data.crossFadeTime;
                                if (crossFadeTime > palyingChildPlayableDurationTime / 2f)
                                    crossFadeTime = palyingChildPlayableDurationTime / 2f;
                                childrenPlayable[PlayListOrder[playingIndex + 1]].GetPlayState(
                                    out var nextChildPlayableTime, out var nextChildPlayableDurationTime,
                                    out var nextChildPlayableisTheLastTimePlay);
                                if (crossFadeTime > nextChildPlayableDurationTime / 2f)
                                {
                                    crossFadeTime = nextChildPlayableDurationTime / 2;
                                }

                                if (palyingChildPlayableDurationTime - playingChildPlayableTime <
                                    crossFadeTime)
                                {
                                    canCrossFade = false;
                                    playingChildPlayable.PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                                    DoMixerInportTransition(currentPlayingChildrenIndex, (float)crossFadeTime, 1, 0, completeFunc: () =>
                                    {
                                        canCrossFade = true;
                                        CheckLoop();
                                    });

                                    var nextChildIndex = PlayListOrder[playingIndex + 1];
                                    DoMixerInportTransition(nextChildIndex, (float)crossFadeTime, 0, 1);
                                    childrenPlayable[nextChildIndex].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                                    childrenPlayable[nextChildIndex].Replay();

                                    playingIndex++;

                                }
                            }
                        }
                    }
                }

            }

            protected override bool CheckDelay()
            {
                if (!base.CheckDelay()) return false;
                if (canDelay)
                {
                    if (playingIndex == 0 && data.delayTime > 0)
                    {
                        if (instance.GetTime() < data.delayTime)
                        {
                            childrenPlayable[PlayListOrder[playingIndex]].Pause();
                            return true;
                        }
                        else
                        {
                            childrenPlayable[PlayListOrder[playingIndex]].Resume();
                            canDelay = false;
                            return false;
                        }
                    }
                    else
                    {
                        canDelay = false;
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
                }
                //当所有子组件的播放完毕则结束播放
                foreach (var childPlayable in childrenPlayable)
                {
                    if (!childPlayable.IsDone()) return false;
                }
                return true;
            }

            /// <summary>
            /// 获取一份新的PlayListOder，请不要在循环过程中调用
            /// </summary>
            private void ResetChildrenPlayableList()
            {
                var preIdList = new List<int>();
                if (PlayListOrder.Count != 0)
                {
                    foreach (var index in PlayListOrder)
                    {
                        preIdList.Add(childrenPlayable[index].id);
                    }
                }
                var newIdList = data.GetRandomContainerChildrenIDList(preIdList);
                PlayListOrder.Clear();
                //Debug.Log("----------转化结果：");
                for (int i = 0; i < newIdList.Count; i++)
                {
                    var index = childrenPlayable.FindIndex((x) => x.id == newIdList[i]);
                    if (index == -1)
                    {
                        AudioEditorDebugLog.LogError(
                            "发生预料之外的错误，ContainerChildren与实例生成的PlayeableChidren不一致，导致组件无法初始化或重置,id为" + data.id);
                    }
                    else
                    {
                        PlayListOrder.Add(index);
                    }
                    //Debug.Log(index);
                }

                if (PlayListOrder.Count == 0)
                {
                    instance.SetDone(true);
                }
            }

            private void OnChildrenPlayablePlayDone(int childId)
            {
                //Debug.Log("randonContainer OnChildrenPlayablePlayDone");
                var childPlayable = childrenPlayable.Find(x => x.id == childId);
                childPlayable.PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;

                //mixerPlayable.SetInputWeight(playListOrder[playingIndex], 0f);

                EndMixerInportTransition(PlayListOrder[playingIndex]);

                if (data.crossFade && data.crossFadeType == CrossFadeType.Delay &&
                    data.PlayMode == ContainerPlayMode.Continuous && playingIndex + 1 < PlayListOrder.Count)
                {
                    canCrossFade = false;
                    TransitionAction.OnSilence(data.crossFadeTime, ref onMixerPlayableUpdate, () =>
                    {
                        playingIndex++;

                        if (playingIndex < PlayListOrder.Count)
                        {
                            mixerPlayable.SetInputWeight(PlayListOrder[playingIndex], 1f);
                            childrenPlayable[PlayListOrder[playingIndex]].Replay();
                            childrenPlayable[PlayListOrder[playingIndex]].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                        }
                        canCrossFade = true;

                        CheckLoop();
                    });
                }
                else
                {
                    playingIndex++;

                    if (playingIndex < PlayListOrder.Count)
                    {
                        mixerPlayable.SetInputWeight(PlayListOrder[playingIndex], 1f);
                        childrenPlayable[PlayListOrder[playingIndex]].Replay();
                        childrenPlayable[PlayListOrder[playingIndex]].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                    }

                    CheckLoop();
                }

            }

            private void CheckLoop()
            {
                if (data.loop)
                {
                    if (playingIndex == PlayListOrder.Count)
                    {
                        if (!data.loopInfinite)
                        {
                            if (CompletedLoopIndex < data.loopTimes)
                            {
                                CompletedLoopIndex++;
                            }
                            if (CompletedLoopIndex == data.loopTimes)
                            {
                                onPlayableDone?.Invoke(data.id);
                                return;
                            }
                        }
                        ToNextLoopPlay();
                    }
                }
                else
                {
                    if (playingIndex == PlayListOrder.Count)
                    {
                        onPlayableDone?.Invoke(data.id);
                    }
                }
            }

            private void ToNextLoopPlay()
            {
                Init(false, false);
            }

        }

    }
}

