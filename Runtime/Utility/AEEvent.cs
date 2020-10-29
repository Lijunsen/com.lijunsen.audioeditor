using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
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
    }

    public enum Scope
    {
        GameObject,
        Global
    }

    [System.Serializable]
     public  class AEEvent:AEComponent
    {
        public List<AEEventUnit> eventList;

        public AEEvent(string name,int id):base(name,id)
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
    public class AEEventUnit
    {
        public AEEventType type;
        public int targetID;
        public int probability = 100;
        public Scope scope = Scope.GameObject;
        public AEEventUnit()
        {
            type = AEEventType.Play;
            targetID = -1;
            probability = 100;
            scope = Scope.GameObject;
        }

        public AEEventUnit(AEEventType type, int targetId)
        {
            this.type = type;
            this.targetID = targetId;
            probability = 100;
            scope = Scope.GameObject;
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
