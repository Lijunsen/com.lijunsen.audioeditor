using System;
using UnityEngine;

namespace AudioEditor.Runtime
{
    [Obsolete]
    //实例化全局设置数据
    [System.Serializable]
    public class AudioEditorGlobalSettingData : ScriptableObject
    {
        public bool staticallyLinkedAudioClips = true;
        public bool autoLoadDataAseet = true;
    }
}
