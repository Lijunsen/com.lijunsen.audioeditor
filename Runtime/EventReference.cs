using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{
    [Serializable]
    public class EventReference
    {
        [SerializeField]
        public int targetEventID = -1;
        [NonSerialized]
        private bool overrideEventType = false;
        [SerializeField]
        private AEEventType? whichEventTypeOverride = null;

        public EventReference()
        {
        }

        public EventReference(int id)
        {
            targetEventID = id;
        }

        public void PostEvent(GameObject gameObj)
        {
            if (targetEventID == -1)
            {
              //  Debug.LogWarning("未设置AudioEventReference ID");
            }
            else
            {
                if (AudioEditorManager.FindManager() == null){
                      Debug.LogError("[AudioEditor]:无法获取AudioEditorManager");
                      return;
                }
                AudioEditorManager.ApplyEvent(gameObj, targetEventID, whichEventTypeOverride);
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
    }
}