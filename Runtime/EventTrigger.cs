using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ypzxAudioEditor
{
    public enum MyEventTriggerType
    {
        Start,
        TriggerEnter,
        TriggerExit,
        Disable,
        Destroy
    }
    [Serializable]
    [AddComponentMenu("AudioEditor/EventTrigger")]
    public class EventTrigger : MonoBehaviour
    {

        [SerializeField, HideInInspector]
        public GameObject colliderTarget;

        [SerializeField, HideInInspector]
        public int triggerTypes;

       // [FormerlySerializedAs("targetEventIDaa")]
        [SerializeField,HideInInspector]
        public int targetEventID = -1;

        [SerializeField,HideInInspector]
        public string eventName = "null";

        // public EventTrigger(int i)
        // {
        //     targetEventID = i;
        // }

        // Start is called before the first frame update
        void Awake()
        {
            //if (!gameObject.GetComponent<AudioSource>())
            //{
            //    gameObject.AddComponent<AudioSource>();
            //}
        }

        void Start()
        {
            CheckTrigger(MyEventTriggerType.Start);
        }

        // Update is called once per frame
        void Update()
        {
            
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
            for (int i = 0; i < Enum.GetNames(typeof(MyEventTriggerType)).Length; i++)
            {
                var triggerOption = triggerTypes &(1 << i);
             //   Debug.Log(Convert.ToString( triggerOption,2));
                if (triggerOption.Equals(1 << i) && i == (int) type)
                {
                    PostEvent();
                    break;
                }
            }
        }

        public void PostEvent()
        {
            if (AudioEditorManager.FindManager() == null)
            {
                Debug.LogError("[AudioEditor]:无法获取AudioEditorManager");
                return;
            }
            AudioEditorManager.ApplyEvent(this.gameObject, targetEventID);
        }

    }
}
