using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    [System.Serializable]
    public class SwitchContainer : AEAudioComponent, AEContainer
    {
        [SerializeField]
        private ContainerPlayMode m_playMode = ContainerPlayMode.Step;
        [SerializeField]
        private List<int> m_childrenID;
        public int switchGroupID = -1;
        public int defualtPlayIndex;
        //<switchID,ChildID>
        public List<int> outputIDList;
        public SwitchContainer(string name, int id, AEComponentType type) : base(name, id, type)
        {
            m_childrenID = new List<int>();
            outputIDList = new List<int>();
        }

        public List<int> GetSwitchContainerChildrenIDList()
        {
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

        public List<int> ChildrenID { get => m_childrenID; set => m_childrenID = value; }
        public ContainerPlayMode PlayMode { get => m_playMode; set => m_playMode = value; }
    }

}