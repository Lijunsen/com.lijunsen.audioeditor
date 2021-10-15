using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace AudioEditor.Runtime.Utility
{
    internal class EmptyPlayable : AudioEditorPlayable
    {
        private ScriptPlayable<EmptyPlayableBehaviour> emptyScriptPlayable;

        public override AEAudioComponent Data => null;
        public override AudioEditorPlayableBehaviour PlayableBehaviour => emptyScriptPlayable.GetBehaviour();

        protected override AudioMixerPlayable MixerPlayable
        {
            get
            {
                if (emptyScriptPlayable.GetBehaviour() == null)
                {
                    AudioEditorDebugLog.LogWarning("Playable is null");
                    return default;
                }
                return emptyScriptPlayable.GetBehaviour().mixerPlayable;
            }
        }

        public EmptyPlayable(ref PlayableGraph graph, GameObject eventAttachObject, AudioSource audioSource, int id = -1) : base("EmptyPlayable", id, AEComponentType.Empty, ref graph)
        {
            emptyScriptPlayable = ScriptPlayable<EmptyPlayableBehaviour>.Create(graph);
            emptyScriptPlayable.GetBehaviour().id = id;
            Init();
        }

        public ScriptPlayable<EmptyPlayableBehaviour> GetScriptPlayable()
        {
            return emptyScriptPlayable;
        }

        public override bool IsDone()
        {
            return true;
        }

        public override void Destroy()
        {
            base.Destroy();
            graph.DestroySubgraph(emptyScriptPlayable);
        }

        public override void GetPlayState(out double playTime, out double durationTime, out bool isTheLastTimePlay)
        {
            playTime = durationTime = 0;
            isTheLastTimePlay = false;
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {

        }


        public class EmptyPlayableBehaviour : AudioEditorPlayableBehaviour
        {
            public int id;

            public override AEAudioComponent Data { get => null; }

            public override void OnPlayableCreate(Playable playable)
            {
                var graph = playable.GetGraph();
                mixerPlayable = AudioMixerPlayable.Create(graph);
                playable.AddInput(mixerPlayable, 0, 1);

                base.OnPlayableCreate(playable);
            }

            public override void PrepareFrame(Playable playable, FrameData info)
            {
                base.PrepareFrame(playable, info);
                if (mixerPlayable.IsValid() == false) return;

                if (playable.GetPlayState() == PlayState.Playing || playable.IsDone() == false)
                {
                    onPlayableDone?.Invoke(id);
                    playable.SetDone(true);
                    playable.Pause();
                }
            }

            public override bool JudgeDone()
            {
                return true;
            }

        }
    }
}