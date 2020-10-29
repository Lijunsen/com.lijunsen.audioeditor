using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace ypzxAudioEditor.Utility
{
    public class EmptyPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<EmptyPlayableBehaviour> emptyScriptPlayable;
        public EmptyPlayable(ref PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource) : base("EmptyPlayable",-1,AEComponentType.Empty,ref graph)
        {
            emptyScriptPlayable = ScriptPlayable<EmptyPlayableBehaviour>.Create(graph);
            MixerPlayable = AudioMixerPlayable.Create(graph);
            emptyScriptPlayable.AddInput(MixerPlayable, 0, 1);
            MixerPlayable.SetDone(true);

            Init(emptyScriptPlayable,eventAttachObject,audioSource);
        }

        public ScriptPlayable<EmptyPlayableBehaviour> GetScriptPlayable()
        {
            return emptyScriptPlayable;
        }

        protected override AudioEditorPlayableBehaviour PlayableBehaviour => emptyScriptPlayable.GetBehaviour();

        protected override AudioMixerPlayable MixerPlayable
        {
            get => emptyScriptPlayable.GetBehaviour().mixerPlayable;
            set => emptyScriptPlayable.GetBehaviour().mixerPlayable = value;
        }

        public override AEAudioComponent Data { get => null;  }

        public override void Stop()
        {

        }

        public override bool IsDone()
        {
            return true;
        }

        public override void Destroy()
        {
            base.Destroy();
            // if (m_Graph.IsValid())
            // {
            //     m_Graph.DestroyPlayable(MixerPlayable);
            //     m_Graph.DestroyPlayable(emptyScriptPlayable);
            // }
            m_Graph.DestroySubgraph(emptyScriptPlayable);
            if (playableOutput.IsOutputValid())
            {
                playableOutput.SetTarget(null);
                playableOutput.SetUserData(null);
                m_Graph.DestroyOutput(playableOutput);
                m_Graph.Stop();
                m_Graph.Destroy();
            }
        }


        protected override void SetScriptPlayableWeight(float newValue)
        {
            emptyScriptPlayable.SetInputWeight(MixerPlayable,newValue);
        }

        public override void GetTheLastTimePlayState(ref double playTime, ref double durationTime)
        {
            playTime = durationTime = -1;
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            
        }

        protected override void SetScriptPlayableSpeed(double value)
        {
        }

        public class EmptyPlayableBehaviour : AudioEditorPlayableBehaviour
        {

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                mixerPlayable.Pause();
                mixerPlayable.SetDone(true);
                base.PrepareFrame(playable, info);
            }

            public override void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime)
            {
                playTime = -1;
                durationTime = -1;
            }
        }
    }
}