using UnityEditor;
using UnityEngine;
using System.Linq;

[CustomPropertyDrawer(typeof(BaseClipSelection))]
public class BaseClipSelectionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var clipNameProp = property.FindPropertyRelative("clipName");

        Rect dropdownRect = new Rect(position.x, position.y, position.width, position.height);

        if (property.serializedObject.targetObject is BaseClipPreset preset && preset.animatorOverrideController != null)
        {
            var controller = preset.animatorOverrideController.runtimeAnimatorController;
            if (controller != null)
            {
                var clipNames = controller.animationClips.Select(c => c.name).ToArray();
                int selectedIndex = System.Array.IndexOf(clipNames, clipNameProp.stringValue);
                if (selectedIndex < 0) selectedIndex = 0;
                selectedIndex = EditorGUI.Popup(dropdownRect, selectedIndex, clipNames);
                clipNameProp.stringValue = clipNames[selectedIndex];
            }
            else
            {
                EditorGUI.LabelField(dropdownRect, "Controller not found");
            }
        }
        else
        {
            EditorGUI.LabelField(dropdownRect, "Animator Override Controller not assigned");
        }
    }
}
