using AudioEditor.Runtime.Utility.EasingCore;
using System.Collections.Generic;
using UnityEngine;

namespace AudioEditor.Runtime.Utility
{
    public enum AEEventType
    {
        Play = 0,
        Pause,
        PauseAll,
        Resume,
        ResumeAll,
        Stop,
        StopAll,
        SetSwitch,
        SetState,
        InstallBank,
        UnInstallBank,
        //将SequencePlayable实例往前一位播放，仅对SequencePlayable有效
        //SetToPreviousStep
    }

    public enum AEEventScope
    {
        GameObject,
        Global
    }

    public enum AEEventTriggerType
    {
        Instance,
        Delay,
        Trigger
    }

    public enum AEEventTriggerOccasion
    {
        NextBar,
        NextBeat
    }

    [System.Serializable]
    internal class AEEvent : AEComponent
    {
        public List<AEEventUnit> eventList;

        public AEEvent(string name, int id) : base(name, id)
        {
            eventList = new List<AEEventUnit>();
        }


        public void NewEventUnit()
        {
            eventList.Add(new AEEventUnit());
        }


    }

    /// <summary>
    /// 具体的一个执行事项
    /// </summary>
    [System.Serializable]
    internal class AEEventUnit
    {
        public AEEventType type;
        public int targetID;
        public int probability = 100;
        public AEEventScope scope = AEEventScope.GameObject;
        [SerializeField]
        private float fadeTime = 0f;
        public EaseType fadeType = EaseType.Linear;

        public AEEventTriggerType triggerType;
        /// <summary>
        /// 用于记录延迟触发的时间或者对齐触发的TargetAudioComponentId
        /// </summary>
        public float triggerData;
        public AEEventTriggerOccasion triggerOccasion;

        public float FadeTime
        {
            get => fadeTime;
            set
            {
                if (value < 0)
                {
                    value = 0;
                }

                fadeTime = value;
            }
        }

        public AEEventUnit()
        {
            type = AEEventType.Play;
            targetID = -1;
            probability = 100;
            scope = AEEventScope.GameObject;
        }

        public AEEventUnit(AEEventType type, int targetId)
        {
            this.type = type;
            this.targetID = targetId;
            probability = 100;
            scope = AEEventScope.GameObject;
        }

        public bool SimulationProbability()
        {
            var randomNumber = Random.value;
            //Debug.Log("randomNumber:" + randomNumber+",probability:"+ (float)probability / 100);
            if (randomNumber <= (float)probability / 100)
            {
                return true;
            }
            return false;
        }
    }
}
