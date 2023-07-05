//
// (c) 2016 Digital Ruby, LLC
// http://www.digitalruby.com
// Code may not be redistributed in source form!
// Using this code in commercial games and apps is fine.
//

using UnityEngine;

using System.Collections;

#if UNITY_EDITOR

using UnityEditor;

#endif

namespace DigitalRuby.FastLineRenderer
{
    /// <summary>
    /// Range of integers
    /// </summary>
    [System.Serializable]
    public struct RangeOfIntegers
    {
        /// <summary>
        /// Min value
        /// </summary>
        public int Minimum;

        /// <summary>
        /// Max value
        /// </summary>
        public int Maximum;
    }

    /// <summary>
    /// Range of floats
    /// </summary>
    [System.Serializable]
    public struct RangeOfFloats
    {
        /// <summary>
        /// Min value
        /// </summary>
        public float Minimum;

        /// <summary>
        /// Max value
        /// </summary>
        public float Maximum;
    }

    /// <summary>
    /// Reorderable list of game object
    /// </summary>
    [System.Serializable]
    public class ReorderableList_GameObject : ReorderableList<GameObject> { }

    /// <summary>
    /// Reorderable list of transform
    /// </summary>
    [System.Serializable]
    public class ReorderableList_Transform : ReorderableList<Transform> { }

    /// <summary>
    /// Reorderable list of Vector3
    /// </summary>
    [System.Serializable]
    public class ReorderableList_Vector3 : ReorderableList<Vector3> { }

    /// <summary>
    /// Reorderable list of rect
    /// </summary>
    [System.Serializable]
    public class ReorderableList_Rect : ReorderableList<Rect> { }

    /// <summary>
    /// Reorderable list of rect offset
    /// </summary>
    [System.Serializable]
    public class ReorderableList_RectOffset : ReorderableList<RectOffset> { }

    /// <summary>
    /// Reorderable list of int
    /// </summary>
    [System.Serializable]
    public class ReorderableList_Int : ReorderableList<int> { }

    /// <summary>
    /// Reorderable list of float
    /// </summary>
    [System.Serializable]
    public class ReorderableList_Float : ReorderableList<float> { }

    /// <summary>
    /// Reorderable list of string
    /// </summary>
    [System.Serializable]
    public class ReorderableList_String : ReorderableList<string> { }

    /// <summary>
    /// Reorderable list of generic type
    /// </summary>
    [System.Serializable]
    public class ReorderableList<T> : ReorderableListBase
    {
        /// <summary>
        /// Inner list
        /// </summary>
        public System.Collections.Generic.List<T> List;
    }

    /// <summary>
    /// Reorderable list base
    /// </summary>
    [System.Serializable]
    public class ReorderableListBase { }

    /// <summary>
    /// Reorderable list attribute
    /// </summary>
    public class ReorderableListAttribute : PropertyAttribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tooltip">Tooltip</param>
        public ReorderableListAttribute(string tooltip) { Tooltip = tooltip; }

        /// <summary>
        /// Tooltip
        /// </summary>
        public string Tooltip { get; private set; }
    }

    /// <summary>
    /// Single line attribute, use in place of tooltip attribute
    /// </summary>
    public class SingleLineAttribute : PropertyAttribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tooltip">Tooltip</param>
        public SingleLineAttribute(string tooltip) { Tooltip = tooltip; }

        /// <summary>
        /// Tooltip
        /// </summary>
        public string Tooltip { get; private set; }
    }

    /// <summary>
    /// Single line attribute that clamps, use in place of tooltip attribute
    /// </summary>
    public class SingleLineClampAttribute : SingleLineAttribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tooltip">Tooltip</param>
        /// <param name="minValue">Min value</param>
        /// <param name="maxValue">Max value</param>
        public SingleLineClampAttribute(string tooltip, double minValue, double maxValue) : base(tooltip)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }

        /// <summary>
        /// Min value
        /// </summary>
        public double MinValue { get; private set; }

        /// <summary>
        /// Max value
        /// </summary>
        public double MaxValue { get; private set; }
    }

