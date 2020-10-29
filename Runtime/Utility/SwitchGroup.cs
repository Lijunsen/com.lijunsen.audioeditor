using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    [System.Serializable]
    public class SwitchGroup : AEGameSyncs
    {
        public int currentSwitchID;
        [SerializeField]
        private List<Switch> switchList;

        public SwitchGroup(string name, int id) : base(name, id)
        {
            currentSwitchID = -1;
            switchList = new List<Switch>();
        }

        public override void Init()
        {
            currentSwitchID = -1;
        }

        public Switch GenerateSwitch(string name, int newSwitchID)
        {
            //var newSwitchName = string.Format("New_Switch_" + newSwitchID.ToString());
            switchList.Add(new Switch(name, newSwitchID));
            return switchList[SwitchListCount - 1];
        }

        public Switch FindSwitch(int switchID)
        {
            return switchList.Find((x) => x.id == switchID);
        }

        public Switch FindSwitchAt(int index)
        {
            return switchList[index];
        }

        public int FindSwitchIndex(int switchID)
        {
            return switchList.FindIndex(x => x.id == switchID);
        }

        public bool RemoveSwitch(int switchID)
        {
            var mySwitch = switchList.Find((x) => x.id == switchID);
            if (mySwitch != null)
            {
                switchList.Remove(mySwitch);
                return true;
            }
            return false;
        }


        public bool RemoveSwitchAt(int i)
        {
            if (i < 0 || i >= SwitchListCount)
            {
                Debug.LogError("传入非法参数");
                return false;
            }
            switchList.RemoveAt(i);
            return true;
        }


        public bool ContainsSwitch(int switchID)
        {
            if (switchList.Find((x) => x.id == switchID) == null)
            {
                return false;
            }
            return true;
        }

        // public Switch this[int i] => switchList[i];

        public int SwitchListCount => switchList.Count;

        // public List<Switch> SwitchList
        // {
        //     get => switchList;
        // }
    }

    [System.Serializable]
    public class Switch : AEGameSyncs
    {
        public Switch(string name, int id) : base(name, id)
        {

        }
    }

}