using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using ypzxAudioEditor.Utility;
using PopupWindow = UnityEditor.PopupWindow;

namespace ypzxAudioEditor
{
    [CustomPropertyDrawer(typeof(EventReference))]
    public class EventReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null || label == null)
            {
                Debug.LogError("Error rendering drawer for AssetReference property.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            var  id = property.FindPropertyRelative("targetEventID");
            position = UnityEditor.EditorGUI.PrefixLabel(position,
                 UnityEngine.GUIUtility.GetControlID(UnityEngine.FocusType.Passive), label);
            var audioEditorEventData = AudioEditorManager.GetAEComponentDataByID<AEEvent>(id.intValue);
            var name = audioEditorEventData == null ? "null" : audioEditorEventData.name;
            var labelText = label.text;
            var eventReference =
                property.GetActualObjectForSerializedProperty<EventReference>(fieldInfo, ref labelText);
            if (eventReference == null)
            {
                Debug.LogError("EventReferenceObj is null");
            }

            if (EditorGUI.DropdownButton(position, new GUIContent(name), FocusType.Passive))
            {
                PopupWindow.Show(position, new SelectPopupWindow(WindowOpenFor.SelectEvent, (x) =>
                {
                    eventReference.targetEventID = x;

                }));
            }
            //  EditorGUI.PropertyField(position, property.FindPropertyRelative("targetEventID"), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }

    public static class SerializedPropertyExtensions
    {
        public static T GetActualObjectForSerializedProperty<T>(this SerializedProperty property, FieldInfo field, ref string label)
        {
            try
            {
                if (property == null || field == null)
                    return default(T);
                var serializedObject = property.serializedObject;
                if (serializedObject == null)
                {
                    return default(T);
                }

                var targetObject = serializedObject.targetObject;

                if (property.depth > 0)
                {
                    var slicedName = property.propertyPath.Split('.').ToList();
                    List<int> arrayCounts = new List<int>();
                    for (int index = 0; index < slicedName.Count; index++)
                    {
                        arrayCounts.Add(-1);
                        var currName = slicedName[index];
                        if (currName.EndsWith("]"))
                        {
                            var arraySlice = currName.Split('[', ']');
                            if (arraySlice.Length >= 2)
                            {
                                arrayCounts[index - 2] = Convert.ToInt32(arraySlice[1]);
                                slicedName[index] = string.Empty;
                                slicedName[index - 1] = string.Empty;
                            }
                        }
                    }

                    while (string.IsNullOrEmpty(slicedName.Last()))
                    {
                        int i = slicedName.Count - 1;
                        slicedName.RemoveAt(i);
                        arrayCounts.RemoveAt(i);
                    }

                    if (property.propertyPath.EndsWith("]"))
                    {
                        var slice = property.propertyPath.Split('[', ']');
                        if (slice.Length >= 2)
                            label = "Element " + slice[slice.Length - 2];
                    }
                    else
                    {
                        label = slicedName.Last();
                    }

                    return DescendHierarchy<T>(targetObject, slicedName, arrayCounts, 0);
                }

                var obj = field.GetValue(targetObject);
                return (T)obj;
            }
            catch
            {
                return default(T);
            }
        }

        static T DescendHierarchy<T>(object targetObject, List<string> splitName, List<int> splitCounts, int depth)
        {
            if (depth >= splitName.Count)
                return default(T);

            var currName = splitName[depth];

            if (string.IsNullOrEmpty(currName))
                return DescendHierarchy<T>(targetObject, splitName, splitCounts, depth + 1);

            int arrayIndex = splitCounts[depth];

            var newField = targetObject.GetType().GetField(currName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (newField == null)
            {
                Type baseType = targetObject.GetType().BaseType;
                while (baseType != null && newField == null)
                {
                    newField = baseType.GetField(currName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    baseType = baseType.BaseType;
                }
            }

            var newObj = newField.GetValue(targetObject);
            if (depth == splitName.Count - 1)
            {
                T actualObject = default(T);
                if (arrayIndex >= 0)
                {
                    if (newObj.GetType().IsArray && ((System.Array)newObj).Length > arrayIndex)
                        actualObject = (T)((System.Array)newObj).GetValue(arrayIndex);

                    var newObjList = newObj as IList;
                    if (newObjList != null && newObjList.Count > arrayIndex)
                    {
                        actualObject = (T)newObjList[arrayIndex];

                        //if (actualObject == null)
                        //    actualObject = new T();
                    }
                }
                else
                {
                    actualObject = (T)newObj;
                }

                return actualObject;
            }
            else if (arrayIndex >= 0)
            {
                if (newObj is IList)
                {
                    IList list = (IList)newObj;
                    newObj = list[arrayIndex];
                }
                else if (newObj is System.Array)
                {
                    System.Array a = (System.Array)newObj;
                    newObj = a.GetValue(arrayIndex);
                }
            }

            return DescendHierarchy<T>(newObj, splitName, splitCounts, depth + 1);
        }
    }
}