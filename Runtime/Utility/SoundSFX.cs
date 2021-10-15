using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    // public enum FadeType
    // {
    //     Linear,
    //     Sin,
    // }

    [System.Serializable]
    internal class SoundSFX : AEAudioComponent
    {
        public AudioClip clip;
        public string clipGUID;
        public string clipAssetPath;

        // [System.NonSerialized]
        // public GameObject gameObject;

        // public bool isplaying;

        public SoundSFX(string name, int id, AEComponentType type) : base(name, id, type)
        {
            clipGUID = "";
            clipAssetPath = "";
        }

    }
}
