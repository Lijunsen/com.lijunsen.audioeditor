using AudioEditor.Runtime.Utility.EasingCore;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    [Serializable]
    internal class BlendContainer : AEAudioComponent, IAEContainer
    {
        [SerializeField]
        private List<int> childrenId;
        [SerializeField]
        private ContainerPlayMode playMode;
        [SerializeField]
        internal List<BlendContainerTrack> blendContainerTrackList;

        public ContainerPlayMode PlayMode { get => playMode; set => playMode = value; }
        public List<int> ChildrenID { get => childrenId; set => childrenId = value; }

        public BlendContainer(string name, int id, AEComponentType type) : base(name, id, type)
        {
            childrenId = new List<int>();
            playMode = ContainerPlayMode.Step;
            blendContainerTrackList = new List<BlendContainerTrack>();
        }

        public override void Init()
        {
            base.Init();
        }

        public List<int> GetBlendContainerChildrenIDList()
        {
            return ChildrenID;
        }


    }

    [Serializable]
    internal class BlendContainerTrack
    {
        public int gameParameterId;
        /// <summary>
        /// 储存ChildBlendInfo的列表，其具有顺序，顺序为元素的中心坐标从小到大排列
        /// </summary>
        [SerializeField]
        private List<BlendContainerChildBlendInfo> childrenBlendInfoList;

        public int ChildrenBlendInfoListCount => childrenBlendInfoList.Count;

        public BlendContainerTrack(int gameParameterId)
        {
            this.gameParameterId = gameParameterId;
            childrenBlendInfoList = new List<BlendContainerChildBlendInfo>();
        }

        public bool AddNewChildBlendInfo(int childId, Vector2 xAxisPosition)
        {
            xAxisPosition.x = Mathf.Clamp01(xAxisPosition.x);
            var leftXAxisValue = 0f;
            var rightXAxisValue = 1f;
            //监测新插入的位置是否是在已经有的childBlendInfo之中，若有，则创建失败
            foreach (var childBlendInfo in childrenBlendInfoList)
            {
                if (xAxisPosition.x > childBlendInfo.LeftXAxisValue && xAxisPosition.x < childBlendInfo.RightXAxisValue)
                {
                    return false;
                }

                //确定新的BlendInfo的左右位置信息
                if (childBlendInfo.RightXAxisValue < xAxisPosition.x && childBlendInfo.RightXAxisValue > leftXAxisValue)
                {
                    leftXAxisValue = childBlendInfo.RightXAxisValue;
                }

                if (childBlendInfo.LeftXAxisValue > xAxisPosition.x && childBlendInfo.LeftXAxisValue < rightXAxisValue)
                {
                    rightXAxisValue = childBlendInfo.LeftXAxisValue;
                }
            }

            //检测空间是否过小
            if (rightXAxisValue - leftXAxisValue < BlendContainerChildBlendInfo.MinXAxisWeight)
            {
                AudioEditorDebugLog.Log("宽度过小");
                return false;
            }

            var newChildBlendInfo = new BlendContainerChildBlendInfo(childId);
            newChildBlendInfo.LeftXAxisValue = newChildBlendInfo.LeftBlendXAxisValue = leftXAxisValue;
            newChildBlendInfo.RightXAxisValue = newChildBlendInfo.RightBlendXAxisValue = rightXAxisValue;
            childrenBlendInfoList.Add(newChildBlendInfo);

            //每次插入新元素都进行一次顺序排序，保证中心坐标从小到大排列，后根据算法保证所有元素的中心坐标相对顺序
            childrenBlendInfoList.Sort((x, y) =>
            {
                var xCenterValue = (x.RightXAxisValue + x.LeftXAxisValue) / 2;
                var yCenterValue = (y.RightXAxisValue + y.LeftXAxisValue) / 2;
                if (xCenterValue > yCenterValue)
                {
                    return 1;
                }
                if (xCenterValue < yCenterValue)
                {
                    return -1;
                }
                return 0;
            });

            return true;
        }

        public BlendContainerChildBlendInfo GetChildBlendInfo(int index)
        {
            return childrenBlendInfoList[index];
        }

        public BlendContainerChildBlendInfo GetChildBlendInfoByChildID(int childId)
        {
            return childrenBlendInfoList.Find(x => x.childId == childId);
        }

        public void RemoveChildBlendInfo(int index)
        {
            childrenBlendInfoList.RemoveAt(index);
            //删除后对原先前后的两个BlendInfo进行数值检测，修正BlendXAxisValue的值
            DoChildBlendInfoTransform(index, 0);
            DoChildBlendInfoTransform(index - 1, 0);
        }

        /// <summary>
        /// 删除所有与ChildId相关的ChildBlendInfo
        /// </summary>
        /// <param name="id"></param>
        public void RemoveChildBlendInfoByChildID(int id)
        {
            while (childrenBlendInfoList.Exists(x => x.childId == id))
            {
                var index = childrenBlendInfoList.FindIndex(x => x.childId == id);
                childrenBlendInfoList.Remove(GetChildBlendInfoByChildID(id));
                DoChildBlendInfoTransform(index, 0);
                DoChildBlendInfoTransform(index - 1, 0);
            }
        }

        public void RemoveChildBlendInfo(BlendContainerChildBlendInfo childBlendInfo)
        {
            var index = childrenBlendInfoList.FindIndex(x => x == childBlendInfo);
            childrenBlendInfoList.Remove(childBlendInfo);
            DoChildBlendInfoTransform(index, 0);
            DoChildBlendInfoTransform(index - 1, 0);
        }

        public List<BlendContainerChildBlendInfo> FindChildBlendInfo(Predicate<BlendContainerChildBlendInfo> match)
        {
            return childrenBlendInfoList.FindAll(match);
        }

        /// <summary>
        /// 对单个BlendInfo进行整体位移，会进行越界检测和其他BlendInfo的边界交互检测
        /// </summary>
        /// <param name="childIndexInListOrder">单个BlendInfo在列表中的序列</param>
        /// <param name="deltaX">位移的增量，其值应该为[0,1]</param>
        public void DoChildBlendInfoTransform(int childIndexInListOrder, float deltaX)
        {
            if (childIndexInListOrder < 0 || childIndexInListOrder >= childrenBlendInfoList.Count) return;
            var childBlendInfo = childrenBlendInfoList[childIndexInListOrder];
            deltaX = DoCrossBorderDetection(childIndexInListOrder, deltaX, true);
            deltaX = DoCrossBorderDetection(childIndexInListOrder, deltaX, false);
            childBlendInfo.Transform(deltaX);

            CheckChildBlendXAxisValue(childIndexInListOrder);
        }

        /// <summary>
        /// 调整BlendInfo的左右边界
        /// </summary>
        /// <param name="childIndexInListOrder">BlendInfo在列表中的序列</param>
        /// <param name="newXAxisValue">新的数值</param>
        /// <param name="isLeft">true为左边界，false为右边界</param>
        public void ResizeChildBlendInfoRect(int childIndexInListOrder, float newXAxisValue, bool isLeft)
        {
            if (childIndexInListOrder < 0 || childIndexInListOrder >= childrenBlendInfoList.Count) return;
            var childBlendInfo = childrenBlendInfoList[childIndexInListOrder];
            var deltaX = 0f;
            if (isLeft)
            {
                deltaX = newXAxisValue - childBlendInfo.LeftXAxisValue;
                deltaX = DoCrossBorderDetection(childIndexInListOrder, deltaX, true);
                childBlendInfo.LeftXAxisValue += deltaX;
            }
            else
            {
                deltaX = newXAxisValue - childBlendInfo.RightXAxisValue;
                deltaX = DoCrossBorderDetection(childIndexInListOrder, deltaX, false);
                childBlendInfo.RightXAxisValue += deltaX;
            }

            CheckChildBlendXAxisValue(childIndexInListOrder);
        }

        /// <summary>
        /// 进行边界判定，并返回约束后的值
        /// </summary>
        /// <param name="childIndexInListOrder">进行检测的BlendInfo在Lsit中的序列</param>
        /// <param name="deltaX">想要移动的值，小于0为向左移动，大于0为向右移动</param>
        /// <param name="isLeft">true为左边界，false为右边界</param>
        /// <returns></returns>
        private float DoCrossBorderDetection(int childIndexInListOrder, float deltaX, bool isLeft)
        {
            var childBlendInfo = childrenBlendInfoList[childIndexInListOrder];
            if (deltaX > 0)
            {
                if (childIndexInListOrder + 1 < childrenBlendInfoList.Count)
                {
                    var nextChildBlendInfo1 = childrenBlendInfoList[childIndexInListOrder + 1];
                    if (isLeft)
                    {
                        //左边界不允许超过下一个ChildRect的左边界
                        if (childBlendInfo.LeftXAxisValue + deltaX > nextChildBlendInfo1.LeftXAxisValue)
                        {
                            deltaX = nextChildBlendInfo1.LeftXAxisValue - childBlendInfo.LeftXAxisValue;
                        }
                    }
                    else
                    {
                        //右边界不允许超过下一个ChildRect的右边界
                        if (childBlendInfo.RightXAxisValue + deltaX > nextChildBlendInfo1.RightXAxisValue)
                        {
                            deltaX = nextChildBlendInfo1.RightXAxisValue - childBlendInfo.RightXAxisValue;
                        }
                        //如果是相邻是相同的childId，不允许有交叉
                        if (childBlendInfo.childId == nextChildBlendInfo1.childId)
                        {
                            if (childBlendInfo.RightXAxisValue + deltaX > nextChildBlendInfo1.LeftXAxisValue)
                            {
                                deltaX = nextChildBlendInfo1.LeftXAxisValue - childBlendInfo.RightXAxisValue;
                            }
                        }
                    }
                }

                //右边界不允许超过下下个ChildRect的左边界
                if (childIndexInListOrder + 2 < childrenBlendInfoList.Count)
                {
                    if (isLeft == false)
                    {
                        var nextChildBlendInfo2 = childrenBlendInfoList[childIndexInListOrder + 2];
                        if (childBlendInfo.RightXAxisValue + deltaX > nextChildBlendInfo2.LeftXAxisValue)
                        {
                            deltaX = nextChildBlendInfo2.LeftXAxisValue - childBlendInfo.RightXAxisValue;
                        }
                    }
                }
            }
            else if (deltaX < 0)
            {
                if (childIndexInListOrder - 1 >= 0)
                {
                    var preChildBlendInfo1 = childrenBlendInfoList[childIndexInListOrder - 1];

                    if (isLeft == false)
                    {
                        //右边界不允许超过上一个ChildRect的右边界
                        if (childBlendInfo.RightXAxisValue + deltaX < preChildBlendInfo1.RightXAxisValue)
                        {
                            deltaX = preChildBlendInfo1.RightXAxisValue - childBlendInfo.RightXAxisValue;
                        }
                    }
                    else
                    {
                        //左边界不允许超过上一个ChildRect的左边界
                        if (childBlendInfo.LeftXAxisValue + deltaX < preChildBlendInfo1.LeftXAxisValue)
                        {
                            deltaX = preChildBlendInfo1.LeftXAxisValue - childBlendInfo.LeftXAxisValue;
                        }
                        //如果是相邻是相同的childId，不允许有交叉
                        if (childBlendInfo.childId == preChildBlendInfo1.childId)
                        {
                            if (childBlendInfo.LeftXAxisValue + deltaX < preChildBlendInfo1.RightXAxisValue)
                            {
                                deltaX = preChildBlendInfo1.RightXAxisValue - childBlendInfo.LeftXAxisValue;
                            }
                        }
                    }
                }

                //左边界不允许超过上上个ChildRect的右边界
                if (childIndexInListOrder - 2 >= 0)
                {
                    if (isLeft)
                    {
                        var preChildBlendInfo2 = childrenBlendInfoList[childIndexInListOrder - 2];
                        if (childBlendInfo.LeftXAxisValue + deltaX < preChildBlendInfo2.RightXAxisValue)
                        {
                            deltaX = preChildBlendInfo2.RightXAxisValue - childBlendInfo.LeftXAxisValue;
                        }
                    }
                }
            }

            return deltaX;
        }

        /// <summary>
        /// 对ChildBlendInfo的过渡边界进行检测修正
        /// </summary>
        /// <param name="childIndexInListOrder"></param>
        private void CheckChildBlendXAxisValue(int childIndexInListOrder)
        {
            var childBlendInfo = childrenBlendInfoList[childIndexInListOrder];
            //确定BlendXAxisValue的数值

            if (childIndexInListOrder - 1 >= 0)
            {
                if (childrenBlendInfoList[childIndexInListOrder - 1].RightXAxisValue >
                    childBlendInfo.LeftXAxisValue)
                {
                    childBlendInfo.LeftBlendXAxisValue = childrenBlendInfoList[childIndexInListOrder - 1].RightXAxisValue;
                    childrenBlendInfoList[childIndexInListOrder - 1].RightBlendXAxisValue = childBlendInfo.LeftXAxisValue;
                }
                else
                {
                    childBlendInfo.LeftBlendXAxisValue = childBlendInfo.LeftXAxisValue;
                    childrenBlendInfoList[childIndexInListOrder - 1].RightBlendXAxisValue =
                        childrenBlendInfoList[childIndexInListOrder - 1].RightXAxisValue;
                }
            }
            else
            {
                childBlendInfo.LeftBlendXAxisValue = childBlendInfo.LeftXAxisValue;
            }

            if (childIndexInListOrder + 1 < childrenBlendInfoList.Count)
            {
                if (childrenBlendInfoList[childIndexInListOrder + 1].LeftXAxisValue < childBlendInfo.RightXAxisValue)
                {
                    childBlendInfo.RightBlendXAxisValue = childrenBlendInfoList[childIndexInListOrder + 1].LeftXAxisValue;
                    childrenBlendInfoList[childIndexInListOrder + 1].LeftBlendXAxisValue = childBlendInfo.RightXAxisValue;
                }
                else
                {
                    childBlendInfo.RightBlendXAxisValue = childBlendInfo.RightXAxisValue;
                    childrenBlendInfoList[childIndexInListOrder + 1].LeftBlendXAxisValue =
                        childrenBlendInfoList[childIndexInListOrder + 1].LeftXAxisValue;
                }
            }
            else
            {
                childBlendInfo.RightBlendXAxisValue = childBlendInfo.RightXAxisValue;
            }
        }
    }

    [Serializable]
    internal class BlendContainerChildBlendInfo
    {
        public int childId;

        [SerializeField]
        private float leftXAxisValue = 0;
        [SerializeField]
        private float rightXAxisValue = 1;
        //淡变位置有两种方案，其数值可以表示在实际坐标轴上的位置，也可以表示在以左右值为边界的百分比，目前是设定为前者
        [SerializeField]
        private float leftBlendXAxisValue = 0;
        [SerializeField]
        private float rightBlendXAxisValue = 1;

        public EaseType leftEaseType = EaseType.Linear;
        public EaseType rightEaseType = EaseType.Linear;

        //设定最小间隔为0.005f
        public const float MinXAxisWeight = 0.05f;

        public float LeftXAxisValue
        {
            get => leftXAxisValue;
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;
                if (value > rightXAxisValue - MinXAxisWeight) value = rightXAxisValue - MinXAxisWeight;
                leftXAxisValue = value;
            }
        }

        public float RightXAxisValue
        {
            get => rightXAxisValue;
            set
            {
                if (value < 0) value = 0;
                if (value > 1) value = 1;
                if (value < leftXAxisValue + MinXAxisWeight) value = leftXAxisValue + MinXAxisWeight;
                rightXAxisValue = value;
            }
        }

        public float LeftBlendXAxisValue
        {
            get => leftBlendXAxisValue;
            set
            {
                if (value < leftXAxisValue)
                {
                    value = leftXAxisValue;
                }
                if (value > rightXAxisValue)
                {
                    value = rightXAxisValue;
                }
                leftBlendXAxisValue = value;
            }
        }

        public float RightBlendXAxisValue
        {
            get => rightBlendXAxisValue;
            set
            {
                if (value > rightXAxisValue)
                {
                    value = rightXAxisValue;
                }

                if (value < leftXAxisValue)
                {
                    value = leftXAxisValue;
                }

                rightBlendXAxisValue = value;
            }
        }

        public BlendContainerChildBlendInfo(int childId)
        {
            this.childId = childId;
        }

        /// <summary>
        /// 进行整体位移,会进行越界检测
        /// </summary>
        /// <param name="deltaX"></param>
        public void Transform(float deltaX)
        {
            if (LeftXAxisValue + deltaX < 0)
            {
                deltaX = 0 - LeftXAxisValue;
            }
            if (RightXAxisValue + deltaX > 1)
            {
                deltaX = 1 - RightXAxisValue;
            }

            if (deltaX > 0)
            {
                RightXAxisValue += deltaX;
                LeftXAxisValue += deltaX;
            }
            else
            {
                LeftXAxisValue += deltaX;
                RightXAxisValue += deltaX;
            }
        }

        /// <summary>
        /// 获取BlendInfo的y值
        /// </summary>
        /// <param name="xAxisValue">应传入GameParameter的归一化值</param>
        /// <returns></returns>
        public float GetValue(float xAxisValue)
        {
            var weight = 0f;
            if (xAxisValue >= LeftXAxisValue &&
                xAxisValue <= RightXAxisValue)
            {
                weight = 1f;
                if (LeftBlendXAxisValue > LeftXAxisValue && xAxisValue < LeftBlendXAxisValue)
                {
                    weight = Mathf.Clamp01(Easing.Get(leftEaseType)
                        .Invoke((xAxisValue - LeftXAxisValue) / (LeftBlendXAxisValue - LeftXAxisValue)));
                }

                if (RightBlendXAxisValue < RightXAxisValue && xAxisValue > RightBlendXAxisValue)
                {
                    weight = Mathf.Clamp01(Easing.Get(rightEaseType).Invoke((RightXAxisValue - xAxisValue) / (RightXAxisValue - RightBlendXAxisValue)));
                }
            }
            return weight;
        }
        /// <summary>
        /// 判断值是否在此blendInfo的区间内
        /// </summary>
        /// <returns></returns>
        public bool ContainXAixsValue(float xAxisValue)
        {
            if (xAxisValue >= LeftXAxisValue && xAxisValue <= RightXAxisValue) return true;
            return false;
        }
    }
}