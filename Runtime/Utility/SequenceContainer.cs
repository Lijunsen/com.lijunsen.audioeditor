using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    [System.Serializable]
    public class SequenceContainer : AEAudioComponent, AEContainer
    {
        public SequenceContainerPlayType playType = SequenceContainerPlayType.Restart;
        //avoid repeate
        public bool alwaysResetPlayList = true;
        private int stepPtr = 0;
        private bool reverse = false;
        [SerializeField]
        private List<int> m_childrenID;
        [SerializeField]
        public ContainerPlayMode m_playMode = ContainerPlayMode.Step;


        public bool crossFade;
        public float crossFadeTime = 0;
        public CrossFadeType CrossFadeType = CrossFadeType.Linear;

        public SequenceContainer(string name, int id, AEComponentType type) : base(name, id, type)
        {
            m_childrenID = new List<int>();
        }

        public override void Init()
        {
            stepPtr = 0;
            reverse = false;
        }

        public List<int> GetSequenceContainerChildrenIDList()
        {
            //目前step输出仍为全输出，应改成只输出一位childID
            if (ChildrenID.Count == 0)
            {
                return ChildrenID;
            }
            //记录当次列表的末位
            var tempPtr = 0;
            List<int> tempList = new List<int>();
            switch (PlayMode)
            {
                //step只播放某位，所以直接获取顺序列表
                case ContainerPlayMode.Step:
                    int j = stepPtr;
                    for (int i = 0; i < ChildrenID.Count;i++)
                    {
                        tempList.Add(ChildrenID[j]);
                        j ++;
                        if (j == ChildrenID.Count)
                        {
                            j = 0;
                        }
                    }
                    break;
                case ContainerPlayMode.Continuous:
                    //AlwaysResetPlayList下continuous只从首位或末尾开始播放
                    if (alwaysResetPlayList)
                    {
                        if (stepPtr != 0 && stepPtr != ChildrenID.Count - 1)
                        {
                            stepPtr = 0;
                        }
                    }
                    tempPtr = stepPtr;
                    for (int i = 0; i < ChildrenID.Count; i++)
                    {
                        tempList.Add(ChildrenID[tempPtr]);
                        if(i == ChildrenID.Count - 1)
                        {
                            break;
                        }
                        tempPtr += reverse ? -1 : 1;

                        switch (playType)
                        {
                            case SequenceContainerPlayType.Restart:
                                if (tempPtr == ChildrenID.Count) tempPtr = 0;
                                break;
                            case SequenceContainerPlayType.ReverseOrder:
                                if (tempPtr == ChildrenID.Count || tempPtr == -1)
                                {
                                    tempPtr += !reverse ? -2 : 2;
                                    reverse = !reverse;
                                }
                                break;
                        }
                    }
                    break;
            }
            switch (playType)
            {
                case SequenceContainerPlayType.Restart:
                    stepPtr += reverse ? -1 : 1;
                    if (alwaysResetPlayList && PlayMode == ContainerPlayMode.Continuous)
                    {
                        stepPtr = 0;
                    }
                    if (stepPtr == ChildrenID.Count)
                    {
                        stepPtr = 0;
                    }
                    break;
                case SequenceContainerPlayType.ReverseOrder:
                    if (PlayMode == ContainerPlayMode.Continuous)
                    {
                        stepPtr = tempPtr;
                    }
                    stepPtr += reverse ? -1 : 1;
                    if (stepPtr >= ChildrenID.Count)
                    {
                        stepPtr = alwaysResetPlayList ? ChildrenID.Count-1 : ChildrenID.Count-2;
                        reverse = true;
                    }
                    if (stepPtr <= -1)
                    {
                        stepPtr = alwaysResetPlayList ? 0 : 1;
                        reverse = false;
                    }
                    break;
            }
            return tempList;
        }
        public List<int> ChildrenID { get => m_childrenID; set => m_childrenID = value; }
        public ContainerPlayMode PlayMode { get => m_playMode; set => m_playMode = value; }
    }
}
