using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Code.Player.Character;

[CustomEditor(typeof(AnimatorClipReplacer))]
public class AnimatorClipReplacementEditor : UnityEditor.Editor
{
    private SerializedProperty controllerProp;

    private ReorderableList clipReplacementsList;
    private ReorderableList presetList;
    private UnityEditor.Animations.AnimatorController previousController;

    private bool clipReplacementsFoldout = true;
    private bool presetsFoldout = true;



    private void OnEnable()
    {
        controllerProp = serializedObject.FindProperty("_animatorController");

        SetupClipReplacementsList();
        SetupPresetList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw the ObjectField for the controller with validation
        EditorGUILayout.PropertyField(controllerProp);

        // Check if the assigned object is valid (Animator, GameObject with Animator, or AnimatorOverrideController)
        Object selectedObject = controllerProp.objectReferenceValue;
        if (selectedObject != null && !IsValidController(selectedObject))
        {
            EditorGUILayout.HelpBox("Selected object is not a valid Animator Override Controller.", MessageType.Warning);
        }
        else if (selectedObject == null)
        {
            EditorGUILayout.HelpBox("Animator Controller needed on the property.", MessageType.Error);
        }

        CheckAndReloadAnimatorController();

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Clips Replacement
        clipReplacementsFoldout = EditorGUILayout.Foldout(clipReplacementsFoldout, "Clips", true);
        if (clipReplacementsFoldout)
        {
            DrawClipReplacementsList();
        }
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Presets Replacement
        presetsFoldout = EditorGUILayout.Foldout(presetsFoldout, "Base Clip Presets", true);
        if (presetsFoldout)
        {
            DrawPresetsList();
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Function to validate the selected controller object
    private bool IsValidController(Object obj)
    {
        if (obj is Animator) return true; // Direct Animator is valid
        if (obj is AnimatorOverrideController) return true; // OverrideController is valid

        // Check if the object is a GameObject with an Animator component
        if (obj is GameObject go)
        {
            Animator animator = go.GetComponent<Animator>();
            if (animator != null)
            {
                if (animator.runtimeAnimatorController is AnimatorOverrideController)
                {
                    return true;
                }
            }
        }

        return false; // Return false if none of the above conditions are met
    }



    #region Draw Editor Layouts Contents
    private void DrawClipReplacementsList()
    {
        clipReplacementsList.DoLayoutList();
        if (GUILayout.Button("Replace Clips in Overrider Asset"))
        {
            var replacer = (AnimatorClipReplacer)target;

            if (replacer.AnimatorController != null)
            {
                replacer.ReplaceClips(replacer.AnimatorController);

            }
            else
            {
                Debug.LogError("No AnimatorOverrideController assigned in the component.");
            }
        }
    }
   
    private void DrawPresetsList()
    {
        presetList.DoLayoutList();
        if (GUILayout.Button("Apply All Presets"))
        {
            var replacer = (AnimatorClipReplacer)target;

            if (replacer.baseClipSelectionPresets.Count > 0)
            {
                //ClearAllReplacements();

                foreach (var preset in replacer.baseClipSelectionPresets)
                {
                    if (preset != null)
                    {
                        LoadPreset(preset);
                    }
                    else
                    {
                        Debug.LogWarning("A preset is missing from the list.");
                    }
                }
            }
            else
            {
                Debug.LogWarning("No presets available to apply.");
            }
        }
    }
    #endregion

    #region Updating Contents
    ///Animator Controller
    private void CheckAndReloadAnimatorController()
    {
        if (Application.isPlaying) return;

        AnimatorClipReplacer replacer = (AnimatorClipReplacer)target;
        Object controllerObject = replacer.AnimatorController;

        // Check if the controllerObject is null
        if (controllerObject == null)
        {
            return;
        }


        UnityEditor.Animations.AnimatorController currentController = null;

        // Check if the object is valid and tries to extract the AnimatorOverrideController
        if (IsValidController(controllerObject))
        {
            if (controllerObject is Animator animator)
            {
                currentController = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            }
            else if (controllerObject is GameObject go)
            {
                Animator anim = go.GetComponent<Animator>();
                if (anim != null)
                {
                    currentController = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                }
            }
            else if (controllerObject is AnimatorOverrideController overrideController)
            {
                currentController = overrideController.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            }
        }
        else
        {
            return;
        }

        // Check if the current controller has changed
        if (currentController != previousController)
        {
            previousController = currentController;
            if (currentController != null)
            {
                Repaint();
            }
        }
    }

    ///Clips

    // Temporary storage for copied list data

    private string copyBuffer; // Store copied JSON representation of the list

    private void SetupClipReplacementsList()
    {
        clipReplacementsList = new ReorderableList(
            serializedObject,
            serializedObject.FindProperty("clipReplacements"),
            true, true, true, true
        );

        // Header callback with right-click context menu
        clipReplacementsList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Clips List");

            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Overwrite presets"), false, OverwriteAllPresets);
                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Copy List"), false, CopyList);
                menu.AddItem(new GUIContent("Paste List"), false, PasteList);
                menu.AddItem(new GUIContent("Clear List"), false, ClearList);

                menu.ShowAsContext();
                Event.current.Use();
            }
        };

        // Draw element callback to display each element
        clipReplacementsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = clipReplacementsList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element, GUIContent.none
            );
        };
    }

    // Copies the entire list as JSON to the system clipboard
    private void CopyList()
    {
        var list = serializedObject.FindProperty("clipReplacements");
        var clipList = new List<ClipReplacement>();

        // Extract data from the serialized property
        for (int i = 0; i < list.arraySize; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            var baseClipName = element.FindPropertyRelative("baseClipName").stringValue;
            var replacementClip = (AnimationClip)element.FindPropertyRelative("replacementClip").objectReferenceValue;

            clipList.Add(new ClipReplacement(baseClipName, replacementClip));
        }

        // Convert to JSON and store in the clipboard
        copyBuffer = JsonUtility.ToJson(new ClipReplacementList(clipList));
        EditorGUIUtility.systemCopyBuffer = copyBuffer;
    }

    // Pastes the copied list from the clipboard
    private void PasteList()
    {
        var list = serializedObject.FindProperty("clipReplacements");

        if (!string.IsNullOrEmpty(EditorGUIUtility.systemCopyBuffer))
        {
            var json = EditorGUIUtility.systemCopyBuffer;
            var clipList = JsonUtility.FromJson<ClipReplacementList>(json);

            if (clipList != null)
            {
                list.ClearArray();

                // Rebuild the list from the pasted data
                foreach (var clip in clipList.clips)
                {
                    list.arraySize++;
                    var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                    element.FindPropertyRelative("baseClipName").stringValue = clip.baseClipName;
                    element.FindPropertyRelative("replacementClip").objectReferenceValue = clip.replacementClip;
                }

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
            }
        }
    }

    // Clears the entire list
    private void ClearList()
    {
        var list = serializedObject.FindProperty("clipReplacements");

        list.ClearArray();
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    // Helper classes for JSON serialization
    [System.Serializable]
    private class ClipReplacement
    {
        public string baseClipName;
        public AnimationClip replacementClip;

        public ClipReplacement(string baseClipName, AnimationClip replacementClip)
        {
            this.baseClipName = baseClipName;
            this.replacementClip = replacementClip;
        }
    }

    [System.Serializable]
    private class ClipReplacementList
    {
        public List<ClipReplacement> clips;

        public ClipReplacementList(List<ClipReplacement> clips)
        {
            this.clips = clips;
        }
    }

    private void OverwriteAllPresets()
    {
        var replacer = (AnimatorClipReplacer)target;

        if (replacer.baseClipSelectionPresets.Count > 0)
        {
            foreach (var preset in replacer.baseClipSelectionPresets)
            {
                if (preset != null) LoadPreset(preset, true);
            }
        }
        else
        {
            Debug.LogWarning("There are no presets available for application.");
        }
    }




    /// Presets
    private void SetupPresetList()
    {
        presetList = new ReorderableList(serializedObject,
            serializedObject.FindProperty("baseClipSelectionPresets"),
            true, true, true, true);

        presetList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Presets List");
        };

        presetList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = presetList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };
    }

    private void LoadPreset(BaseClipPreset preset, bool overwrite = false)
    {
        if (!preset.baseClipSelections.Any())
        {
            Debug.LogError($"The preset '{preset.name}' is empty. Nothing has been applied.");
            return;
        }

        var replacer = (AnimatorClipReplacer)target;
        Undo.RecordObject(replacer, "Apply Preset");

        // Access the serialized property of the list
        var clipReplacementsProp = serializedObject.FindProperty("clipReplacements");
        var baseClipNames = GetBaseClipNamesFromController();

        bool missingClipsCheck = false;

        for (int i = 0; i < preset.baseClipSelections.Count; i++)
        {
            var clipSelection = preset.baseClipSelections[i];
            bool clipExistsInController = baseClipNames.Contains(clipSelection.clipName.Trim());

            if (clipExistsInController)
            {
                if (overwrite)
                {
                    // Apply the clip to the corresponding element by index
                    if (i < clipReplacementsProp.arraySize)
                    {
                        var element = clipReplacementsProp.GetArrayElementAtIndex(i);
                        element.FindPropertyRelative("baseClipName").stringValue = clipSelection.clipName;
                    }
                    else
                    {
                        // Add a new element if the list is too short
                        clipReplacementsProp.arraySize++;
                        var newElement = clipReplacementsProp.GetArrayElementAtIndex(clipReplacementsProp.arraySize - 1);

                        newElement.FindPropertyRelative("baseClipName").stringValue = clipSelection.clipName;
                        newElement.FindPropertyRelative("replacementClip").objectReferenceValue = null;
                    }
                }
                else
                {
                    // Check if the clip already exists in the inspector list
                    bool clipNameInInspector = false;

                    for (int j = 0; j < clipReplacementsProp.arraySize; j++)
                    {
                        var element = clipReplacementsProp.GetArrayElementAtIndex(j);

                        if (element.FindPropertyRelative("baseClipName").stringValue == clipSelection.clipName)
                        {
                            clipNameInInspector = true;
                            break;
                        }
                    }

                    // Add the clip to the list if it does not exist
                    if (!clipNameInInspector)
                    {
                        clipReplacementsProp.arraySize++;
                        var newElement = clipReplacementsProp.GetArrayElementAtIndex(clipReplacementsProp.arraySize - 1);

                        newElement.FindPropertyRelative("baseClipName").stringValue = clipSelection.clipName;
                        newElement.FindPropertyRelative("replacementClip").objectReferenceValue = null;
                    }
                }
            }
            else
            {
                missingClipsCheck = true;
                Debug.LogError($"The preset clip '{clipSelection.clipName}' not found in the base controller.");
            }
        }

        if (missingClipsCheck)
        {
            Debug.LogWarning($"The list of '{preset.name}' was not fully applied, as some clips were not found.");
        }
        else
        {
            Debug.Log($"Preset: '{preset.name}' applied successfully!");
        }

        // Apply the modifications
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(replacer);
    }



    private List<string> GetBaseClipNamesFromController()
    {
        var replacer = (AnimatorClipReplacer)target;
        var controller = replacer.RuntimeAnimator;
        if (controller is AnimatorOverrideController overrideController)
        {
            controller = overrideController.runtimeAnimatorController;
        }

        if (controller != null)
        {
            return controller.animationClips
                .Select(clip => clip.name.Trim())
                .ToList();
        }

        Debug.LogWarning("No valid base controller found.");
        return new List<string>();
    }

    #endregion

}
