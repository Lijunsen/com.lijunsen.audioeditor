using AudioEditor.Runtime;
using UnityEditor;
using UnityEngine;

namespace AudioEditor.Editor.Utility
{
    internal class ExportAudioEvent : UnityEditor.Editor
    {
        private static string exportStr = "";
        //private static string ExportFilePath = "";

        // [MenuItem("Window/Audio Editor/导出EventList",false,13)]
        private static void ExportAudioEventList()
        {
            exportStr = "";
            var data = AssetDatabase.LoadAssetAtPath(
                "Assets/AudioEditor/Data/Resources/AuioEditorDataAsset.asset", typeof(AudioEditorData)) as AudioEditorData;
            if (data == null)
            {
                Debug.LogError("无法找到Asset Data");
                return;
            }

            foreach (var aeEvent in data.eventData)
            {
                exportStr += aeEvent.name;
                exportStr += "\n";
            }

            exportStr += "\n";
            exportStr += "\n";

            foreach (var aeEvent in data.eventData)
            {
                exportStr += aeEvent.id;
                exportStr += "\n";
            }

            System.IO.File.WriteAllText(Application.dataPath + @"\AudioEditor\Export\EventList.txt", exportStr);
            exportStr = "";
            Debug.Log("导出完成");
        }
    }
}