using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ypzxAudioEditor.Utility
{
    public static class IconUtility
    {
        private const string TextureAssetPath = "Assets/AudioEditor/Editor/Icons/{0}.png";
        private const string TexturePackagePath = "Packages/com.xsj.audioeditor/Editor/Icons/{0}.png";

        // public static readonly GUIContent workunit = GetIconContent(AEComponentType.Workunit);
        // public static readonly GUIContent soundSFX = GetIconContent(AEComponentType.SoundSFX);
        // public static readonly GUIContent randomContainer = GetIconContent(AEComponentType.RandomContainer);
        // public static readonly GUIContent sequenceContainer = GetIconContent(AEComponentType.SequenceContainer);
        // public static readonly GUIContent switchContainer = GetIconContent(AEComponentType.SwitchContainer);

        //  [Obsolete]
        //  public static readonly Dictionary<AEComponentType, Texture2D> ComponentIconDictionary = new Dictionary<AEComponentType, Texture2D> {
        //     {AEComponentType.workUnit,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_Workunit_nor.png")},
        //     {AEComponentType.soundSFX,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_SoundFX_nor.png")},
        //     {AEComponentType.RandomContainer,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_RandomContainer_nor.png")},
        //     {AEComponentType.SequenceContainer,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_SequenceContainer_nor.png")},
        //     {AEComponentType.SwitchContainer,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_SwitchContainer_nor.png")},
        //     {AEComponentType.BlendContainer,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_BlendContainer_nor.png")},
        //     {AEComponentType.Event,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_Event_nor.png")},
        //     {AEComponentType.SwitchGroup,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_SwitchGroup_nor.png")},
        //     {AEComponentType.Switch,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_Switch_nor.png")},
        //     {AEComponentType.StateGroup,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_StateGroup_nor.png")},
        //     {AEComponentType.State,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_State_nor.png")},
        //     {AEComponentType.GameParameter,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/ObjectIcons_GameParameter_nor.png")},
        // };

        // [Obsolete]
        // public static readonly Texture2D[] PreviewModeIconTexture =
        // {
        //     AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/Transport_PlayButton_nor.png"),
        //     AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/Transport_PlayButton_on.png"),
        //     AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/Transport_PauseButton_nor.png"),
        //     AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/Transport_PauseButton_on.png"),
        //     AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/Transport_StopButton_nor.png"),
        //     AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AudioEditor/Editor/Icons/Transport_StopButton_on.png")
        // };

        private static readonly Dictionary<AEComponentType, string> _componentIconName = new Dictionary<AEComponentType, string>
        {
            {AEComponentType.Workunit,"Workunit"},
            {AEComponentType.SoundSFX,"SoundFX"},
            {AEComponentType.RandomContainer,"RandomContainer"},
            {AEComponentType.SequenceContainer,"SequenceContainer"},
            {AEComponentType.SwitchContainer,"SwitchContainer"},
            {AEComponentType.BlendContainer,"BlendContainer"},
            {AEComponentType.Event,"Event"},
            {AEComponentType.SwitchGroup,"SwitchGroup"},
            {AEComponentType.Switch,"Switch"},
            {AEComponentType.StateGroup,"StateGroup"},
            {AEComponentType.State,"State"},
            {AEComponentType.GameParameter,"GameParameter"},
        };

        private static readonly Dictionary<PreviewIconType,string> _previewIconName = new Dictionary<PreviewIconType, string>
        {
            {PreviewIconType.Pause_off,"Transport_PauseButton_nor"},
            {PreviewIconType.Pause_on,"Transport_PauseButton_on"},
            {PreviewIconType.Play_off,"Transport_PlayButton_nor"},
            {PreviewIconType.Play_on,"Transport_PlayButton_on"},
            {PreviewIconType.Stop_off,"Transport_StopButton_nor"},
            {PreviewIconType.Stop_on,"Transport_StopButton_on"}
        };
        

        public static Texture2D GetIconTexture(AEComponentType type)
        {
            var iconName = _componentIconName[type];
            if (iconName == null)
            {
                return null;
            }
            if (System.IO.File.Exists(GetIconPackagePath(iconName)))
            {
                return EditorGUIUtility.FindTexture(GetIconPackagePath(iconName));
            }
            else
            {
                return EditorGUIUtility.FindTexture(GetIconAssetPath(iconName));
            }
        }

        public static GUIContent GetIconContent(AEComponentType iconType, string tooltip = null)
        {
            string iconName = _componentIconName[iconType];
            if (iconName == null)
            {
                return null;
            }

            if (System.IO.File.Exists(GetIconPackagePath(iconName)))
            {
                return EditorGUIUtility.TrIconContent(GetIconPackagePath(iconName), tooltip ?? iconName);
            }
            return EditorGUIUtility.TrIconContent(GetIconAssetPath(iconName), tooltip ?? iconName);
        }

        public static GUIContent GetIconContent(PreviewIconType iconType, string tooltip = null)
        {
            string iconName = _previewIconName[iconType];
            if (iconName == null)
            {
                return null;
            }

            if (System.IO.File.Exists(GetIconPackagePath(iconName)))
            {
                return EditorGUIUtility.TrIconContent(GetIconPackagePath(iconName), tooltip ?? iconName);
            }
            return EditorGUIUtility.TrIconContent(GetIconAssetPath(iconName), tooltip ?? iconName);

            // var guiContent = EditorGUIUtility.TrIconContent(iconName == null ? null : GetIconPackagePath(iconName), tooltip ?? iconName);
            // if (guiContent == null)
            // {
            //     guiContent = EditorGUIUtility.TrIconContent(iconName == null ? null : GetIconAssetPath(iconName), tooltip ?? iconName);
            // }
            // return guiContent;
        }


        private static string GetIconPackagePath(string icon)
        {
            return string.Format(TexturePackagePath, icon);
        }

        private static string GetIconAssetPath(string icon)
        {
            return string.Format(TextureAssetPath, icon);
        }
    }

    public enum PreviewIconType
    {
        Pause_on,
        Pause_off,
        Play_on,
        Play_off,
        Stop_on,
        Stop_off
    }
}