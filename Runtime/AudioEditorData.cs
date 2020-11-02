using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{
    [System.Serializable]
    public class AudioEditorData : ScriptableObject
    {
        public bool StaticallyLinkedAudioClips = true;

        [SerializeReference]
        public List<AEAudioComponent> AudioComponentData = new List<AEAudioComponent>();

        public List<AEEvent> EventData = new List<AEEvent>();

        [SerializeReference]
        public List<AEGameSyncs> GameSyncsData = new List<AEGameSyncs>();

    }
}
