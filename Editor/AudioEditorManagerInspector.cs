using AudioEditor.Runtime;
using UnityEditor;
using UnityEngine;

namespace AudioEditor.Editor
{
    [CustomEditor(typeof(AudioEditorManager))]
    internal class AudioEditorManagerInspector : UnityEditor.Editor
    {
        // private SerializedProperty m_data;
        private AudioEditorManager manager;

        private void OnEnable()
        {
            manager = target as AudioEditorManager;
            if (null == manager)
            {
                Debug.LogError("Manager Script is null");
                return;
            }
        }

        public override void OnInspectorGUI()
        {
            // base.OnInspectorGUI();
            // if (GUILayout.Button("保存数据"))
            // {
            //     AudioEditorManager.m_Graph.Play();
            // }
            // EditorGUILayout.BeginHorizontal();
            // EditorGUILayout.PrefixLabel(new GUIContent("当前生成的音频实例："));
            // EditorGUILayout.LabelField(manager.PlayableList.Count.ToString());
            // EditorGUILayout.EndHorizontal();
        }

    }
}
