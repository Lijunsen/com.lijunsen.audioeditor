using AudioEditor.Runtime.Utility;
using System;
using UnityEngine;

namespace AudioEditor.Runtime
{
    [Serializable]
    public class EventReference
    {
        [SerializeField]
        public int targetEventId = -1;
        [NonSerialized]
        private bool overrideEventType = false;
        [SerializeField]
        private AEEventType? whichEventTypeOverride = null;

        public EventReference()
        {
        }

        public EventReference(int id)
        {
            targetEventId = id;
        }

        public void PostEvent(GameObject gameObj, Action completeCallBack = null)
        {
            if (targetEventId == -1)
            {
                //  Debug.LogWarning("未设置AudioEventReference ID");
            }
            else
            {
                if (AudioEditorManager.FindManager() == null)
                {
                    AudioEditorDebugLog.LogError("无法获取AudioEditorManager");
                    return;
                }
                AudioEditorManager.ApplyEvent(gameObj, targetEventId, whichEventTypeOverride, completeCallBack);
            }
            if (overrideEventType == false)
            {
                whichEventTypeOverride = null;
            }
        }

        public void OverrideEventType(AEEventType whichType)
        {
            overrideEventType = true;
            whichEventTypeOverride = whichType;
        }

        public void OverrideEventTypeOnce(AEEventType whichType)
        {
            whichEventTypeOverride = whichType;
        }

        /// <summary>
        /// 通过事件Id来触发事件
        /// </summary>
        /// <param name="gameObject">触发事件指定的GameObejct</param>
        /// <param name="eventId">需要触发事件的Id</param>
        /// <param name="overrideEventType">需要替换的动作</param>
        /// <param name="completeCallBack">动作完成后的回调，仅Play事件会调用</param>
        public static void PostEvent(GameObject gameObject, int eventId, AEEventType? overrideEventType = null, Action completeCallBack = null)
        {

            if (AudioEditorManager.FindManager() == null)
            {
                AudioEditorDebugLog.LogError("无法获取AudioEditorManager"); 
                return;
            }
            AudioEditorManager.ApplyEvent(gameObject, eventId, overrideEventType, completeCallBack);
        }

        /// <summary>
        /// 通过事件名称来触发事件
        /// </summary>
        /// <param name="gameObject">触发事件指定的GameObejct</param>
        /// <param name="eventName">需要触发事件的名称</param>
        /// <param name="overrideEventType">需要替换的动作</param>
        /// <param name="completeCallBack">动作完成后的回调，仅Play事件会调用</param>
        public static void PostEvent(GameObject gameObject, string eventName, AEEventType? overrideEventType = null, Action completeCallBack = null)
        {
            if (AudioEditorManager.FindManager() == null)
            {
                AudioEditorDebugLog.LogError("无法获取AudioEditorManager");
                return;
            }
            AudioEditorManager.ApplyEvent(gameObject, eventName, overrideEventType, completeCallBack);
        }
    }
}