using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ypzxAudioEditor.Utility
{
    public class ExportAudioEvent:Editor
    {
        private static string ExportStr = "";
        //private static string ExportFilePath = "";

       // [MenuItem("Window/Audio Editor/导出EventList",false,13)]
        private static void ExportAudioEventList()
        {
            ExportStr = "";
            var data = AssetDatabase.LoadAssetAtPath(
                "Assets/AudioEditor/Data/Resources/AuioEditorDataAsset.asset",typeof(AudioEditorData)) as AudioEditorData;
            if (data == null)
            {
                Debug.LogError("无法找到Asset Data");
                return;
            }

            foreach (var aeEvent in data.EventData)
            {
                ExportStr += aeEvent.name;
                ExportStr += "\n";
            }

            ExportStr += "\n";
            ExportStr += "\n";

            foreach (var aeEvent in data.EventData)
            {
                ExportStr += aeEvent.id;
                ExportStr += "\n";
            }

            System.IO.File.WriteAllText(Application.dataPath + @"\AudioEditor\Export\EventList.txt", ExportStr);
            ExportStr = "";
            Debug.Log("导出完成");
        }
    }
}