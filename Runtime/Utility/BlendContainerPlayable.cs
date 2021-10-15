using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal class BlendContainerPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<BlendContainerPlayableBehavior> blendContainerScriptPlayable;
        private List<AudioEditorPlayable> childrenPlayable;

        public override AEAudioComponent Data => blendContainerScriptPlayable.GetBehaviour()?.Data;
        public override AudioEditorPlayableBehaviour PlayableBehaviour => blendContainerScriptPlayable.GetBehaviour();
        protected override AudioMixerPlayable MixerPlayable
        {
            get
            {
                if (blendContainerScriptPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return blendContainerScriptPlayable.GetBehaviour().mixerPlayable;
            }
        }

        public BlendContainerPlayable(BlendContainer data, PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, List<AudioEditorPlayable> childrenPlayable, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            this.childrenPlayable = childrenPlayable;

            var blendContainerScriptPlayableBehaviour = new BlendContainerPlayableBehavior(data, childrenPlayable);
            blendContainerScriptPlayable =
                ScriptPlayable<BlendContainerPlayableBehavior>.Create(base.graph, blendContainerScriptPlayableBehaviour);

            Init();

            AudioEditorManager.GameParameterChanged += SyncBlendTrack;
        }

        public override bool IsDone()
        {
            if (blendContainerScriptPlayable.IsDone())
            {
                return true;
            }
            if (childrenPlayable.Count == 0)
            {
                return true;
            }
            if (blendContainerScriptPlayable.GetBehaviour().JudgeDone())
            {
                blendContainerScriptPlayable.Pause();
                blendContainerScriptPlayable.SetDone(true);
                return true;
            }
            return false;
        }

        //public override void Replay()
        //{
        //    base.Replay();
        //    blendContainerScriptPlayable.GetBehaviour().Init();
        //}

        public override void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            //取剩余播放时间最长的状态
            isTheLastTimePlay = false;
            switch ((Data as BlendContainer).PlayMode)
            {
                case ContainerPlayMode.Step:
                    playTime = durationTime = 0f;
                    double maxChildDuration = 0f;
                    var index = 0;
                    for (var i = 0; i < childrenPlayable.Count; i++)
                    {
                        var childPlayable = childrenPlayable[i];
                        childPlayable.GetPlayState(out var childPlayTime, out var childDurationTime,
                            out var childLastPlay);
                        if (childDurationTime > maxChildDuration)
                        {
                            maxChildDuration = childDurationTime;
                            index = i;
                        }
                    }

                    childrenPlayable[index].GetPlayState(out playTime, out durationTime, out var isChildLastPlay);
                    if (isChildLastPlay)
                    {
                        if (Data.loop)
                        {
                            if (Data.loopInfinite == false && blendContainerScriptPlayable.GetBehaviour().completedLoopIndexList[index] ==
                                Data.loopTimes - 1)
                            {
                                isTheLastTimePlay = true;
                            }
                        }
                        else
                        {
                            isTheLastTimePlay = true;
                        }
                    }

                    break;
                case ContainerPlayMode.Continuous:
                    playTime = durationTime = 0;
                    break;
                default:
                    playTime = durationTime = 0;
                    break;
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

            if (blendContainerScriptPlayable.CanDestroy())
            {
                graph.DestroySubgraph(blendContainerScriptPlayable);
            }

            AudioEditorManager.GameParameterChanged -= SyncBlendTrack;
        }

        public override bool Relate(int id)
        {
            if (base.Relate(id)) return true;
            if (childrenPlayable.FindIndex(x => x.Relate(id)) >= 0) return true;
            return false;
        }


        /// <summary>
        /// 同步GAMEParameter的数值至BlendTrack
        /// </summary>
        /// <param name="gameParameterId"></param>
        /// <param name="preNormalizeValue"></param>
        private void SyncBlendTrack(GameParameter gameParameter, float preNormalizeValue)
        {
            blendContainerScriptPlayable.GetBehaviour().CheckBlendInfoByGameParameter(gameParameter, preNormalizeValue);
        }



        public class BlendContainerPlayableBehavior : AudioEditorPlayableBehaviour
        {
            private readonly BlendContainer data;
            public List<AudioEditorPlayable> childrenPlayable;
            public List<int> completedLoopIndexList = new List<int>();

            public override AEAudioComponent Data { get => data; }

            public BlendContainerPlayableBehavior()
            {

            }

            public BlendContainerPlayableBehavior(BlendContainer data, List<AudioEditorPlayable> chidrenPlayables)
            {
                this.data = data;
                this.childrenPlayable = chidrenPlayables;
                for (int i = 0; i < chidrenPlayables.Count; i++)
                {
                    completedLoopIndexList.Add(0);
                }
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

                CheckBlendInfo();

                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    childrenPlayable[i].PlayableBehaviour.onPlayableDone -= OnChildrenPlayablePlayDone;
                    childrenPlayable[i].PlayableBehaviour.onPlayableDone += OnChildrenPlayablePlayDone;
                    childrenPlayable[i].Replay();
                }

            }


            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (mixerPlayable.IsValid() == false) return;

                for (int i = 0; i < childrenPlayable.Count; i++)
                {
                    var childPlayable = childrenPlayable[i];

                    var hasRelateChidlBlendInfo = false;
                    foreach (var blendContainerTrack in data.blendContainerTrackList)
                    {
                        //筛选与gameParameter的数值有关联的ChildBlendInfo
                        var relateChildBlendInfos =
                            blendContainerTrack.FindChildBlendInfo(x => x.childId == childPlayable.id);
                        if (relateChildBlendInfos.Count > 0)
                        {
                            hasRelateChidlBlendInfo = true;
                        }
                    }
                    if (hasRelateChidlBlendInfo == false)
                    {
                        if (childPlayable.IsDone() == false)
                        {
                            childPlayable.Stop();
                            mixerPlayable.SetInputWeight(i, 0);
                        }
                    }
                }

                if (CheckDelay()) return;
            }

            protected override bool CheckDelay()
            {
                if (!base.CheckDelay()) return false;
                if (canDelay)
                {
                    if (data.delayTime > 0)
                    {
                        if (instance.GetTime() < data.delayTime)
                        {
                            foreach (var childPlayable in childrenPlayable)
                            {
                                childPlayable.Pause();
                            }
                            return true;
                        }
                        else
                        {
                            foreach (var childPlayable in childrenPlayable)
                            {
                                childPlayable.Resume();
                            }
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
                if (data.PlayMode == ContainerPlayMode.Continuous) return false;
                //当所有子组件的播放完毕则结束播放
                foreach (var childPlayable in childrenPlayable)
                {
                    if (!childPlayable.IsDone()) return false;
                }
                return true;
            }

            public void CheckBlendInfoByGameParameter(GameParameter gameParameter, float preNormalizeValue)
            {
                for (int i = 0; i < childrenPlayable.Count; i++)
                {
                    var childPlayable = childrenPlayable[i];

                    //var childPlayableBehaviour = mixerPlayable.GetInput(i);
                    var playableChain = mixerInputWeightChainList[i];

                    var isExitingBlendInfo = false;
                    var isEnteringBlendInfo = false;
                    var gameParameterContainInBlendInfoNumber = 0;

                    var hasRelateChidlBlendInfo = false;
                    foreach (var blendContainerTrack in data.blendContainerTrackList)
                    {
                        if (blendContainerTrack.gameParameterId != gameParameter.id) continue;

                        //var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(blendContainerTrack.gameParameterId);
                        //if (gameParameter == null) continue;

                        //筛选与gameParameter的数值有关联的ChildBlendInfo
                        var relateChildBlendInfos =
                            blendContainerTrack.FindChildBlendInfo(x => x.childId == childPlayable.id);
                        if (relateChildBlendInfos.Count > 0) hasRelateChidlBlendInfo = true;
                        //每个blendTrack只能对childPlayable输出一个节点，若有复数对应的BlendInfo，以大于0的输出值为准
                        var resultValue = 1f;
                        for (int j = 0; j < relateChildBlendInfos.Count; j++)
                        {
                            //因为相同ChildId的BlendInfo是互斥的，所以只需要取一个有大于0的值即可
                            if (relateChildBlendInfos[j].GetValue(gameParameter.ValueAtNormalized) > 0)
                            {
                                resultValue = relateChildBlendInfos[j].GetValue(gameParameter.ValueAtNormalized);
                                //Debug.Log(resultValue+"      "+ childrenPlayable[i].id);
                                break;
                            }
                            else
                            {
                                //如果所有的childBlendInfo都输出0，才总输出0
                                if (j == relateChildBlendInfos.Count - 1)
                                {
                                    resultValue = 0f;
                                }
                            }
                        }
                        for (int j = 0; j < relateChildBlendInfos.Count; j++)
                        {
                            //检测是否为正在出边界和进入边界
                            if (relateChildBlendInfos[j].ContainXAixsValue(preNormalizeValue) == false &&
                                relateChildBlendInfos[j].ContainXAixsValue(gameParameter.ValueAtNormalized))
                            {
                                isEnteringBlendInfo = true;
                            }

                            if (relateChildBlendInfos[j].ContainXAixsValue(preNormalizeValue) &&
                                relateChildBlendInfos[j].ContainXAixsValue(gameParameter.ValueAtNormalized) == false)
                            {
                                isExitingBlendInfo = true;
                            }
                            //检测是否包含在此BlendInfo中
                            if (relateChildBlendInfos[j].ContainXAixsValue(gameParameter.ValueAtNormalized))
                            {
                                gameParameterContainInBlendInfoNumber++;
                            }
                        }

                        var chainNode = playableChain.GetLinkNodeFitrst(blendContainerTrack);
                        if (chainNode == null)
                        {
                            chainNode = playableChain.AddLinkNode(blendContainerTrack);
                        }
                        chainNode.value = resultValue;
                    }

                    if (hasRelateChidlBlendInfo)
                    {
                        mixerPlayable.SetInputWeight(i, playableChain.GetValue());

                        if (data.PlayMode == ContainerPlayMode.Continuous)
                        {
                            //如果是正在退出某一BlendInfo且不再包含在任意相关blendInfo中
                            if (isExitingBlendInfo && !isEnteringBlendInfo && gameParameterContainInBlendInfoNumber == 0)
                            {
                                childPlayable.Pause();
                            }
                            //如果是正在进入某一BlendInfo且仅包含在某一相关blendInfo中
                            if (isEnteringBlendInfo && !isExitingBlendInfo && gameParameterContainInBlendInfoNumber == 1)
                            {
                                childPlayable.Replay();
                                //Continuous下每次replay都重置循环次数
                                completedLoopIndexList[i] = 0;
                            }
                        }
                    }
                }
            }

            public void CheckBlendInfo()
            {
                for (int i = 0; i < childrenPlayable.Count; i++)
                {
                    var childPlayable = childrenPlayable[i];
                    //var childPlayableBehaviour = mixerPlayable.GetInput(i);
                    var playableChain = mixerInputWeightChainList[i];

                    var hasRelateChidlBlendInfo = false;
                    foreach (var blendContainerTrack in data.blendContainerTrackList)
                    {
                        var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(blendContainerTrack.gameParameterId);
                        if (gameParameter == null) continue;
                        //筛选与gameParameter的数值有关联的ChildBlendInfo
                        var relateChildBlendInfos =
                            blendContainerTrack.FindChildBlendInfo(x => x.childId == childPlayable.id);
                        //每个blendTrack只能对childPlayable输出一个节点，若有复数对应的BlendInfo，以大于0的输出值为准
                        var resultValue = 1f;
                        if (relateChildBlendInfos.Count > 0)
                        {
                            hasRelateChidlBlendInfo = true;
                        }
                        for (int j = 0; j < relateChildBlendInfos.Count; j++)
                        {
                            //因为相同ChildId的BlendInfo是互斥的，所以只需要取一个有大于0的值即可
                            if (relateChildBlendInfos[j].GetValue(gameParameter.ValueAtNormalized) > 0)
                            {
                                resultValue = relateChildBlendInfos[j].GetValue(gameParameter.ValueAtNormalized);
                                break;
                            }
                            else
                            {
                                //如果所有的childBlendInfo都输出0，才总输出0
                                if (j == relateChildBlendInfos.Count - 1)
                                {
                                    resultValue = 0f;
                                }
                            }
                        }
                        var chainNode = playableChain.GetLinkNodeFitrst(blendContainerTrack);
                        if (chainNode == null)
                        {
                            chainNode = playableChain.AddLinkNode(blendContainerTrack);
                        }
                        chainNode.value = resultValue;
                    }

                    if (hasRelateChidlBlendInfo == false)
                    {
                        childPlayable.Stop();
                        this.mixerPlayable.SetInputWeight(i, 0);
                    }
                    else
                    {
                        this.mixerPlayable.SetInputWeight(i, playableChain.GetValue());
                    }
                }
            }

            public void OnChildrenPlayablePlayDone(int childId)
            {
                //Debug.Log("blendContainer OnChildrenPlayablePlayDone");
                var childIndex = childrenPlayable.FindIndex(x => x.id == childId);

                if (data.loop)
                {
                    if (!data.loopInfinite)
                    {
                        if (completedLoopIndexList[childIndex] < data.loopTimes)
                        {
                            completedLoopIndexList[childIndex]++;
                        }

                        if (completedLoopIndexList[childIndex] < data.loopTimes)
                        {
                            childrenPlayable[childIndex].Replay();
                        }
                        else
                        {
                            if (data.PlayMode != ContainerPlayMode.Continuous)
                            {
                                //如果其他的全部播放完毕,==1是因为当前的childPlayable到下一帧才会isdone
                                if (childrenPlayable.FindAll(x => x.IsDone() == false).Count == 1)
                                {
                                    onPlayableDone?.Invoke(data.id);
                                }
                            }
                        }
                    }
                    else
                    {
                        childrenPlayable[childIndex].Replay();
                    }
                }
                else
                {
                    if (data.PlayMode != ContainerPlayMode.Continuous)
                    {
                        if (childrenPlayable.FindAll(x => x.IsDone() == false).Count == 1)
                        {
                            onPlayableDone?.Invoke(data.id);
                        }
                    }
                }
            }

            public override void ResetLoop()
            {
                base.ResetLoop();
                completedLoopIndexList.Clear();
                for (var i = 0; i < childrenPlayable.Count; i++)
                {
                    completedLoopIndexList.Add(0);
                }
            }
        }
    }
}