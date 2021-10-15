using AudioEditor.Runtime.Utility.EasingCore;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;
using Random = UnityEngine.Random;

namespace AudioEditor.Runtime.Utility
{
    internal abstract class AudioEditorPlayableBehaviour : PlayableBehaviour
    {
        public Playable instance;
        protected bool initialized = false;
        protected bool canDelay = true;
        protected bool canFadeIn = true;
        protected bool canFadeOut = true;
        // protected bool isChild = false;

        public AudioMixerPlayable mixerPlayable;

        public ResponsibilityPatternChain playableVolumeChain;
        public ResponsibilityPatternChain playablePitchChain;

        //TODO key是否可以改为Playable的序列（int）
        //protected Dictionary<int, ResponsibilityPatternChain<Playable>> mixerInputWeightChainDictionary = new Dictionary<int, ResponsibilityPatternChain<Playable>>();

        protected List<ResponsibilityPatternChain> mixerInputWeightChainList = new List<ResponsibilityPatternChain>();

        public Action<float> onMixerPlayableUpdate;
        /// <summary>
        /// 如果组件将在这一帧内播放结束需调用此事件
        /// </summary>
        public Action<int> onPlayableDone;

        public int CompletedLoopIndex { get; protected set; }
        public abstract AEAudioComponent Data { get; }

        public AudioEditorPlayableBehaviour()
        {

        }

        public override void OnPlayableCreate(Playable playable)
        {
            base.OnPlayableCreate(playable);
            if (instance.IsNull()) instance = playable;
            playableVolumeChain = new ResponsibilityPatternChain(playable, true);
            playablePitchChain = new ResponsibilityPatternChain(playable, true);

        }

        public override void OnPlayableDestroy(Playable playable)
        {
            base.OnPlayableDestroy(playable);

            //将所有单链内容解构，避免引用无法回收
            playableVolumeChain.Dispose();
            playablePitchChain.Dispose();

            //foreach (var chain in mixerInputWeightChainDictionary.Values)
            //{
            //    chain.Dispose();
            //}
            foreach (var chain in mixerInputWeightChainList)
            {
                chain.Dispose();
            }

            playableVolumeChain = null;
            playablePitchChain = null;
            //mixerInputWeightChainDictionary.Clear();
            //mixerInputWeightChainDictionary = null;

            mixerInputWeightChainList.Clear();
            mixerInputWeightChainList = null;

            onMixerPlayableUpdate = null;
        }

        public virtual void Init(bool initLoop = true, bool initDelay = true)
        {
            canFadeIn = true;
            canFadeOut = true;
            initialized = true;

            if (initDelay)
            {
                canDelay = true;
            }

            if (initLoop)
            {
                ResetLoop();
            }

            if (Data != null)
            {
                if (Data.randomVolume)
                {
                    Data.volume = Random.value * (Data.maxVolume - Data.minVolume) + Data.minVolume;
                }

                if (Data.randomPitch)
                {
                    Data.pitch = Random.value * (Data.maxPitch - Data.minPitch) + Data.minPitch;
                }


                RefreshPlayableChain();

                SyncPlayableVolume();
                SyncPlayablePitch();


                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    EndMixerInportTransition(i);
                }
                
                mixerInputWeightChainList.Clear();
                for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
                {
                    mixerInputWeightChainList.Add(new ResponsibilityPatternChain(mixerPlayable.GetInput(i).GetHashCode()));
                }

                //RefreshMixerInportWeight();

            }

            mixerPlayable.SetTime(0f);
            mixerPlayable.SetDone(false);
            mixerPlayable.Play();
            instance.SetTime(0f);
            instance.SetDone(false);
            instance.Play();
        }

        public override void OnGraphStart(Playable playable)
        {
            //在调用graph.play()时候在graph中的所有playable组件都会调用此方法
            base.OnGraphStart(playable);
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            //只要调用playable.Play()都会执行此方法
            base.OnBehaviourPlay(playable, info);
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            base.PrepareFrame(playable, info);

            if (mixerPlayable.IsValid() && mixerPlayable.GetPlayState() == PlayState.Playing)
                onMixerPlayableUpdate?.Invoke(info.deltaTime);
        }

        //public void SetScriptPlayableWeight(float newValue)
        //{
        //    instance.SetInputWeight(mixerPlayable, newValue);
        //}

