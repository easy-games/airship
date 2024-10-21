using UnityEditor;
using UnityEngine;
using System.Linq;
using Code.Player.Character;

[CustomPropertyDrawer(typeof(AnimationClipReplacementEntry))]
public class AnimationClipReplacementDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null)
        {
            Debug.LogError("SerializedProperty is null");
            return;
        }

        var baseClipNameProp = property.FindPropertyRelative("baseClipName");
        var replacementClipProp = property.FindPropertyRelative("replacementClip");

        if (baseClipNameProp == null || replacementClipProp == null)
        {
            Debug.LogError("One of the SerializedProperties is null");
            return;
        }

        Rect baseClipRect = new Rect(
            position.x,
            position.y + (position.height - EditorGUIUtility.singleLineHeight) / 2,
            position.width / 2,
            EditorGUIUtility.singleLineHeight
        );

        Rect replacementClipRect = new Rect(
            position.x + position.width / 2 + 5,
            position.y + (position.height - EditorGUIUtility.singleLineHeight) / 2,
            position.width / 2 - 5,
            EditorGUIUtility.singleLineHeight
        );

        if (property.serializedObject.targetObject is AnimatorClipReplacer replacer)
        {
            Object controllerObject = replacer.AnimatorController;
            RuntimeAnimatorController runtimeController = null;

            if (controllerObject is GameObject go)
            {
                Animator animator = go.GetComponent<Animator>();
                if (animator != null)
                {
                    runtimeController = animator.runtimeAnimatorController;

                    if (runtimeController is AnimatorOverrideController overrideController)
                    {
                        runtimeController = overrideController.runtimeAnimatorController;
                    }
                    else
                    {
                        EditorGUI.LabelField(baseClipRect, "Need an Animator Override Controller");
                        return;
                    }
                }
            }
            // Handle directly assigned Animator
            else if (controllerObject is Animator animator)
            {
                runtimeController = animator.runtimeAnimatorController;

                if (runtimeController is AnimatorOverrideController overrideController)
                {
                    runtimeController = overrideController.runtimeAnimatorController;
                }
                else
                {
                    EditorGUI.LabelField(baseClipRect, "Need an Animator Override Controller");
                    return;
                }
            }
            else if (controllerObject is AnimatorOverrideController overrideController)
            {
                runtimeController = overrideController.runtimeAnimatorController;
            }

            // If runtimeController is valid, populate the dropdown with the base clip names
            if (runtimeController != null)
            {
                var clipNames = runtimeController.animationClips.Select(c => c.name).ToArray();
                int selectedIndex = System.Array.IndexOf(clipNames, baseClipNameProp.stringValue);
                if (selectedIndex < 0) selectedIndex = 0;

                selectedIndex = EditorGUI.Popup(baseClipRect, selectedIndex, clipNames);
                baseClipNameProp.stringValue = clipNames[selectedIndex];

                EditorGUI.PropertyField(replacementClipRect, replacementClipProp, GUIContent.none);
            }
            else
            {
                EditorGUI.LabelField(baseClipRect, "No valid controller found");
                return;
            }

        }
        else
        {
            EditorGUI.LabelField(baseClipRect, "No Animator or OverrideController assigned");
        }


    
    }
}


