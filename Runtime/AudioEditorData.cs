using AudioEditor.Runtime.Utility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime
{
    [System.Serializable]
    public class AudioEditorData : ScriptableObject
    {
        [SerializeReference]
        internal List<AEAudioComponent> audioComponentData = new List<AEAudioComponent>();

        [SerializeReference]
        internal List<AEEvent> eventData = new List<AEEvent>();

        [SerializeReference]
        internal List<AEGameSyncs> gameSyncsData = new List<AEGameSyncs>();

        [SerializeReference]
        internal MyTreeLists myTreeLists = new MyTreeLists();

        internal static AudioEditorData CreateDefaultData()
        {
            var newData = ScriptableObject.CreateInstance<AudioEditorData>();
            for (int i = 0; i < MyTreeLists.TreeListCount; i++)
            {
                newData.myTreeLists[i].Add(new MyTreeElement("root", -1, 0));
                newData.myTreeLists[i].Add(new MyTreeElement("Work Unit_1", 0, 1));
            }
            return newData;
        }
    }


    [Serializable]
    internal class MyTreeLists
    {
        [SerializeReference]
        private List<MyTreeElement> audioTreeList = new List<MyTreeElement>();
        [SerializeReference]
        private List<MyTreeElement> eventTreeList = new List<MyTreeElement>();
        [SerializeReference]
        private List<MyTreeElement> gameSyncTreeList = new List<MyTreeElement>();
        //Bank功能暂时隐藏
        //[SerializeReference]
        //private List<MyTreeElement> bankTreeList = new List<MyTreeElement>();

        public static int TreeListCount => Enum.GetNames(typeof(AudioEditorDataType)).Length;

        public List<MyTreeElement> this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0:
                        return audioTreeList;
                    case 1:
                        return eventTreeList;
                    case 2:
                        return gameSyncTreeList;
                    //case 3:
                    //    return bankTreeList;
                    default:
                        Debug.LogError("读取列表树失败，请检查");
                        return null;
                }
            }
            set
            {
                switch (i)
                {
                    case 0:
                        audioTreeList = value;
                        break;
                    case 1:
                        eventTreeList = value;
                        break;
                    case 2:
                        gameSyncTreeList = value;
                        break;
                    //case 3:
                    //    bankTreeList = value;
                    //    break;
                    default:
                        Debug.LogError("储存列表树失败，请检查");
                        break;
                }
            }
        }
    }

    internal enum AudioEditorDataType
    {
        Audio,
        Events,
        GameSyncs,
        //Banks
    }

}
