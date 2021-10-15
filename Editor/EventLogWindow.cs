using AudioEditor.Runtime;
using AudioEditor.Runtime.Utility;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AudioEditor.Editor
{

    internal class EventLogWindow : EditorWindow
    {
        //private static EventLogWindow window;
        private static List<string> eventLogList = new List<string>();
        private static string searchString = "";
        private SearchField searchField;
        private Rect scrollViewPosition = Rect.zero;
        private Rect scrollViewRect = Rect.zero;
        private Vector2 scrollPosition = Vector2.zero;
        private float actuallyScrollViewHeight = 0f;
        private static bool prepareToDownPage = false;
        private float boxHeight = 0f;

        [MenuItem("Window/Audio Editor/EventLog", false, 12)]
        public static EventLogWindow GetWindow()
        {
            var window = (EventLogWindow)GetWindow(typeof(EventLogWindow), false, "AudioEditor EventLog");
            return window;
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AudioEditorManager.EventTriggered -= OnEventTriggered;
            AudioEditorManager.EventUnitTriggered -= OnEventActionTriggered;
            AudioEditorManager.PlayableGenerated -= OnPlayableGenerated;
            AudioEditorManager.PlayableDestroyed -= OnPlayableDestroyed;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AudioEditorManager.EventTriggered += OnEventTriggered;
            AudioEditorManager.EventUnitTriggered += OnEventActionTriggered;
            AudioEditorManager.PlayableGenerated += OnPlayableGenerated;
            AudioEditorManager.PlayableDestroyed += OnPlayableDestroyed;
            eventLogList = new List<string>();
            searchField = new SearchField();
            searchString = "";
            prepareToDownPage = false;
        }

        void OnGUI()
        {
            if (AudioEditorManager.FindManager() == null)
            {
                EditorGUILayout.HelpBox("场景中无法找到AudioEditorManager", MessageType.Error);
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
            {
                eventLogList.Clear();
            }
            GUILayout.FlexibleSpace();
            if (searchField == null) searchField = new SearchField();
            searchString = searchField.OnToolbarGUI(searchString);
            GUILayout.EndHorizontal();
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Color.gray);

            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);

            List<string> tempLogList;
            if (searchString != "")
            {
                tempLogList = eventLogList.FindAll(x => x.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > 0);
            }
            else
            {
                tempLogList = eventLogList;
            }
            if (Event.current.type == EventType.Layout)
            {
                actuallyScrollViewHeight = Math.Abs(scrollViewPosition.height);
                scrollViewRect.width = scrollViewPosition.width - GUI.skin.verticalScrollbar.fixedWidth;
                scrollViewRect.height = (boxHeight + EditorGUIUtility.standardVerticalSpacing * 2) * tempLogList.Count;
            }

            GUI.SetNextControlName("EventLog Background Area");
            scrollViewPosition = EditorGUILayout.GetControlRect(true, GUILayout.ExpandHeight(true));
            scrollPosition = GUI.BeginScrollView(scrollViewPosition, scrollPosition, scrollViewRect);
            GUILayout.BeginArea(scrollViewRect);
            for (int i = 0; i < tempLogList.Count; i++)
            {
                using (new GUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField(tempLogList[i]);
                }

                if (Event.current.type == EventType.Repaint && i == tempLogList.Count - 1)
                {
                    boxHeight = GUILayoutUtility.GetLastRect().height;
                }
            }
            GUILayout.EndArea();

            GUI.EndScrollView();

            if (prepareToDownPage == true)
            {
                scrollPosition.y = scrollViewRect.height - actuallyScrollViewHeight;
                prepareToDownPage = false;
            }

            if (scrollViewPosition.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
            {
                GUI.FocusControl("EventLog Background Area");
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredPlayMode)
            {
                eventLogList.Clear();
            }
        }

        private void OnEventTriggered(AEEvent applyEvent, GameObject eventAttachObject)
        {
            var gameObjectName = eventAttachObject != null ? eventAttachObject.name : "null";
            eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]事件触发，name：{applyEvent.name}，id：{applyEvent.id}，GameObject：{gameObjectName}");
            if (Math.Abs(Math.Abs(Mathf.Abs(scrollPosition.y - scrollViewRect.height)) - actuallyScrollViewHeight) < 1 || (scrollPosition.y == 0 && scrollViewRect.height < actuallyScrollViewHeight))
            {
                prepareToDownPage = true;
            }
            Repaint();
        }

        private void OnEventActionTriggered(AEEventType? eventType, bool isOverride, string targetName, int targetId, string targetType, GameObject eventAttachObject)
        {
            var overrideTag = isOverride ? "(覆盖动作)" : "";
            var gameObjectName = eventAttachObject != null ? eventAttachObject.name : "null";
            eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]  执行动作：{eventType}{overrideTag}，[ 对象：{targetName}，id：{targetId}，GameObejct:{gameObjectName} ]");
            if (Math.Abs(Math.Abs(Mathf.Abs(scrollPosition.y - scrollViewRect.height)) - actuallyScrollViewHeight) < 1 || (scrollPosition.y == 0 && scrollViewRect.height < actuallyScrollViewHeight))
            {
                prepareToDownPage = true;
            }
            Repaint();
        }



        private void OnPlayableGenerated(string playableName, int playableId, GameObject gameObject)
        {
            var gameObjectName = gameObject != null ? gameObject.name : "null";
            eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]      生成实例：{playableName}，Id：{playableId}，GameObject：{gameObjectName}");
            if (Math.Abs(Math.Abs(Mathf.Abs(scrollPosition.y - scrollViewRect.height)) - actuallyScrollViewHeight) < 1 || (scrollPosition.y == 0 && scrollViewRect.height < actuallyScrollViewHeight))
            {
                prepareToDownPage = true;
            }
            Repaint();
        }

        private void OnPlayableDestroyed(string playableName, int playableId, GameObject gameObject)
        {
            var gameObjectName = gameObject != null ? gameObject.name : "null";
            eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]      实例结束播放，销毁实例：{playableName}，Id：{playableId}，GameObject：{gameObjectName}");
            if (Math.Abs(Math.Abs(Mathf.Abs(scrollPosition.y - scrollViewRect.height)) - actuallyScrollViewHeight) < 1 || (scrollPosition.y == 0 && scrollViewRect.height < actuallyScrollViewHeight))
            {
                prepareToDownPage = true;
            }
            Repaint();
        }

        public static void ShowLog(string info)
        {
            if (eventLogList != null) eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}] {info}");
        }

        void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AudioEditorManager.EventTriggered -= OnEventTriggered;
            AudioEditorManager.EventUnitTriggered -= OnEventActionTriggered;
            AudioEditorManager.PlayableGenerated -= OnPlayableGenerated;
            AudioEditorManager.PlayableDestroyed -= OnPlayableDestroyed;

            searchString = null;
            eventLogList.Clear();
        }
    }
}
