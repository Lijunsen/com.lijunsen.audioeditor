using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Audio;

namespace ypzxAudioEditor.Utility
{
    public class SoundSFXPlayable : AudioEditorPlayable
    {
        // private double pauseTime;
        //private SoundSFX data;
        private ScriptPlayable<SoundSFXPlayableBehaviour> soundSFXScriptPlayable;

        public SoundSFXPlayable(SoundSFX data, ref PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, bool isChild) : base(data.name, data.id, data.unitType, ref graph)
        {
            soundSFXScriptPlayable = ScriptPlayable<SoundSFXPlayableBehaviour>.Create(m_Graph);
            soundSFXScriptPlayable.GetBehaviour().SetData(data, isChild);

            ClipPlayable = AudioClipPlayable.Create(m_Graph, data.clip, false);
            MixerPlayable = AudioMixerPlayable.Create(m_Graph);
            MixerPlayable.AddInput(ClipPlayable, 0, 1);

            soundSFXScriptPlayable.AddInput(MixerPlayable, 0, 1);

            Init(soundSFXScriptPlayable, eventAttachObject, audioSource);
        }

        public ScriptPlayable<SoundSFXPlayableBehaviour> GetScriptPlayable()
        {
            return soundSFXScriptPlayable;
        }


        protected override void SetScriptPlayableSpeed(double value)
        {
            soundSFXScriptPlayable.SetSpeed(value);
        }

        protected override void SetScriptPlayableWeight(float newValue)
        {
            soundSFXScriptPlayable.SetInputWeight(MixerPlayable, newValue);
        }

        public override void GetTheLastTimePlayState(ref double playTime, ref double durationTime)
        {
            soundSFXScriptPlayable.GetBehaviour().GetTheLastTimePlayStateAsChild(ref playTime, ref durationTime);
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            clips.Add((Data as SoundSFX).clip);
        }

        public override void Stop()
        {
            Pause();
            soundSFXScriptPlayable.SetDone(true);
            //if fadeOut
            //   MixerPlayable.SetDone(true);
        }

        public override void Replay()
        {
            soundSFXScriptPlayable.GetBehaviour().Init();
            soundSFXScriptPlayable.SetDone(false);
            soundSFXScriptPlayable.Play();
            base.Replay();
        }

        public override bool IsDone()
        {
            if (soundSFXScriptPlayable.GetBehaviour() == null) return true;
            if (soundSFXScriptPlayable.IsDone()) return true;
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
            m_Graph.DestroySubgraph(soundSFXScriptPlayable);
            if (playableOutput.IsOutputValid())
            {
                playableOutput.SetTarget(null);
                playableOutput.SetUserData(null);
                m_Graph.DestroyOutput(playableOutput);
                m_Graph.Stop();
                m_Graph.Destroy();
            }
        }

        protected override AudioEditorPlayableBehaviour PlayableBehaviour => soundSFXScriptPlayable.GetBehaviour();

        protected override AudioMixerPlayable MixerPlayable
        {
            get => soundSFXScriptPlayable.GetBehaviour().mixerPlayable;
            set => soundSFXScriptPlayable.GetBehaviour().mixerPlayable = value;
        }

        public override AEAudioComponent Data { get => soundSFXScriptPlayable.GetBehaviour().data; }

        public AudioClipPlayable ClipPlayable
        {
            get => soundSFXScriptPlayable.GetBehaviour().clipPlayable;
            set => soundSFXScriptPlayable.GetBehaviour().clipPlayable = value;
        }

        public class SoundSFXPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            public SoundSFX data;
            public AudioClipPlayable clipPlayable;

            public override void Init()
            {
                base.Init();
                ResetAudioClipPlayable();
            }

            public override void OnPlayableCreate(Playable playable)
            {
                base.OnPlayableCreate(playable);
            }

            public override void OnBehaviourPlay(Playable playable, FrameData info)
            {
                base.OnBehaviourPlay(playable, info);
                Init();
            }

            public override void OnGraphStart(Playable playable)
            {
                base.OnGraphStart(playable);
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                // Debug.Log(playable.GetTime());
                base.PrepareFrame(playable, info);
                CheckDelay();
                CheckFadeIn(data, ref mixerPlayable, 0, mixerPlayable.GetTime());
                CheckFadeOut(data, ref mixerPlayable, 0, mixerPlayable.GetTime(), clipPlayable.GetDuration());

                if (clipPlayable.IsDone() && data.loop)
                {
                    if (data.loopInfinite)
                    {
                        //Debug.Log("Infinite Play");
                        mixerPlayable.SetTime(0f);
                        ResetAudioClipPlayable();
                    }
                    else if (loopIndex > 0)
                    {
                        loopIndex--;
                        //Debug.Log(loopIndex);
                        mixerPlayable.SetTime(0f);
                        ResetAudioClipPlayable();
                    }
                }
            }

            protected override bool CheckDelay()
            {
                if (!base.CheckDelay()) return false;
                if (data.delayTime > 0 && mixerPlayable.GetTime() < data.delayTime)
                {
                    clipPlayable.Pause();
                    //Debug.Log("pause");
                    return true;
                }
                else
                {
                    if (clipPlayable.GetPlayState() == PlayState.Paused)
                    {
                        ResetAudioClipPlayable();
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
                return clipPlayable.IsDone();
            }

            public override void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime)
            {
                if (data.loop)
                {
                    if (loopIndex == 1)
                    {
                        playTime = clipPlayable.GetTime();
                        durationTime = clipPlayable.GetDuration();
                        return;
                    }
                }
                else
                {
                    playTime = clipPlayable.GetTime();
                    durationTime = clipPlayable.GetDuration();
                    return;
                }
                playTime = durationTime = -1;
            }

            public override void SetData(AEAudioComponent data, bool isChild, List<AudioEditorPlayable> childrenPlayables = null)
            {
                base.SetData(data, isChild, childrenPlayables);
                this.data = data as SoundSFX;
                loopIndex = data.loopTimes;
            }

            private void ResetAudioClipPlayable()
            {
                //目前AudioClipPlayable有bug，会截掉0.03s左右的时间
                // clipPlayable.Seek(-0.03f,0,data.clip.length+0.03f);
                clipPlayable.SetTime(-0.03f);
                clipPlayable.SetDone(false);
                clipPlayable.Play();
            }

        }
    }
}