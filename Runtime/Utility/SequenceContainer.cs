using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    [System.Serializable]
    internal class SequenceContainer : AEAudioComponent, IAEContainer
    {
        public SequenceContainerPlayType playType = SequenceContainerPlayType.Restart;
        public bool alwaysResetPlayList = true;
        private int currentStepPtr = 0;
        private int nextStepPtr;
        [NonSerialized]
        private bool reverse;
        [SerializeField]
        private List<int> childrenId;
        [SerializeField]
        private ContainerPlayMode playMode = ContainerPlayMode.Step;

        public bool crossFade;
        public float crossFadeTime = 0;
        public CrossFadeType crossFadeType = CrossFadeType.Linear;

        public List<int> ChildrenID { get => childrenId; set => childrenId = value; }
        public ContainerPlayMode PlayMode { get => playMode; set => playMode = value; }

        private int NextStepPtr
        {
            get => nextStepPtr;
            set
            {
                if (value < 0)
                {
                    value += ChildrenID.Count;
                }

                if (value >= ChildrenID.Count)
                {
                    value -= ChildrenID.Count;
                }
                nextStepPtr = value;
            }
        }

        public SequenceContainer(string name, int id, AEComponentType type) : base(name, id, type)
        {
            childrenId = new List<int>();
        }

        public override void Init()
        {
            currentStepPtr = 0;
            nextStepPtr = 0;
            reverse = false;
        }

        public List<int> GetSequenceContainerChildrenIDList()
        {
            if (ChildrenID.Count == 0)
            {
                return ChildrenID;
            }

            if (nextStepPtr >= ChildrenID.Count || nextStepPtr < 0)
            {
                Init();
            }

            currentStepPtr = nextStepPtr;

            //记录当次列表的末位
            var tempPtr = 0;
            List<int> tempList = new List<int>();
            switch (PlayMode)
            {
                //step只播放某位，所以直接获取顺序列表
                case ContainerPlayMode.Step:
                    //int j = stepPtr;
                    //for (int i = 0; i < ChildrenID.Count;i++)
                    //{
                    //    tempList.Add(ChildrenID[j]);
                    //    j ++;
                    //    if (j == ChildrenID.Count)
                    //    {
                    //        j = 0;
                    //    }
                    //}
                    tempList.Add(ChildrenID[currentStepPtr]);
                    break;
                case ContainerPlayMode.Continuous:
                    //AlwaysResetPlayList下continuous只从首位或末尾开始播放
                    if (alwaysResetPlayList)
                    {
                        if (currentStepPtr != 0 && currentStepPtr != ChildrenID.Count - 1)
                        {
                            currentStepPtr = 0;
                        }
                    }
                    tempPtr = currentStepPtr;
                    for (int i = 0; i < ChildrenID.Count; i++)
                    {
                        tempList.Add(ChildrenID[tempPtr]);
                        if (i == ChildrenID.Count - 1)
                        {
                            break;
                        }

                        switch (playType)
                        {
                            case SequenceContainerPlayType.Restart:
                                tempPtr++;
                                if (tempPtr == ChildrenID.Count) tempPtr = 0;
                                break;
                            case SequenceContainerPlayType.ReverseOrder:

                                tempPtr += reverse ? -1 : 1;
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
            //对步进指针进行移位
            switch (playType)
            {
                case SequenceContainerPlayType.Restart:
                    nextStepPtr++;
                    //alwaysResetPlayList情况下一直输出123 123 123
                    if (alwaysResetPlayList && PlayMode == ContainerPlayMode.Continuous)
                    {
                        nextStepPtr = 0;
                    }
                    if (nextStepPtr == -1 || nextStepPtr == ChildrenID.Count)
                    {
                        nextStepPtr = 0;
                    }

                    //stepPtr += reverse ? -1 : 1;
                    //if (alwaysResetPlayList && PlayMode == ContainerPlayMode.Continuous)
                    //{
                    //    stepPtr = 0;
                    //}
                    //if (stepPtr == ChildrenID.Count)
                    //{
                    //    stepPtr = 0;
                    //}
                    //if (reverse && stepPtr == -1)
                    //{
                    //    stepPtr = ChildrenID.Count - 1;
                    //}

                    break;
                case SequenceContainerPlayType.ReverseOrder:
                    if (PlayMode == ContainerPlayMode.Continuous)
                    {
                        nextStepPtr = tempPtr;
                    }
                    nextStepPtr += reverse ? -1 : 1;
                    if (nextStepPtr >= ChildrenID.Count)
                    {
                        nextStepPtr = alwaysResetPlayList ? ChildrenID.Count - 1 : ChildrenID.Count - 2;
                        reverse = true;
                    }
                    if (nextStepPtr <= -1)
                    {
                        nextStepPtr = alwaysResetPlayList ? 0 : 1;
                        reverse = false;
                    }
                    break;
            }
            return tempList;
        }

        /// <summary>
        /// 将标志位设回当前步进位的上一位，下一次播放将从当前的上一首开始
        /// </summary>
        public void SetToPlayPreviousChild()
        {
            //if (NextStepPtr == 0)
            //{
            //    NextStepPtr = ChildrenID.Count - 1;
            //}
            //else
            //{
            //    //因为一首播放时已将标志位下移，要回到当前的上一首需要-2
            //    NextStepPtr -= 2;
            //}

            NextStepPtr = currentStepPtr - 1;
            Debug.Log(NextStepPtr);
        }
    }
}