        //public void SetScriptPlayableSpeed(double newValue)
        //{
        //    instance.SetSpeed(newValue);
        //}

        public void SyncPlayableVolume()
        {
            var resultNumber = Data.volume;
            resultNumber *= playableVolumeChain.GetValue();
            instance.SetInputWeight(instance.GetInput(0), resultNumber);
        }

        public void SyncPlayablePitch()
        {
            var resultPitch = Data.pitch;
            resultPitch *= playablePitchChain.GetValue();
            instance.SetSpeed(resultPitch);
        }

        /// <summary>
        /// 断开所有节点重新链接，当有相关数据新增或删除时应调用此方法
        /// </summary>
        public void RefreshPlayableChain()
        {
            if (playableVolumeChain == null || playablePitchChain == null) return;
            playableVolumeChain.DeleteAllLinkNode();
            playablePitchChain.DeleteAllLinkNode();

            foreach (var gameParameterCurveSetting in Data.gameParameterCurveSettings)
            {
                var gameParameter = AudioEditorManager.GetAEComponentDataByID<GameParameter>(gameParameterCurveSetting.gameParameterId);
                if (gameParameter == null)
                {
                    AudioEditorDebugLog.LogWarning("RTPC未设置X轴属性，请检查，AudioComponentID：" + Data.id + ",name：" + Data.name);
                    return;
                }

                switch (gameParameterCurveSetting.targetType)
                {
                    case GameParameterTargetType.Volume:
                        playableVolumeChain.AddLinkNode(gameParameterCurveSetting,
                            gameParameterCurveSetting.GetYxisValue(gameParameter.ValueAtNormalized));
                        break;
                    case GameParameterTargetType.Pitch:
                        playablePitchChain.AddLinkNode(gameParameterCurveSetting,
                            gameParameterCurveSetting.GetYxisValue(gameParameter.ValueAtNormalized));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            foreach (var stateSetting in Data.stateSettings)
            {
                var stateGroup = AudioEditorManager.GetAEComponentDataByID<StateGruop>(stateSetting.stateGroupId);
                if (stateGroup == null)
                {
                    AudioEditorDebugLog.LogError("发生错误,无法寻找到对应StateGroup，组件初始化失败");
                    continue;
                }

                playableVolumeChain.AddLinkNode(stateSetting,
                    stateSetting.volumeList[stateGroup.GetCurrentStateIndex()]);

                playablePitchChain.AddLinkNode(stateSetting,
                    stateSetting.pitchList[stateGroup.GetCurrentStateIndex()]);

                //stateSetting.OnDispose += () =>
                //{
                //    PlayableBehaviour.playableVolumeChain.DeleteLinkNode(stateSetting);
                //    PlayableBehaviour.playablePitchChain.DeleteLinkNode(stateSetting);
                //};
            }
        }


        /// <summary>
        /// 判断PlayableBehaviour是否满足播放结束条件
        /// </summary>
        /// <returns></returns>
        public abstract bool JudgeDone();


        /// <summary>
        /// 检测是否执行延时
        /// </summary>
        /// <returns>true:正在执行Delay  false:Delay结束</returns>
        protected virtual bool CheckDelay()
        {
            //在此加上是否能在父级启用的功能
            //     if (this.isChild) return false;
            return true;
        }

        //TODO 目前这种方式会在播放时再勾选FadeIn时如果当前时间正好在fadeIn里时触发，会有一个播放一段时间~>勾选FadeIn~>开始FadeIn的时间差，而不是一开始就触发，与实际触发不符
        protected void CheckCommonFadeIn(AEAudioComponent data, int inputIndex)
        {
            //在此加上是否能在父级启用的功能
            //   if(isChild) return;
            if (canFadeIn)
            {
                //确认是否有设置FadeIn数值
                if (data.fadeIn && data.fadeInTime > 0)
                {
                    canFadeIn = false;
                    DoMixerInportTransition(inputIndex, data.fadeInTime, 0, 1, () =>
                    {
                        //Debug.Log("End CommonFadeIn");
                        //canFadeIn = true;
                    }, data.fadeInType);
                }
            }
        }

        protected void CheckCommonFadeOut(AEAudioComponent data, int inputIndex, double time, double durationTime)
        {
            //在此加上是否能在父级启用的功能
            //     if (isChild) return;
            if (durationTime <= 0) return;
            if (canFadeOut)
            {
                if (data.fadeOut && time > durationTime - data.fadeOutTime)
                {
                    //Debug.Log("FadeOut   "+ time);
                    canFadeOut = false;
                    DoMixerInportTransition(inputIndex, data.fadeOutTime, 1, 0, () =>
                      {
                          //Debug.Log("End CommonFadeOut");
                          //canFadeOut = true;
                      }, data.fadeOutType);
                }
            }
        }

        /// <summary>
        /// 执行交叉淡变、淡入淡出等功能
        /// </summary>
        /// <param name="mixerInputIndex">要执行的子组件在mixer中的序列</param>
        /// <param name="duration">持续时间</param>
        /// <param name="startValue">开始的值</param>
        /// <param name="endValue">结束的值</param>
        /// <param name="completeFunc">淡变结束后的回调</param>
        /// <param name="easeType">淡变过渡的类型</param>
        protected void DoMixerInportTransition(int mixerInputIndex, float duration, float startValue, float endValue, Action completeFunc = null, EaseType easeType = EaseType.Linear)
        {
            if (mixerInputIndex >= 0 && mixerInputIndex < mixerInputWeightChainList.Count)
            {
                var childPlayableBehaviour = mixerPlayable.GetInput(mixerInputIndex);
                //mixerInputWeightChainDictionary.TryGetValue(childrenPlayable[mixerInputIndex].id, out var childPlayableWeightChain);
                var childPlayableWeightChain = mixerInputWeightChainList[mixerInputIndex];

                if (childPlayableWeightChain == null) return;
                var newChainNode = childPlayableWeightChain.AddLinkNode(childPlayableBehaviour.GetHashCode());
                TransitionAction.OnUpdate(newChainNode, duration, startValue, endValue, ref onMixerPlayableUpdate, (newValue) =>
                {
                    newChainNode.value = newValue;
                    this.mixerPlayable.SetInputWeight(mixerInputIndex, childPlayableWeightChain.GetValue());
                }, () =>
                {
                    childPlayableWeightChain.DeleteLinkNode(newChainNode);
                    completeFunc?.Invoke();
                }, () =>
                {
                    childPlayableWeightChain.DeleteLinkNode(newChainNode);
                    completeFunc?.Invoke();
                }, easeType);
            }
        }

        protected void EndMixerInportTransition(int mixerInputIndex)
        {

            if (mixerInputIndex >= 0 && mixerInputIndex < mixerInputWeightChainList.Count)
            {
                var childPlayableBehaviour = mixerPlayable.GetInput(mixerInputIndex);

                //mixerInputWeightChainDictionary.TryGetValue(childrenPlayable[mixerInputIndex].id,out var currentPlayableChain);
                var currentPlayableChain = mixerInputWeightChainList[mixerInputIndex];

                if (currentPlayableChain == null)
                {
                    return;
                }
                foreach (var chainNode in currentPlayableChain.GetLinkNodes(childPlayableBehaviour.GetHashCode()))
                {
                    TransitionAction.EndTransition(chainNode);
                    currentPlayableChain.DeleteLinkNode(chainNode);
                }
            }
        }

        protected void RefreshMixerInportWeight()
        {
            for (int i = 0; i < mixerPlayable.GetInputCount(); i++)
            {
                if (i >= 0 && i < mixerInputWeightChainList.Count)
                {
                    //mixerInputWeightChainDictionary.TryGetValue(childrenPlayable[i].id, out var childPlayableWeightChain);
                    var childPlayableWeightChain = mixerInputWeightChainList[i];

                    if (childPlayableWeightChain == null) return;
                    mixerPlayable.SetInputWeight(i, childPlayableWeightChain.GetValue());
                }
            }
        }

        ///// <summary>
        ///// 作为子组件时，回报父级自身是否是最后一次播放，且返回目前的状态
        ///// </summary>
        ///// <param name="playTime">当前播放的时间</param>
        ///// <param name="durationTime">总时间</param>
        //public abstract void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime);

        public virtual void ResetLoop()
        {
            CompletedLoopIndex = 0;
        }
    }

    /// <summary>
    /// 一个用于储存数据的责任链模式，可输出单链上所有数据的乘积结果
    /// </summary>
    internal class ResponsibilityPatternChain
    {
        private readonly LinkNode first;
        private const int MaxLength = 1000;
        /// <summary>
        /// 互斥性，相同的target只能至多拥有一个节点
        /// </summary>
        public bool exclusiveness = false;

        /// <summary>
        /// 创建一条责任链模式实例
        /// </summary>
        /// <param name="target">依附的Object,会储存其HashCode，用于进行行为判别</param>
        /// <param name="exclusiveness">互斥性，设置为true则相同的target只能至多拥有一个节点</param>
        public ResponsibilityPatternChain(object target, bool exclusiveness = false)
        {
            first = new LinkNode(target.GetHashCode());
            this.exclusiveness = exclusiveness;
        }

        /// <summary>
        /// 新增一个链节点
        /// </summary>
        /// <param name="target">链节点所链接的target</param>
        /// <param name="vaule">链节点所储存的数据</param>
        /// <returns></returns>
        public LinkNode AddLinkNode(object target, float vaule = 1)
        {
            if (exclusiveness)
            {
                DeleteLinkNode(target);
            }
            var newLinkNode = new LinkNode(target.GetHashCode(), vaule);
            var linkNodePtr = first;
            int i = 0;
            while (linkNodePtr.next != null)
            {
                linkNodePtr = linkNodePtr.next;
                i++;
                if (i == MaxLength)
                {
                    Debug.Log("循环超界!!!!!!");
                    break;
                }
            }

            linkNodePtr.next = newLinkNode;
            return newLinkNode;
        }

        /// <summary>
        /// 删除对应target的所有节点
        /// </summary>
        /// <param name="target"></param>
        public void DeleteLinkNode(object target)
        {
            var hashCode = target.GetHashCode();
            var linkNodePtr = first;

            int i = 0;
            while (linkNodePtr.next != null)
            {
                var preLinkNodePtr = linkNodePtr;
                linkNodePtr = linkNodePtr.next;

                if (linkNodePtr.TargetId == hashCode)
                {
                    preLinkNodePtr.next = linkNodePtr.next;
                    linkNodePtr.next = null;
                    linkNodePtr = preLinkNodePtr;
                }

                i++;
                if (i == MaxLength)
                {
                    Debug.Log("循环超界!!!!!!");
                    break;
                }
            }
        }

        /// <summary>
        /// 删除某个节点
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public void DeleteLinkNode(LinkNode node)
        {
            var linkNodePtr = first;

            int i = 0;
            while (linkNodePtr.next != null)
            {
                var preLinkNodePtr = linkNodePtr;
                linkNodePtr = linkNodePtr.next;

                if (linkNodePtr == node)
                {
                    preLinkNodePtr.next = linkNodePtr.next;
                    linkNodePtr.next = null;
                    linkNodePtr = preLinkNodePtr;
                    break;
                }

                i++;
                if (i == MaxLength)
                {
                    Debug.Log("循环超界!!!!!!");
                    break;
                }
            }
        }

        /// <summary>
        /// 删除全部节点
        /// </summary>
        public void DeleteAllLinkNode()
        {
            var linkNodeList = new List<LinkNode>();
            var linkPtr = first;
            var index = 0;
            while (linkPtr.next != null)
            {
                linkNodeList.Add(linkPtr.next);
                linkPtr = linkPtr.next;
                index++;
                if (index > MaxLength) break;
            }
            //将所有节点解除引用和解除链接
            foreach (var linkNode in linkNodeList)
            {
                linkNode.TargetId = default;
                linkNode.next = null;
            }
            linkNodeList.Clear();
            first.next = null;
        }

        /// <summary>
        /// 销毁所有节点
        /// </summary>
        public void Dispose()
        {
            var linkNodeList = new List<LinkNode>();
            var linkPtr = first;
            linkNodeList.Add(linkPtr);
            var index = 0;
            while (linkPtr.next != null)
            {
                linkNodeList.Add(linkPtr.next);
                linkPtr = linkPtr.next;
                index++;
                if (index > MaxLength)
                {
                    break;
                }
            }
            //将所有节点解除引用和解除链接
            foreach (var linkNode in linkNodeList)
            {
                linkNode.TargetId = default;
                linkNode.next = null;
            }
            linkNodeList.Clear();
        }

        public LinkNode GetLinkNodeFitrst(object target)
        {
            var tempLinkNodes = GetLinkNodes(target);
            if (tempLinkNodes.Count > 0)
            {
                return tempLinkNodes[0];
            }
            return null;
        }

        public List<LinkNode> GetLinkNodes(object target)
        {
            var hashCode = target.GetHashCode();
            var linkNodePtr = first;
            int i = 0;
            var tempLinkNodeList = new List<LinkNode>();
            while (linkNodePtr.next != null)
            {
                linkNodePtr = linkNodePtr.next;
                if (linkNodePtr.TargetId == hashCode)
                {
                    tempLinkNodeList.Add(linkNodePtr);
                }

                i++;
                if (i == MaxLength)
                {
                    Debug.Log("循环超界!!!!!!");
                    break;
                }
            }

            if (exclusiveness && tempLinkNodeList.Count > 1)
            {
                AudioEditorDebugLog.LogError("发生错误,在互斥性单链中存在复数相同target的节点");
            }
            return tempLinkNodeList;
        }

        public float GetValue()
        {
            //CheckLinkNodeTargetExist();
            var resultValue = first.value;
            var linkNodePtr = first;
            int i = 0;
            while (linkNodePtr.next != null)
            {
                //不计入First节点的值
                linkNodePtr = linkNodePtr.next;
                resultValue *= linkNodePtr.value;
                i++;
                if (i == MaxLength)
                {
                    Debug.Log("循环超界!!!!!!");
                    break;
                }
            }
            return resultValue;
        }

        /// <summary>
        /// 修改相关target对应节点的数值
        /// </summary>
        /// <param name="target"></param>
        /// <param name="newValue"></param>
        public void SetTargetValue(object target, float newValue)
        {
            var linkNodes = GetLinkNodes(target);

            if (linkNodes.Count == 0)
            {
                //出于谨慎性，目前不新增节点
                //如果未找到有相关节点，则新增一个对应节点
                //   AddLinkNode(target, newValue);
            }
            else
            {
                foreach (var linkNode in linkNodes)
                {
                    linkNode.value = newValue;
                }
            }
        }

        ///// <summary>
        ///// 确认节点的target是否为Null，同步删除对应节点
        ///// </summary>
        //[Obsolete]
        //private void CheckLinkNodeTargetExist()
        //{
        //    var linkNodePtr = first;
        //    int i = 0;
        //    while (linkNodePtr.next != null)
        //    {
        //        var preLinkNodePtr = linkNodePtr;
        //        linkNodePtr = linkNodePtr.next;
        //        //就算target在list中被移除后，这里的target依旧是为强引用而不会等于null，
        //        //如果采用弱引用则不明确GC何时发生，第一时间判定仍不为null，需要主动进行delete节点
        //        if (linkNodePtr.TargetId == null)
        //        {
        //            Debug.Log("寻找到target已为空但未被删除的节点");
        //            preLinkNodePtr.next = linkNodePtr.next;
        //            linkNodePtr.next = null;
        //            linkNodePtr = preLinkNodePtr;
        //        }

        //        i++;
        //        if (i == MaxLength)
        //        {
        //            Debug.Log("循环超界!!!!!!");
        //            break;
        //        }
        //    }
        //}

        /// <summary>
        /// 获取当前链的长度
        /// </summary>
        /// <returns></returns>
        public int GetChainLength()
        {
            //默认的First节点不算入链长度
            var linkNodePtr = first;
            int i = 0;
            while (linkNodePtr.next != null)
            {
                linkNodePtr = linkNodePtr.next;

                i++;
                if (i == MaxLength)
                {
                    Debug.Log("循环超界!!!!!!");
                    break;
                }
            }
            return i;
        }
    }

    internal class LinkNode
    {
        /// <summary>
        /// 节点储存的数据
        /// </summary>
        public float value;
        public LinkNode next;

        /// <summary>
        /// 节点链接的target，当target为null时，此节点将被删除
        /// </summary>
        public int TargetId { get; set; }

        public LinkNode(int target, float value = 1)
        {
            this.TargetId = target;
            this.value = value;
        }

        // public float ValueTuple
        // {
        //     get => value;
        //     set
        //     {
        //         if (value < 0)
        //         {
        //             value = 0;
        //         }
        //         if (value > 1)
        //         {
        //             value = 1;
        //         }
        //         this.value = value;
        //     }
        // }
    }
}