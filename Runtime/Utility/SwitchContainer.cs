using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    [System.Serializable]
    internal class SwitchContainer : AEAudioComponent, IAEContainer
    {
        [SerializeField]
        private ContainerPlayMode playMode = ContainerPlayMode.Step;
        [SerializeField]
        private List<int> childrenID;
        public int switchGroupID = -1;
        public int defualtPlayIndex;
        //<switchID,ChildID>
        public List<int> outputIDList;

        public bool crossFade = false;
        public float crossFadeTime = 0;
        public CrossFadeType crossFadeType = CrossFadeType.Linear;

        public List<int> ChildrenID { get => childrenID; set => childrenID = value; }
        public ContainerPlayMode PlayMode { get => playMode; set => playMode = value; }

        public SwitchContainer(string name, int id, AEComponentType type) : base(name, id, type)
        {
            childrenID = new List<int>();
            outputIDList = new List<int>();
        }

        public List<int> GetSwitchContainerOutputIDList()
        {
            //SwitchContainer实例仅生成在GroupSetting中设置的子类
            return outputIDList;
        }

        /// <summary>
        /// 当有子项拖动出去时，修改OutputIDList中的相应数据
        /// </summary>
        /// <param name="dragOutIDList"></param>
        public void ResetOutputIDList(List<int> dragOutIDList)
        {
            for (var i = 0; i < outputIDList.Count; i++)
            {
                foreach (var dragOutID in dragOutIDList)
                {
                    if (outputIDList[i] == dragOutID)
                    {
                        outputIDList[i] = -1;
                    }
                }
            }
        }
    }

}