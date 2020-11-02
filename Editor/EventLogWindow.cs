using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using ypzxAudioEditor.Utility;

namespace ypzxAudioEditor
{

    public class EventLogWindow : EditorWindow
    {
        //private static EventLogWindow window;
        private static List<string> _eventLogList = new List<string>();
        private static string _searchString = "";
        private SearchField _searchField;
        private Rect _scrollViewPosition = Rect.zero;
        private Rect _scrollViewRect = Rect.zero;
        private Vector2 _scrollPosition = Vector2.zero;
        private float _actuallyScrollViewHeight = 0f;
        private static bool _prepareToDownPage = false;
        private float _boxHeight = 0f;

        [MenuItem("Window/Audio Editor/EventLog",false,12)]
        public static EventLogWindow GetWindow()
        {
            var window =  (EventLogWindow)GetWindow(typeof(EventLogWindow),false,"AudioEditor EventLog");
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
            _eventLogList = new List<string>();
            _searchField = new SearchField();
            _searchString = "";
            _prepareToDownPage = false;
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
                _eventLogList.Clear();
            }
            GUILayout.FlexibleSpace();
            if (_searchField == null) _searchField = new SearchField();
            _searchString = _searchField.OnToolbarGUI(_searchString);
            GUILayout.EndHorizontal();
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Color.gray);
            
            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            
            List<string> tempLogList;
            if (_searchString != "")
            {
                tempLogList  = _eventLogList.FindAll(x => x.IndexOf(_searchString,StringComparison.OrdinalIgnoreCase)>0);
            }
            else
            {
                tempLogList = _eventLogList;
            }
            if (Event.current.type == EventType.Layout)
            {
                _actuallyScrollViewHeight = Math.Abs(_scrollViewPosition.height);
                _scrollViewRect.width = _scrollViewPosition.width-GUI.skin.verticalScrollbar.fixedWidth;
                _scrollViewRect.height = (_boxHeight + EditorGUIUtility.standardVerticalSpacing * 2) * tempLogList.Count;
            }

            GUI.SetNextControlName("EventLog Background Area");
            _scrollViewPosition = EditorGUILayout.GetControlRect(true,GUILayout.ExpandHeight(true));
            _scrollPosition = GUI.BeginScrollView(_scrollViewPosition,_scrollPosition,_scrollViewRect);
            GUILayout.BeginArea(_scrollViewRect);
            for (int i = 0; i < tempLogList.Count; i++)
            {
                using (new GUILayout.HorizontalScope("box"))
                {
                    EditorGUILayout.LabelField(tempLogList[i]);
                }
            
                if (Event.current.type == EventType.Repaint && i ==tempLogList.Count-1)
                {
                    _boxHeight = GUILayoutUtility.GetLastRect().height;
                }
            }
            GUILayout.EndArea();
            
            GUI.EndScrollView();
            
            if (_prepareToDownPage == true)
            {
                _scrollPosition.y = _scrollViewRect.height - _actuallyScrollViewHeight;
                _prepareToDownPage = false;
            }

            if (_scrollViewPosition.Contains(Event.current.mousePosition) && Event.current.type == EventType.MouseDown)
            {
                GUI.FocusControl("EventLog Background Area");
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange obj)
        {
            if (obj == PlayModeStateChange.EnteredPlayMode)
            {
                _eventLogList.Clear();
            }
        }

        private void OnEventTriggered(AEEvent applyEvent,GameObject eventAttachObject)
        {
            var gameObjectName = eventAttachObject != null ? eventAttachObject.name : "null";
            _eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]事件触发，name：{applyEvent.name}，id：{applyEvent.id}，GameObject：{gameObjectName}");
            if (Math.Abs(Math.Abs(Mathf.Abs(_scrollPosition.y - _scrollViewRect.height)) - _actuallyScrollViewHeight) < 1 || (_scrollPosition.y ==0 && _scrollViewRect.height<_actuallyScrollViewHeight))
            {
                _prepareToDownPage = true;
            }
            Repaint();
        }

        private void OnEventActionTriggered(AEEventType? eventType, bool isOverride, string targetName, int targetId, string targetType, GameObject eventAttachObject)
        {
            var overrideTag = isOverride ? "(覆盖动作)" : "";
            var gameObjectName = eventAttachObject != null ? eventAttachObject.name : "null";
            _eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]  执行动作：{eventType}{overrideTag}，[ 对象：{targetName}，id：{targetId}，GameObejct:{gameObjectName} ]");
            if (Math.Abs(Math.Abs(Mathf.Abs(_scrollPosition.y - _scrollViewRect.height)) - _actuallyScrollViewHeight) < 1 || (_scrollPosition.y == 0 && _scrollViewRect.height < _actuallyScrollViewHeight))
            {
                _prepareToDownPage = true;
            }
            Repaint();
        }



        private void OnPlayableGenerated(string playableName, int playableId, GameObject gameObject)
        {
            var gameObjectName = gameObject != null ? gameObject.name : "null";
            _eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]      生成实例：{playableName}，Id：{playableId}，GameObject：{gameObjectName}");
            if (Math.Abs(Math.Abs(Mathf.Abs(_scrollPosition.y - _scrollViewRect.height)) - _actuallyScrollViewHeight) < 1 || (_scrollPosition.y == 0 && _scrollViewRect.height < _actuallyScrollViewHeight))
            {
                _prepareToDownPage = true;
            }
            Repaint();
        }

        private void OnPlayableDestroyed(string playableName, int playableId, GameObject gameObject)
        {
            var gameObjectName = gameObject != null ? gameObject.name : "null";
            _eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}]      实例结束播放，销毁实例：{playableName}，Id：{playableId}，GameObject：{gameObjectName}");
            if (Math.Abs(Math.Abs(Mathf.Abs(_scrollPosition.y - _scrollViewRect.height)) - _actuallyScrollViewHeight) < 1 || (_scrollPosition.y == 0 && _scrollViewRect.height < _actuallyScrollViewHeight))
            {
                _prepareToDownPage = true;
            }
            Repaint();
        }

        public static void ShowLog(string info)
        {
            if(_eventLogList!= null) _eventLogList.Add($"[{System.DateTime.Now.ToLongTimeString()}] {info}");
        }

        void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AudioEditorManager.EventTriggered -= OnEventTriggered;
            AudioEditorManager.EventUnitTriggered -= OnEventActionTriggered;
            AudioEditorManager.PlayableGenerated -= OnPlayableGenerated;
            AudioEditorManager.PlayableDestroyed -= OnPlayableDestroyed;

            _searchString = null;
            _eventLogList.Clear();
        }
    }
}
