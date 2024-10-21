using Code.Player.Character.API;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace Code.Player.Character
{
    [ExecuteInEditMode]
    [LuauAPI]
    public class AnimatorClipReplacer : MonoBehaviour
    {
        /// <summary>
        /// Holds a reference to the animator-related object, which can be an <see cref="Animator"/>, 
        /// <see cref="GameObject"/>, or <see cref="AnimatorOverrideController"/>.
        /// </summary>
        [SerializeField] private Object _animatorController;
        public Object AnimatorController
        {
            get => _animatorController;
            set => _animatorController = value;
        }

        /// <summary>
        /// A list of animation clip replacement entries, where each entry defines a base clip 
        /// and its corresponding replacement.
        /// </summary>
        public List<AnimationClipReplacementEntry> clipReplacements = new List<AnimationClipReplacementEntry>();

        /// <summary>
        /// A list of base clip presets, providing predefined sets of animation clip replacements.
        /// </summary>
        public List<BaseClipPreset> baseClipSelectionPresets = new List<BaseClipPreset>();

        private void Awake()
        {
            var components = GetComponents<AnimatorClipReplacer>();
            if (components.Length > 1)
            {
                Debug.LogWarning("Multiple AnimatorClipReplacer components found on the GameObject. Removing duplicates.");
                for (int i = 1; i < components.Length; i++)
                {
                    DestroyImmediate(components[i]);
                }
            }
        }

        /// <summary>
        /// Applies the animation clip replacements to the provided Animator or AnimatorOverrideController.
        /// Supports <see cref="Animator"/>, <see cref="GameObject"/>, and <see cref="AnimatorOverrideController"/> types.
        /// </summary>
        /// <param name="_controller">The Animator or AnimatorOverrideController to apply the replacements to.</param>
        public void ReplaceClips(Object controller)
        {

            if (controller == null)
            {
                Debug.LogError($"{nameof(AnimatorClipReplacer)}: No controller assigned.");
                return;
            }

            AnimatorController = controller;

            if (TryGetOverrideController(AnimatorController, out var overrideController))
            {
                ReplaceClipsInternal(overrideController);
            }
            else
            {
                Debug.LogError($"{nameof(AnimatorClipReplacer)}: Invalid type or missing AnimatorOverrideController.");
            }
        }



        /// <summary>
        /// Overload that directly applies clip replacements to the given <see cref="AnimatorOverrideController"/>.
        /// </summary>
        /// <param name="overrideController">The AnimatorOverrideController to apply the replacements to.</param>
        public void ReplaceClips(AnimatorOverrideController overrideController)
        {
            ReplaceClipsInternal(overrideController);
        }

        /// <summary>
        /// Internal method that performs the actual replacement of animation clips in the given 
        /// <see cref="AnimatorOverrideController"/>.
        /// It validates the clip replacements and applies them to the controller.
        /// </summary>
        /// <param name="overrideController">The AnimatorOverrideController to apply the replacements to.</param>
        private void ReplaceClipsInternal(AnimatorOverrideController overrideController)
        {
            if (overrideController == null)
            {
                Debug.LogError("No valid AnimatorOverrideController found.");
                return;
            }

            if (clipReplacements.Count == 0)
            {
                Debug.LogError("No clips has been applied as the overrider list is empty");
                return;
            }

            // Checks for duplicates in the clipReplacements list
            var duplicateClips = clipReplacements
                .GroupBy(clip => clip.baseClipName)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();

            if (duplicateClips.Count > 0)
            {
                string duplicateMessage = "Duplicate animation clips detected: " +
                                          string.Join(", ", duplicateClips);
                Debug.LogError(duplicateMessage);
                return;
            }

            // Creates the animation map without duplicates
            var animationMap = clipReplacements.ToDictionary(
                clip => clip.baseClipName,
                clip => clip.replacementClip
            );

            // Copy existing substitutions to preserve
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(overrides);

            // Create a new list for the updated overrides
            var newOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrides);

            // Overwriting clips in the list
            bool hasError = false;
            for (int i = 0; i < overrides.Count; i++)
            {
                var originalClip = overrides[i].Key;
                if (animationMap.TryGetValue(originalClip.name, out var newClip))
                {
                    if (newClip != null)
                    {
                        newOverrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(originalClip, newClip);
                    }
                    else
                    {
                        Debug.LogError("(" + this.gameObject.name + ")" +
                                       " tried to overwrite (" + originalClip.name + ")" +
                                       " but there's no clip set!");
                        hasError = true;
                    }
                }
            }

            if (!hasError)
            {
                Debug.Log("Clips overrides have been applied");
            }
            else
            {
                Debug.LogWarning("Some clips could not be applied due to missing overrides.");
            }

            // Apply the updated overrides
            overrideController.ApplyOverrides(newOverrides);


        }

        /// <summary>
        /// Attempts to extract an AnimatorOverrideController from the given object.
        /// </summary>
        private bool TryGetOverrideController(Object controller, out AnimatorOverrideController overrideController)
        {
            overrideController = controller switch
            {
                Animator animator => animator.runtimeAnimatorController as AnimatorOverrideController,
                GameObject go => go.GetComponent<Animator>()?.runtimeAnimatorController as AnimatorOverrideController,
                AnimatorOverrideController oc => oc,
                _ => null
            };

            return overrideController != null;
        }

        /// <summary>
        /// Returns the <see cref="RuntimeAnimatorController"/> from the _animatorController property.
        /// It supports <see cref="Animator"/>, <see cref="GameObject"/>, and 
        /// <see cref="AnimatorOverrideController"/> types, returning the associated 
        /// runtimeAnimatorController or null if none is found.
        /// </summary>
        public RuntimeAnimatorController RuntimeAnimator
        {
            get
            {
                if (_animatorController is Animator animator)
                {
                    return animator.runtimeAnimatorController;
                }
                else if (_animatorController is GameObject go)
                {
                    var anim = go.GetComponent<Animator>();
                    if (anim != null)
                    {
                        return anim.runtimeAnimatorController;
                    }
                }
                else if (_animatorController is AnimatorOverrideController overrideController)
                {
                    return overrideController.runtimeAnimatorController;
                }

                return null;
            }
        }
    }

    /// <summary>
    /// Represents an entry for replacing animation clips. 
    /// Each entry consists of a base clip name and its replacement animation clip.
    /// </summary>
    [System.Serializable]
    public class AnimationClipReplacementEntry
    {
        /// <summary>
        /// The name of the base animation clip to be replaced.
        /// </summary>
        public string baseClipName;
        /// <summary>
        /// The replacement animation clip to be used.
        /// </summary>
        public AnimationClip replacementClip;
    }
}