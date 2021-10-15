using AudioEditor.Runtime.Utility;
using System;
using UnityEngine;

namespace AudioEditor.Runtime
{

    [Serializable]
    public class RTPC
    {
        [SerializeField]
        public int targetGameParameterId = -1;

        public RTPC()
        {

        }

        public RTPC(int id)
        {
            targetGameParameterId = id;
        }

        public void SetGlobalVlue(float newValue)
        {
            if (targetGameParameterId == -1)
            {
                Debug.LogWarning("未设置GameParameterID");
            }
            else
            {
                AudioEditorManager.SetGameParameter(targetGameParameterId, newValue);
            }
        }

        /// <summary>
        /// 通过GameparameterId关联RTPC中的属性
        /// </summary>
        /// <param name="gameParameterId">对应的GameparameterId</param>
        /// <param name="newValue">需要同步的数值</param>
        public static void PostGameParameterValue(int gameParameterId, float newValue)
        {
            if (AudioEditorManager.FindManager() == null)
            {
                AudioEditorDebugLog.LogError("无法获取AudioEditorManager");
                return;
            }
            AudioEditorManager.SetGameParameter(gameParameterId, newValue);
        }

        /// <summary>
        /// 通过GameParameterName关联RTPC中的属性
        /// </summary>
        /// <param name="gameParameterName">对应的GameparameterName</param>
        /// <param name="newValue">需要同步的数值</param>
        public static void PostGameParameterValue(string gameParameterName, float newValue)
        {

            if (AudioEditorManager.FindManager() == null)
            {
                AudioEditorDebugLog.LogError("无法获取AudioEditorManager");
                return;
            }
            AudioEditorManager.SetGameParameter(gameParameterName, newValue);
        }
    }
}