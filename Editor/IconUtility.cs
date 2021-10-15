using AudioEditor.Runtime;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioEditor.Editor.Utility
{
    internal static class IconUtility
    {

        private static readonly Dictionary<AEComponentType, string> componentIconName = new Dictionary<AEComponentType, string>
        {
            {AEComponentType.WorkUnit,"Workunit"},
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
            {AEComponentType.ActorMixer,"ActorMixer"}
        };

        private static readonly Dictionary<PreviewIconType, string> previewIconName = new Dictionary<PreviewIconType, string>
        {
            {PreviewIconType.PauseOff,"Transport_PauseButton_nor"},
            {PreviewIconType.PauseOn,"Transport_PauseButton_on"},
            {PreviewIconType.PlayOff,"Transport_PlayButton_nor"},
            {PreviewIconType.PlayOn,"Transport_PlayButton_on"},
            {PreviewIconType.StopOff,"Transport_StopButton_nor"},
            {PreviewIconType.StopOn,"Transport_StopButton_on"}
        };

        private const string TexturePackagePath = "Packages/com.xsj.audioeditor/Editor/Icons/{0}.png";

        private static string TextureAssetPath => "Assets/AudioEditor/Editor/Icons/{0}.png";

        public static Texture2D GetIconTexture(AEComponentType type)
        {
            var iconName = componentIconName[type];
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
            string iconName = componentIconName[iconType];
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
            string iconName = previewIconName[iconType];
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

    internal enum PreviewIconType
    {
        PauseOn,
        PauseOff,
        PlayOn,
        PlayOff,
        StopOn,
        StopOff
    }
}