#if UNITY_EDITOR

    /// <summary>
    /// Single line property drawer
    /// </summary>
    [CustomPropertyDrawer(typeof(SingleLineAttribute))]
    [CustomPropertyDrawer(typeof(SingleLineClampAttribute))]
    public class SingleLineDrawer : PropertyDrawer
    {
        private void DrawIntTextField(Rect position, string text, string tooltip, SerializedProperty prop)
        {
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.IntField(position, new GUIContent(text, tooltip), prop.intValue);
            SingleLineClampAttribute clamp = attribute as SingleLineClampAttribute;
            if (clamp != null)
            {
                value = Mathf.Clamp(value, (int)clamp.MinValue, (int)clamp.MaxValue);
            }
            if (EditorGUI.EndChangeCheck())
            {
                prop.intValue = value;
            }
        }

        private void DrawFloatTextField(Rect position, string text, string tooltip, SerializedProperty prop)
        {
            EditorGUI.BeginChangeCheck();
            float value = EditorGUI.FloatField(position, new GUIContent(text, tooltip), prop.floatValue);
            SingleLineClampAttribute clamp = attribute as SingleLineClampAttribute;
            if (clamp != null)
            {
                value = Mathf.Clamp(value, (float)clamp.MinValue, (float)clamp.MaxValue);
            }
            if (EditorGUI.EndChangeCheck())
            {
                prop.floatValue = value;
            }
        }

        private void DrawRangeField(Rect position, SerializedProperty prop, bool floatingPoint)
        {
            EditorGUIUtility.labelWidth = 30.0f;
            EditorGUIUtility.fieldWidth = 40.0f;
            float width = position.width * 0.49f;
            float spacing = position.width * 0.02f;
            position.width = width;
            if (floatingPoint)
            {
                DrawFloatTextField(position, "Min", "Minimum value", prop.FindPropertyRelative("Minimum"));
            }
            else
            {
                DrawIntTextField(position, "Min", "Minimum value", prop.FindPropertyRelative("Minimum"));
            }
            position.x = position.xMax + spacing;
            position.width = width;
            if (floatingPoint)
            {
                DrawFloatTextField(position, "Max", "Maximum value", prop.FindPropertyRelative("Maximum"));
            }
            else
            {
                DrawIntTextField(position, "Max", "Maximum value", prop.FindPropertyRelative("Maximum"));
            }
        }

        /// <summary>
        /// OnGUI
        /// </summary>
        /// <param name="position">Position</param>
        /// <param name="prop">Property</param>
        /// <param name="label">Label</param>
        public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, prop);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), new GUIContent(label.text, (attribute as SingleLineAttribute).Tooltip));

            switch (prop.type)
            {
                case "RangeOfIntegers":
                    DrawRangeField(position, prop, false);
                    break;

                case "RangeOfFloats":
                    DrawRangeField(position, prop, true);
                    break;

                default:
                    EditorGUI.HelpBox(position, "[SingleLineDrawer] doesn't work with type '" + prop.type + "'", MessageType.Error);
                    break;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>
    /// Reorderable list property drawer
    /// </summary>
    [CustomPropertyDrawer(typeof(ReorderableListAttribute), true)]
    public class ReorderableListDrawer : UnityEditor.PropertyDrawer
    {
        private UnityEditorInternal.ReorderableList list;
        private SerializedProperty prevProperty;

        private UnityEditorInternal.ReorderableList GetList(SerializedProperty property)
        {
            if (list == null || prevProperty != property)
            {
                prevProperty = property;
                SerializedProperty listProperty = property.FindPropertyRelative("List");
                list = new UnityEditorInternal.ReorderableList(listProperty.serializedObject, listProperty, true, false, true, true);
                list.drawElementCallback = (UnityEngine.Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    EditorGUIUtility.labelWidth = 100.0f;
                    EditorGUI.PropertyField(rect, listProperty.GetArrayElementAtIndex(index), true);
                };
            }
            return list;
        }

        /// <summary>
        /// Get property height
        /// </summary>
        /// <param name="property">Property</param>
        /// <param name="label">Label</param>
        /// <returns>Height</returns>
        public override float GetPropertyHeight(SerializedProperty property, UnityEngine.GUIContent label)
        {
            return GetList(property).GetHeight();
        }

        /// <summary>
        /// OnGUI
        /// </summary>
        /// <param name="position">Position</param>
        /// <param name="property">Property</param>
        /// <param name="label">Label</param>
        public override void OnGUI(UnityEngine.Rect position, SerializedProperty property, UnityEngine.GUIContent label)
        {
            ReorderableListAttribute attr = attribute as ReorderableListAttribute;
            string tooltip = (attr == null ? string.Empty : attr.Tooltip);
            UnityEditorInternal.ReorderableList list = GetList(property);
            float height;
            if (list.serializedProperty.arraySize == 0)
            {
                height = 20.0f;
            }
            else
            {
                height = 0.0f;
                for (var i = 0; i < list.serializedProperty.arraySize; i++)
                {
                    height = Mathf.Max(height, EditorGUI.GetPropertyHeight(list.serializedProperty.GetArrayElementAtIndex(i)));
                }
            }
            list.drawHeaderCallback = (Rect r) =>
            {
                EditorGUI.LabelField(r, new GUIContent(label.text, tooltip));
            };
            list.elementHeight = height;
            list.DoList(position);
        }
    }

#endif

}
