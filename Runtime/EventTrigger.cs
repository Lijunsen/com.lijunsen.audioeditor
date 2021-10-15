using AudioEditor.Runtime.Utility;
using System;
using UnityEngine;

namespace AudioEditor.Runtime
{
    [Flags]
    internal enum MyEventTriggerType
    {
        Start = 0b00001,
        TriggerEnter = 0b00010,
        TriggerExit = 0b00100,
        Disable = 0b01000,
        Destroy = 0b10000
    }

    [Serializable]
    [AddComponentMenu("AudioEditor/EventTrigger")]
    public class EventTrigger : MonoBehaviour
    {

        [SerializeField, HideInInspector]
        public GameObject colliderTarget;

        [SerializeField, HideInInspector]
        public int triggerTypes;

        [SerializeField, HideInInspector]
        public int targetEventID = -1;

        [SerializeField, HideInInspector]
        public string eventName = "null";
        

        void Start()
        {
            CheckTrigger(MyEventTriggerType.Start);
        }
        
        private void OnTriggerEnter(Collider other)
        {

            if (other.gameObject == colliderTarget)
            {
                // Debug.Log("Collider Enter");
                CheckTrigger(MyEventTriggerType.TriggerEnter);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject == colliderTarget)
            {
                //Debug.Log("Collider Exit");
                CheckTrigger(MyEventTriggerType.TriggerExit);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {

        }

        private void OnCollisionExit(Collision collision)
        {

        }

        void OnDisable()
        {
            CheckTrigger(MyEventTriggerType.Disable);
        }

        void OnDestroy()
        {
            CheckTrigger(MyEventTriggerType.Destroy);
        }

        /// <summary>
        /// 检测触发类型
        /// </summary>
        /// <param name="type"></param>
        private void CheckTrigger(MyEventTriggerType type)
        {
            //for (int i = 0; i < Enum.GetNames(typeof(MyEventTriggerType)).Length; i++)
            //{
            //    //将选项掩码与当前选项进行按位与，提取出对应位的数值
            //    var triggerOption = triggerTypes & (1 << i);
            //    //Debug.Log(Convert.ToString( triggerOption,2));
            //    if (triggerOption.Equals(1 << i) && i == (int)type)
            //    {
            //        //对应位的数值为1且i为要检测的类型则触发
            //        PostEvent();
            //        break;
            //    }
            //}

            if ((triggerTypes & (int)type) != 0)
            {
                PostEvent();
            }
        }

        public void PostEvent()
        {
            PostEvent(null);
        }


        internal void PostEvent(AEEventType? overrideEventType)
        {
            if (AudioEditorManager.FindManager() == null)
            {
                Debug.LogError("[AudioEditor]:无法获取AudioEditorManager");
                return;
            }
            AudioEditorManager.ApplyEvent(this.gameObject, targetEventID, overrideEventType);
        }

        
    }
}
