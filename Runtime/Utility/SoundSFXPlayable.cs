using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal class SoundSFXPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<SoundSFXPlayableBehaviour> soundSFXScriptPlayable;

        public override AudioEditorPlayableBehaviour PlayableBehaviour => soundSFXScriptPlayable.GetBehaviour();
        public override AEAudioComponent Data => soundSFXScriptPlayable.GetBehaviour()?.Data;
        protected override AudioMixerPlayable MixerPlayable
        {
            get
            {
                if (soundSFXScriptPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return soundSFXScriptPlayable.GetBehaviour().mixerPlayable;
            }
        }

        public AudioClipPlayable ClipPlayable
        {
            get
            {
                if (soundSFXScriptPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return soundSFXScriptPlayable.GetBehaviour().clipPlayable;
            }
        }

        public SoundSFXPlayable(SoundSFX data, ref PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            var soundSFXPlayableBehaviour = new SoundSFXPlayableBehaviour(data, false);
            soundSFXScriptPlayable = ScriptPlayable<SoundSFXPlayableBehaviour>.Create(base.graph, soundSFXPlayableBehaviour);

            Init();
        }

        public ScriptPlayable<SoundSFXPlayableBehaviour> GetScriptPlayable()
        {
            return soundSFXScriptPlayable;
        }

        public override void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            //对外部来说ClipPlayable的duration增长以及播放轴的修改应该是不可见的
            playTime = ClipPlayable.GetTime() % ClipPlayable.GetClip().length;
            durationTime = ClipPlayable.GetClip().length;
            isTheLastTimePlay = false;
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

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            if (Data != null && Data is SoundSFX soundSFX)
            {
                if (soundSFX.clip != null)
                {
                    clips.Add(soundSFX.clip);
                }
                else
                {
                    AudioEditorDebugLog.LogError("寻找到没有AudioClip的SoundSFX");
                }
            }
        }

        //public override void Replay()
        //{
        //    base.Replay();
        //    soundSFXScriptPlayable.GetBehaviour().Init();
        //}

        public override bool IsDone()
        {
            if (soundSFXScriptPlayable.GetBehaviour() == null)
            {
                return true;
            }
            if (soundSFXScriptPlayable.IsDone())
            {
                return true;
            }
            if (soundSFXScriptPlayable.GetBehaviour().JudgeDone())
            {
                soundSFXScriptPlayable.SetDone(true);
                soundSFXScriptPlayable.Pause();
                return true;
            }
            return false;
        }

        public override void Destroy()
        {
            base.Destroy();

            if (ClipPlayable.CanDestroy())
            {
                //Debug.Log("DestroySubgraph(ClipPlayable);");
                //ClipPlayable.Destroy();

                graph.Disconnect(MixerPlayable, 0);
                graph.DestroyPlayable(ClipPlayable);
            }

            if (MixerPlayable.CanDestroy())
            {
                //Debug.Log("DestroySubgraph(MixerPlayable);");
                //MixerPlayable.Destroy();
                graph.Disconnect(soundSFXScriptPlayable, 0);
                graph.DestroyPlayable(MixerPlayable);
            }

            if (soundSFXScriptPlayable.CanDestroy())
            {
                //Debug.Log("DestroySubgraph(soundSFXScriptPlayable);");
                graph.DestroySubgraph(soundSFXScriptPlayable);
            }
        }


        public class SoundSFXPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            private readonly SoundSFX data;
            public AudioClipPlayable clipPlayable;

            public override AEAudioComponent Data { get => data; }
            public bool PrepareToCompleted { get; private set; } = false;

            public SoundSFXPlayableBehaviour()
            {

            }
            public SoundSFXPlayableBehaviour(SoundSFX data, bool isChild)
            {
                this.data = data;
                //loopIndex = data.loopTimes;
            }

            public override void OnPlayableCreate(Playable playable)
            {
                var graph = playable.GetGraph();

                //为了实现音频的无缝循环，clipPlayable需要开启loop
                clipPlayable = AudioClipPlayable.Create(graph, data.clip, data.loop);

                mixerPlayable = AudioMixerPlayable.Create(graph);

                mixerPlayable.AddInput(clipPlayable, 0, 1);

                playable.AddInput(mixerPlayable, 0, 1);

                base.OnPlayableCreate(playable);
            }

            public override void Init(bool initLoop = true, bool initDelay = true)
            {
                base.Init(initLoop, initDelay);

                mixerPlayable.SetInputWeight(0, data.fadeIn ? 0 : 1);

                ResetAudioClipPlayable();
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (clipPlayable.IsValid() == false || mixerPlayable.IsValid() == false) return;

                if (clipPlayable.GetClip() == null)
                {
                    playable.SetDone(true);
                    playable.Pause();
                    return;
                }

                if (CheckDelay()) return;
                CheckCommonFadeIn(data, 0);

                CheckCommonFadeOut(data, 0, clipPlayable.GetTime(), clipPlayable.GetDuration());

                ////在这一帧中clipPlayable还未更新，需加上deltaTime进行预测
                //if (clipPlayable.GetTime() + info.deltaTime >= clipPlayable.GetDuration())
                //{
                //    deltaTime = info.deltaTime;
                //    //ClipPlayable将在当前帧结束播放
                //    Debug.Log("prepareToCompleted");

                //    //clipPlayable.SetLooped(false);

                //    if (PrepareToCompleted == false)
                //    {
                //        PrepareToCompleted = true;
                //        //Debug.Log("PrepareToCompleted =true;" + data.id);
                //        CompletedLoopIndex++;
                //    }

                //}

                //if (PrepareToCompleted)
                //{
                //    if (data.loop)
                //    {
                //        if (data.loopInfinite)
                //        {
                //            //ResetAudioClipPlayable();
                //            ToNextLoopPlay();
                //        }
                //        else
                //        {
                //            if (CompletedLoopIndex < data.loopTimes)
                //            {
                //                //ResetAudioClipPlayable();
                //                ToNextLoopPlay();
                //            }
                //            else
                //            {
                //                //不能采用问询的方法在父级中进行播放循环，因为在父级在下一帧才会监测到子级IsDone
                //                //这一帧的时间差会导致音频有个很微小的中断，这在进行无缝循环的音频播放时会听到刺耳的咔声
                //                //所以改为采用事件通知的形式，让父级的切换循环也在这一帧进行执行
                //                onPlayableDone?.Invoke(data.id);
                //            }
                //        }
                //    }
                //    else
                //    {
                //        //不能设置SetDone，会直接被回收
                //        onPlayableDone?.Invoke(data.id);
                //    }
                //}

                if (clipPlayable.GetTime() > clipPlayable.GetClip().length * (CompletedLoopIndex + 1))
                {
                    CompletedLoopIndex++;
                }

                if (clipPlayable.IsDone())
                {
                    PrepareToCompleted = true;
                    CompletedLoopIndex++;
                    onPlayableDone?.Invoke(data.id);
                }


                if (clipPlayable.IsDone())
                {
                    //Debug.Log("clipPlayable is done "+ data.name+"  "+clipPlayable.GetTime());
                    PrepareToCompleted = false;
                    //mixerPlayable.SetInputWeight(0, 0f);
                    //clipPlayable.SetLooped(false);

                    playable.SetDone(true);
                    playable.Pause();
                }
            }

            public override void OnBehaviourPause(Playable playable, FrameData info)
            {
                base.OnBehaviourPause(playable, info);
                //Debug.Log("soundSFXplayable pause  " + data.id);
                //如果是被父级暂停则说明不会被连续播放，应将PrepareToCompleted置false以下一次从头开始播放
                PrepareToCompleted = false;
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
                            if (clipPlayable.GetPlayState() != PlayState.Paused)
                            {
                                clipPlayable.Pause();
                                //Debug.Log("pause");
                            }
                            return true;
                        }
                        else
                        {
                            if (clipPlayable.GetPlayState() == PlayState.Paused)
                            {
                                clipPlayable.Play();
                                canDelay = false;
                                return false;
                            }
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
                return instance.IsDone();
            }

            private void ToNextLoopPlay()
            {
                //如果直接采用Init()则ResetAudioClipPlayable()中canDelay永远为false
                Init(false, false);

            }

            private void ResetAudioClipPlayable()
            {
                //如果有Delay的话也应重置时间
                //if (PrepareToCompleted && !(canDelay && data.delayTime > 0))
                //{
                //    Debug.Log("continue Play " + data.id);
                //    //在clipPlayable播放的任何时候去settime会导致有中断声
                //    //所以如果是在即将结束的时候调用Repaly，为保证连贯性应直接续播，保证无缝循环

                //    //用seek设置duration后setDurantion无效
                //    if (data.loop)
                //    {
                //        if (!data.loopInfinite)
                //        {
                //            clipPlayable.SetDuration(clipPlayable.GetDuration() + clipPlayable.GetClip().length * data.loopTimes);
                //        }
                //    }
                //    else
                //    {
                //        clipPlayable.SetDuration(clipPlayable.GetDuration() + clipPlayable.GetClip().length);
                //    }

                //    clipPlayable.SetLooped(true);
                //}
                //else
                //{
                //    Debug.Log($"Replay  {data.id}");
                //    //目前AudioClipPlayable有bug，会截掉0.03s左右的时间


                //    //clipPlayable.SetTime(-1*1/30f);
                //    //clipPlayable.SetDuration(clipPlayable.GetClip().length - 1 / 30f);

                //    if (data.loop)
                //    {
                //        if (data.loopInfinite)
                //        {
                //            clipPlayable.Seek(0, 1 / 30f);

                //        }
                //        else
                //        {
                //            clipPlayable.Seek(0, 1 / 30f, clipPlayable.GetClip().length * data.loopTimes);
                //            //clipPlayable.Seek(0, 1 / 30f);
                //            //clipPlayable.SetDuration(clipPlayable.GetClip().length * data.loopTimes);
                //        }
                //    }
                //    else
                //    {
                //        clipPlayable.Seek(0, 1 / 30f, clipPlayable.GetClip().length);

                //        //clipPlayable.SetDuration(clipPlayable.GetClip().length);

                //    }
                //}

                if (data.loop)
                {
                    if (data.loopInfinite)
                    {
#if UNITY_2019_4_OR_NEWER || UNITY_2020_3_OR_NEWER || UNITY_2021_1_OR_NEWER
                        clipPlayable.Seek(0, 0);
#else
                        clipPlayable.Seek(0, 1 / 30f);
#endif
                    }
                    else
                    {
#if UNITY_2019_4_OR_NEWER || UNITY_2020_3_OR_NEWER || UNITY_2021_1_OR_NEWER
                        //虽然seek有下面的问题，但是不使用seek的话duration的时间会对不上，导致末尾会泄露出一小段声音的情况
                        clipPlayable.Seek(0, 0, clipPlayable.GetClip().length * data.loopTimes);
#else
                        clipPlayable.Seek(0, 1 / 30f, clipPlayable.GetClip().length * data.loopTimes);
#endif

                        //clipPlayable.SetTime(-1 / 30f);
                        //clipPlayable.SetDuration(clipPlayable.GetClip().length * data.loopTimes - 1 / 30f);
                    }
                }
                else
                {
#if UNITY_2019_4_OR_NEWER || UNITY_2020_3_OR_NEWER || UNITY_2021_1_OR_NEWER
                    clipPlayable.SetTime(0f);
                    //clipPlayable.SetDuration(clipPlayable.GetClip().length);
#else
                    //如果使用seek来调整音频量，同一个音频都会在某种固定情况（暂时不知，必现）下只播放一段然后被切断直接完成的情况
                    //clipPlayable.Seek(0, 1 / 30f, clipPlayable.GetDuration());

                    //目前AudioClipPlayable有bug，会截掉0.03s左右的时间，一种方法是直接设置时间从-2帧开始播放，一种是使用seek延后2帧播放
                    clipPlayable.SetTime(-1 / 30f);
                    //clipPlayable.SetDuration(clipPlayable.GetClip().length);
#endif
                }


                PrepareToCompleted = false;

                clipPlayable.SetDone(false);
                clipPlayable.Play();

                //不能到下一帧才进行delay检测,会播放出一点声音，需要在replay的当前帧进行检测
                CheckDelay();
            }
        }
    }
}