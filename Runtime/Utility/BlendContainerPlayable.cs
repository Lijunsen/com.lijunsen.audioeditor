using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Playables;

namespace ypzxAudioEditor.Utility
{
    public class BlendContainerPlayable : AudioEditorPlayable
    {

        List<AudioEditorPlayable> childrenPlayable;
        public BlendContainerPlayable(string name, int id, AEComponentType type, ref PlayableGraph graph) : base(name, id, type, ref graph)
        {
        }

        protected override AudioEditorPlayableBehaviour PlayableBehaviour { get; }
        protected override AudioMixerPlayable MixerPlayable { get; set; }
        public override AEAudioComponent Data { get; }
        public override void Stop()
        {
            throw new System.NotImplementedException();
        }

        public override bool IsDone()
        {
            throw new System.NotImplementedException();
        }

        protected override void SetScriptPlayableSpeed(double value)
        {
            throw new System.NotImplementedException();
        }

        protected override void SetScriptPlayableWeight(float newValue)
        {
            throw new System.NotImplementedException();
        }

        public override void GetTheLastTimePlayState(ref double playTime, ref double durationTime)
        {
            throw new System.NotImplementedException();
        }

        public override void GetAudioClips(ref List<AudioClip> clips)
        {
            throw new System.NotImplementedException();
        }

        public override bool Relate(int id)
        {
            if (base.Relate(id)) return true;
            if (childrenPlayable.FindIndex(x => x.id == id) >= 0) return true;
            return false;
        }
    }

    public class BlendContainerPlayableBehavior : AudioEditorPlayableBehaviour
    {
        public override void GetTheLastTimePlayStateAsChild(ref double playTime, ref double durationTime)
        {
            throw new System.NotImplementedException();
        }
    }
}