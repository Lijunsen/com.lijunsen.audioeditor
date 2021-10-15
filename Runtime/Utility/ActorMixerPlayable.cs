using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal class ActorMixerPlayable : AudioEditorPlayable
    {
        public ScriptPlayable<ActorMixerPlayableBehaviour> actorMixerPlayable;

        public override AudioEditorPlayableBehaviour PlayableBehaviour => actorMixerPlayable.GetBehaviour();

        public override AEAudioComponent Data => actorMixerPlayable.GetBehaviour()?.Data;

        protected override AudioMixerPlayable MixerPlayable
        {
            get
            {
                if (actorMixerPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return actorMixerPlayable.GetBehaviour().mixerPlayable;
            }
        }

        public ActorMixerPlayable(ActorMixer data, ref PlayableGraph graph,AudioEditorPlayable childPlayable) : base(data.name, data.id, data.unitType, ref graph)
        {
            var actorMixerPlayableBehaviour = new ActorMixerPlayableBehaviour(data,childPlayable);
            actorMixerPlayable = ScriptPlayable<ActorMixerPlayableBehaviour>.Create(graph,actorMixerPlayableBehaviour);
            
            Init();
        }
        
        public override bool IsDone()
        {
            return false;
        }

        public override void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            Debug.LogWarning("不应获取Actor的状态");
            playTime = actorMixerPlayable.GetTime();
            durationTime = actorMixerPlayable.GetDuration();
            isTheLastTimePlay = false;
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
        }

        [Obsolete]
        public void AddMixerInput(Playable playable)
        {
            if (MixerPlayable.GetInputCount() > 0)
            {
                AudioEditorDebugLog.LogWarning("检测到ActorMixer接入多个输入");
            }

            MixerPlayable.AddInput(playable, MixerPlayable.GetInputCount(), 1);
        }


        public class ActorMixerPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            private readonly ActorMixer data;
            private readonly AudioEditorPlayable childPlayable;
            public override AEAudioComponent Data { get => data; }

            public ActorMixerPlayableBehaviour()
            {

            }

            public ActorMixerPlayableBehaviour(ActorMixer data,AudioEditorPlayable childPlayable)
            {
                this.data = data;
                this.childPlayable = childPlayable;
            }

            public override void OnPlayableCreate(Playable playable)
            {

                var graph = playable.GetGraph();
                
                mixerPlayable = AudioMixerPlayable.Create(graph);

                playable.AddInput(mixerPlayable, 0, 1);

                mixerPlayable.AddInput(childPlayable.PlayableBehaviour.instance,0,1);

                base.OnPlayableCreate(playable);
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);

                CheckCommonFadeIn(data, 0);
                
            }

            public override bool JudgeDone()
            {
                Debug.LogWarning("不应检测Actor的完成状态");
                return false;
            }
        }
    }


}
