using UnityEngine;
using UnityEngine.EventSystems;

namespace AudioEditor.Runtime.Utility
{
    internal static class AudioEditorDebugLog
    {
        public static void Log(object str)
        {
            Debug.Log("[AudioEditor]: " + str);
        }

        public static void LogWarning(object str)
        {
            Debug.LogWarning("[AudioEditor]: " + str);
        }

        public static void LogError(object str)
        {
            Debug.LogError("[AudioEditor]:" + str);
        }
    }

    internal class EventLog : IEventSystemHandler
    {

    }
}
