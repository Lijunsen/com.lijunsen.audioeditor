using AudioEditor.Runtime;
using AudioEditor.Runtime.Utility;
using UnityEditor;
using UnityEngine;

namespace AudioEditor.Editor
{
    [CustomPropertyDrawer(typeof(RTPC))]
    internal class RTPCDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null || label == null)
            {
                Debug.LogError("Error rendering drawer for AssetReference property.");
                return;
            }
            EditorGUI.BeginProperty(position, label, property);
            var id = property.FindPropertyRelative("targetGameParameterId");
            position = UnityEditor.EditorGUI.PrefixLabel(position,
                UnityEngine.GUIUtility.GetControlID(UnityEngine.FocusType.Passive), label);

            var audioEditorGameParameterData = AudioEditorManager.GetAEComponentDataByID<GameParameter>(id.intValue);
            var name = audioEditorGameParameterData == null ? "null" : audioEditorGameParameterData.name;
            var labelText = label.text;
            var rtpc =
                property.GetActualObjectForSerializedProperty<RTPC>(fieldInfo, ref labelText);
            if (rtpc == null)
            {
                Debug.LogError("RTPC is null");
            }

            if (EditorGUI.DropdownButton(position, new GUIContent(name), FocusType.Passive))
            {
                PopupWindow.Show(position, new SelectPopupWindow(WindowOpenFor.SelectGameParameter, (x) =>
                {
                    rtpc.targetGameParameterId = x;

                }));
            }

            EditorGUI.EndProperty();
        }

    }
}