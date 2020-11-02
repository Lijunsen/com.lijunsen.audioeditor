using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{

    [Serializable]
    public class RTPC
    {
        [SerializeField]
        public int targetGameParameterID = -1;

        public RTPC()
        {

        }

        public RTPC(int id)
        {
            targetGameParameterID = id;
        }

        public void SetGlobalVlue(float newValue)
        {
            if (targetGameParameterID == -1)
            {
                Debug.LogWarning("未设置GameParameterID");
            }
            else
            {
                AudioEditorManager.SetGameParameter(targetGameParameterID, newValue);
            }
        }
    }
}