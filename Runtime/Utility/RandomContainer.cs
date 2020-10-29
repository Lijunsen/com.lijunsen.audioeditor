using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;


namespace ypzxAudioEditor.Utility
{
    [System.Serializable]
    public class RandomContainer : AEAudioComponent, AEContainer
    {
        public RandomContainerPlayType playType = RandomContainerPlayType.Standard;
        public int shufflePtr = 0;
        [SerializeField]
        private List<int> m_childrenID;
        [SerializeField]
        private ContainerPlayMode m_playMode = ContainerPlayMode.Step;
        /// <summary>
        /// 保存每次Shuffle乱序后的ChildrenIDList，用于下次Shuffle
        /// </summary>
        [System.NonSerialized]
        private List<int> shuffledID;

        public bool crossFade = false;
        public float crossFadeTime = 0;
        public CrossFadeType crossFadeType = CrossFadeType.Linear;

        public RandomContainer(string name, int id, AEComponentType type):base(name,id,type)
        {
            m_childrenID = new List<int>();
            shuffledID = new List<int>();
        }

        public override void Init()
        {
            shufflePtr = m_childrenID.Count;
            shuffledID = new List<int>();
          //  shuffledID.AddRange(ChildrenID);
        }

        /// <summary>
        /// 实现标准乱序和洗牌乱序，每次生成Playable都会调用并乱序ChildrenList
        /// </summary>
        public List<int> GetRandomContainerChildrenIDList(List<int> preChildrenIdList)
        {
            if (m_childrenID.Count == 0)
            {
                return m_childrenID;
            }

            if (shuffledID.Count != ChildrenID.Count)
            {
                shuffledID.Clear();
                shufflePtr = m_childrenID.Count;
            }

            System.Random random = new System.Random(System.DateTime.Now.Second);
            List<int> tempList = new List<int>();

            switch (playType)
            {
                case RandomContainerPlayType.Standard:
                    switch (PlayMode)
                    {
                        case ContainerPlayMode.Step:
                            var randomNumber = random.Next(ChildrenID.Count);
                            tempList.Add(m_childrenID[randomNumber]);
                            break;
                        case ContainerPlayMode.Continuous:
                            foreach (var item in m_childrenID)
                            {
                                //random.Next return the value of (>=0 && <maxValue);
                                tempList.Insert(random.Next(tempList.Count + 1), item);
                            }
                            break;
                    }
                    break;
                case RandomContainerPlayType.Shuffle:
                    switch (PlayMode)
                    {
                        case ContainerPlayMode.Step:
                            //初次洗牌随机为完全随机，后续随机为从shuffledID中1到末尾随机抽取一位
                            var randomNumber = -1;
                            if (shuffledID.Count == 0)
                            {
                                shuffledID.AddRange(ChildrenID);
                                randomNumber = random.Next(shuffledID.Count);
                                tempList.Add(shuffledID[randomNumber]);
                            }
                            else
                            {
                                //目前固定1次洗牌
                                if (shuffledID.Count == 1)
                                {
                                    //如果只有子类只有1，则直接取首位
                                    randomNumber = 0;
                                }
                                else
                                {
                                    randomNumber = random.Next(1, shuffledID.Count);
                                }
                                tempList.Add(shuffledID[randomNumber]);
                            }
                            //随机出来的位放到首位，保证第一位都是正在播放的
                            var playID = shuffledID[randomNumber];
                            shuffledID.RemoveAt(randomNumber);
                            shuffledID.Insert(0, playID);
                            break;
                        case ContainerPlayMode.Continuous:
                            // //初次洗牌完全随机
                            // if (shuffledID.Count == 0)
                            // {
                            //     shuffledID.AddRange(ChildrenID);
                            //     foreach (var item in shuffledID)
                            //     {
                            //         //random.Next return the value of (>=0 && <maxValue);
                            //         tempList.Insert(random.Next(tempList.Count + 1), item);
                            //     }
                            //     shuffledID.Clear();
                            //     shuffledID.AddRange(tempList);
                            // }
                            // else
                            // {
                            //     if (shufflePtr == 1)
                            //     {
                            //         shufflePtr = shuffledID.Count;
                            //     }
                            //     //将元素分为已播放和未播放两组，分别执行乱序
                            //     List<int> unplayedList = new List<int>();
                            //     List<int> playedLIst = new List<int>();
                            //     for (int i = 1; i < shufflePtr; i++)
                            //     {
                            //         unplayedList.Add(shuffledID[i]);
                            //     }
                            //     for (int i = shufflePtr; i < shuffledID.Count; i++)
                            //     {
                            //         playedLIst.Add(shuffledID[i]);
                            //     }
                            //     //将第一首放入已播放组中
                            //     playedLIst.Add(shuffledID[0]);
                            //     //未播放乱序
                            //     tempList.Clear();
                            //     foreach (var item in unplayedList)
                            //     {
                            //         tempList.Insert(random.Next(tempList.Count + 1), item);
                            //     }
                            //     unplayedList.Clear();
                            //     unplayedList.InsertRange(0, tempList);
                            //     //已播放乱序
                            //     tempList.Clear();
                            //     foreach (var item in playedLIst)
                            //     {
                            //         tempList.Insert(random.Next(tempList.Count + 1), item);
                            //     }
                            //     playedLIst.Clear();
                            //     playedLIst.InsertRange(0, tempList);
                            //     //整合入结果中
                            //     tempList.Clear();
                            //     tempList.AddRange(unplayedList);
                            //     tempList.AddRange(playedLIst);
                            //     shuffledID.Clear();
                            //     shuffledID.AddRange(tempList);
                            //     shufflePtr--;
                            //
                            // }

                            if (preChildrenIdList.Count == 0)
                            {
                                //实例初次生成
                                foreach (var item in ChildrenID)
                                {
                                    //random.Next return the value of (>=0 && <maxValue);
                                    tempList.Insert(random.Next(tempList.Count + 1), item);
                                }
                                shuffledID.Clear();
                                shuffledID.AddRange(tempList);
                            }
                            else
                            {
                                if (preChildrenIdList.Count == 1)
                                {
                                    tempList.AddRange(preChildrenIdList);
                                }
                                else
                                {
                                    //将除了末位的元素进行随机排位
                                    for (int i = 0; i < preChildrenIdList.Count - 1; i++)
                                    {
                                        tempList.Insert(random.Next(tempList.Count + 1), preChildrenIdList[i]);
                                    }
                                    //末位的元素随机排入非首位位置，保证前后循环不重复
                                    tempList.Insert(random.Next(1, tempList.Count), preChildrenIdList.Last());
                                }
                            }
                            break;
                    }
                    break;
            }
            return tempList;
        }

        public List<int> ChildrenID { get => m_childrenID; set => m_childrenID = value; }

        public ContainerPlayMode PlayMode { get => m_playMode; set => m_playMode=value; }
    }

    public enum CrossFadeType
    {
        Linear,
        Delay
    }
}
