using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AudioEditor.Runtime;
using AudioEditor.Runtime.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace AudioEditor.Editor
{
    public class AudioEdtiorGlobalSetting : SettingsProvider
    {
        private AudioEditorManager manager;
        private EditorViewData viewData;
        private readonly GUIContent staticLinkAudioClipLabel = new GUIContent("静态关联音频文件", "Static Link AudioClip");
        private readonly GUIContent autoLoadDataFileLabel = new GUIContent("自动加载配置文件", "Auto Load Data File");
        private readonly GUIContent dataLoadFolderLabel = new GUIContent("配置文件加载路径", "Data Path");
        private readonly GUIContent freshPageWhenTabIndexChangedLabel = new GUIContent("当选择变更时是否刷新页面", "Fresh Page When Tab Index Changed");


        public AudioEdtiorGlobalSetting(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes = SettingsScope.Project, keywords)
        {

        }

        [SettingsProvider]
        public static SettingsProvider GetAudioEditorProjectSetting()
        {
            var provider = new AudioEdtiorGlobalSetting("Project/AudioEditor", SettingsScope.Project);
            //provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;

        }


        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            manager = AudioEditorManager.FindManager();
            if (manager != null)
            {
                LoadEditorViewData();
            }
        }

        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);

            manager = AudioEditorManager.FindManager();
            if (manager != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.LabelField("Global Setting", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

                EditorGUI.indentLevel++;
                //在勾选上后直接链接上所有的AudioClip，否则所有组件都还是空的状态，直接进行构建打包的话会无法加载AudioClip
                var newStaticallyLinkedAudioClipsOption =
                    EditorGUILayout.ToggleLeft(new GUIContent("静态关联AudioClips", "如取消此选项，会调用加载AudioClip的委托方法"), manager.staticallyLinkedAudioClips);
                GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
                if (newStaticallyLinkedAudioClipsOption != manager.staticallyLinkedAudioClips)
                {
                    manager.staticallyLinkedAudioClips = newStaticallyLinkedAudioClipsOption;
                    if (newStaticallyLinkedAudioClipsOption == true)
                    {
                        foreach (var audioComponent in AudioEditorManager.Data.audioComponentData)
                        {
                            if (audioComponent is SoundSFX soundSFX)
                            {
                                if (soundSFX.clip == null)
                                {
                                    AudioEditorView.LoadSoundSFXAudioClip(soundSFX);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var audioComponent in AudioEditorManager.Data.audioComponentData)
                        {
                            if (audioComponent is SoundSFX soundSFX)
                            {
                                soundSFX.clip = null;
                            }
                        }
                    }
                }

                manager.autoLoadDataAsset = EditorGUILayout.ToggleLeft(autoLoadDataFileLabel,
                    manager.autoLoadDataAsset, GUILayout.ExpandWidth(false));

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(dataLoadFolderLabel, GUILayout.ExpandWidth(false));

                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.TextField(manager.programPath);
                }

                using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("更改"))
                    {
                        var newPath = EditorUtility.OpenFolderPanel("请选择文件路径","Assets","");
                        //进行路径检测，不能在项目路径之外和Resources文件夹下
                        newPath = newPath.Replace(@"\", "/");

                        if (newPath != manager.programPath)
                        {
                            var projectPath = Application.dataPath;

                            if (newPath.Contains(projectPath) == false)
                            {
                                Debug.LogError("请选择工程内的目录");
                            }
                            else
                            {
                                newPath = newPath.Replace(projectPath, "Assets");
                                try
                                {
                                    //AssetDatabase.StartAssetEditing();
                                    
                                    string parentDirectory;
                                    var selectOriginPath = false;
                                    if (newPath.Split('/').Last() == "AudioEditor")
                                    {
                                        selectOriginPath = true;
                                        parentDirectory = $"{newPath}/Data";
                                    }
                                    else
                                    {
                                        parentDirectory = $"{newPath}/AudioEditor/Data";

                                        if (Directory.Exists($"{newPath}/AudioEditor") == false)
                                        {
                                            AssetDatabase.CreateFolder(newPath, "AudioEditor");
                                        }
                                    }

                                    var result = AssetDatabase.MoveAsset($"{manager.programPath}/AudioEditor/Data",
                                        parentDirectory);

                                    if (result != "")
                                    {

                                        Debug.Log(result);
                                    }
                                    else
                                    {
                                        //删除旧目录
                                        if (System.IO.Directory
                                            .GetFileSystemEntries($"{manager.programPath}/AudioEditor").Length == 0)
                                        {
                                            AssetDatabase.DeleteAsset($"{manager.programPath}/AudioEditor");
                                        }

                                        manager.programPath =
                                            selectOriginPath ? newPath.Remove(newPath.Length - 12) : newPath;
                                    }


                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                    throw;
                                }
                                //finally
                                //{
                                //    AssetDatabase.StopAssetEditing();
                                //}
                            }

                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("UI Setting", EditorStyles.boldLabel);
                if (viewData == null)
                {
                    LoadEditorViewData();
                }

                EditorGUI.indentLevel++;

                viewData.freshPageWhenTabInexChange = EditorGUILayout.ToggleLeft(freshPageWhenTabIndexChangedLabel,
                    viewData.freshPageWhenTabInexChange, GUILayout.ExpandWidth(false));

                EditorGUI.indentLevel--;
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(manager);
                    EditorUtility.SetDirty(viewData);
                    AssetDatabase.SaveAssets();
                    if (EditorApplication.isPlaying == false)
                    {
                        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请在场景中挂载AudioEditorManager", MessageType.Warning);
            }

        }

        private void LoadEditorViewData()
        {
            viewData = AssetDatabase.LoadAssetAtPath<EditorViewData>(AudioEditorManager.EditorViewDataPath);
            if (viewData == null)
            {
                viewData = ScriptableObject.CreateInstance<EditorViewData>();
                AssetDatabase.CreateAsset(viewData, AudioEditorManager.EditorViewDataPath);
            }
        }
    }
}
