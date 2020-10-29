using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace ypzxAudioEditor.Utility
{
    public abstract class AudioEditorPlayableBehaviour : PlayableBehaviour
    {
        protected bool initialized = false;
        protected bool canDelay = true;
        protected bool canFadeIn = true;
        protected bool canFadeOut = true;
        /// <summary>
        /// 实例的剩余循环播放数（loop不为Infinite时）
        /// </summary>
        public int loopIndex = -1;
        public AudioMixerPlayable mixerPlayable;
        public Action<float> onPlayableBehaviourUpdate; 

        // protected bool isChild = false;

        public virtual void Init()
        {
            canDelay = true;
            canFadeIn = true;
            canFadeOut = true;
            initialized = true;
        }

        public virtual void SetData(AEAudioComponent data, bool isChild,
             List<AudioEditorPlayable> chidrenPlayables = null)
        {
            //     this.isChild = isChild;
        }


        public override void PrepareFrame(Playable playable, FrameData info)
        {
            base.PrepareFrame(playable, info);
            if (mixerPlayable.IsValid() && mixerPlayable.GetPlayState() == PlayState.Playing)
                onPlayableBehaviourUpdate?.Invoke(info.deltaTime);
        }

        public virtual bool JudgeDone()
        {
            return false;
        }

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

        protected void CheckFadeIn(AEAudioComponent data, ref AudioMixerPlayable mixerPlayable, int inputIndex, double time)
        {
            //在此加上是否能在父级启用的功能
            //   if(isChild) return;
            if (canFadeIn)
            {
                if (data.fadeIn && time < data.fadeInTime)
                {
                    switch (data.fadeInType)
                    {
                        case FadeType.Linear:
                            mixerPlayable.SetInputWeight(inputIndex, Mathf.Clamp01((float)(1 / data.fadeInTime * time)));
                            break;
                        case FadeType.Sin:
                            mixerPlayable.SetInputWeight(inputIndex, Mathf.Sin((float)(Mathf.PI / 2 * 1 / data.fadeInTime * time)));
                            break;
                    }
                }
                if (data.fadeIn && time > data.fadeInTime)
                {
                    canFadeIn = false;
                    //Debug.Log("fade In false");
                }
            }
        }

        protected void CheckFadeOut(AEAudioComponent data, ref AudioMixerPlayable mixerPlayable, int inputIndex, double time, double durationTime)
        {
            //在此加上是否能在父级启用的功能
            //     if (isChild) return;
            if (durationTime <= 0) return;
            if (canFadeOut)
            {
                if (data.fadeOut && time > durationTime - data.fadeOutTime)
                {
                    switch (data.fadeOutType)
                    {
                        case FadeType.Linear:
                            mixerPlayable.SetInputWeight(inputIndex, Mathf.Clamp01((float)(-1 / data.fadeOutTime * (time - durationTime + data.fadeOutTime) + 1)));
                            break;
                        case FadeType.Sin:
                            mixerPlayable.SetInputWeight(inputIndex, Mathf.Sin((float)(Mathf.PI / 2 * 1 / data.fadeOutTime * (time - durationTime + data.fadeOutTime) + Mathf.PI / 2)));
                            break;
                    }
                }
                if (data.fadeOut && time >= durationTime)
                {
                    canFadeOut = false;
                }
            }
        }

        /// <summary>
        /// 作为子组件时，回报父级自身是否是最后一次播放，且返回目前的状态
        /// </summary>
        /// <param name="playTime">当前播放的时间</param>
        /// <param name="durationTime">总时间</param>
        public abstract void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime);
    }
}