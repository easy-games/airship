using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace Code.Player.Character
{
    [ExecuteInEditMode]
    [LuauAPI]
    public class AnimatorClipReplacer : MonoBehaviour {
        private static Dictionary<Animator, AnimatorOverrideController> _overrideControllers = new ();
        
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

        private List<KeyValuePair<AnimationClip, AnimationClip>> originalOverrides;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetOnLoad() {
            _overrideControllers.Clear();
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

            SaveOriginalOverrides(overrideController);

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
                if (!Application.isPlaying)
                {
                Debug.Log("Clips overrides have been applied");
                }
            }
            else
            {
                Debug.LogWarning("Some clips could not be applied due to missing overrides.");
            }

            // Apply the updated overrides
            overrideController.ApplyOverrides(newOverrides);


        }

        /// <summary>
        /// Method that performs the actual removal of animation clips from the given
        /// <see cref="AnimatorOverrideController"/>.
        /// It validates the clip replacements and removes them from the controller.
        /// </summary>
        /// <param name="controller">The AnimatorOverrideController to apply the replacements to.</param>
        public void RemoveClips(Object controller)
        {
            AnimatorOverrideController overrideController = null;

            if (controller == null)
            {
                Debug.LogError("No valid AnimatorOverrideController found.");
                return;
            }

            if (controller is Animator animator)
            {
                overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
            }
            else if (controller is GameObject go && go.TryGetComponent(out Animator animatorComponent))
            {
                overrideController = animatorComponent.runtimeAnimatorController as AnimatorOverrideController;
            }
            else if (controller is AnimatorOverrideController oc)
            {
                overrideController = oc;
            }
            else
            {
                Debug.LogError("No valid AnimatorOverrideController found.");
                return;
            }

            if (overrideController == null)
            {
                Debug.LogError("No valid AnimatorOverrideController found.");
                return;
            }

            if (originalOverrides == null || originalOverrides.Count == 0)
            {
                Debug.LogWarning("No original overrides found to restore.");
                return;
            }

            if (clipReplacements == null || clipReplacements.Count == 0)
            {
                Debug.LogWarning("No clip replacements configured to restore.");
                return;
            }

            // Get the current overrides from the AnimatorOverrideController
            var currentOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
            overrideController.GetOverrides(currentOverrides);

            // Create a new list to store the restored overrides
            var restoredOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(currentOverrides);

            // Restore only the clips configured in clipReplacements
            foreach (var replacement in clipReplacements)
            {
                var baseClipName = replacement.baseClipName;
                var originalOverride = originalOverrides.FirstOrDefault(o => o.Key.name == baseClipName);

                if (originalOverride.Key != null)
                {
                    // Replace the current clip with the original
                    for (int i = 0; i < restoredOverrides.Count; i++)
                    {
                        if (restoredOverrides[i].Key.name == baseClipName)
                        {
                            restoredOverrides[i] = originalOverride;
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Original clip for {baseClipName} not found in saved overrides.");
                }
            }

            // Apply the restored overrides to the AnimatorOverrideController
            overrideController.ApplyOverrides(restoredOverrides);

            if (!Application.isPlaying)
            {
                Debug.Log("Specified clips have been restored.");
            }
        }


        /// <summary>
        /// Attempts to extract an AnimatorOverrideController from the given object.
        /// </summary>
        private bool TryGetOverrideController(Object controller, out AnimatorOverrideController overrideController)
        {
            overrideController = null;

            if (controller is Animator animator) {
                if (_overrideControllers.TryGetValue(animator, out var oc)) {
                    overrideController = oc;
                    return true;
                }
                
                overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;

                if (Application.isPlaying)
                {
                    SaveOriginalOverrides(overrideController);
                    AnimatorOverrideController instanaceAnimator = new(animator.runtimeAnimatorController);
                    var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
                    overrideController.GetOverrides(overrides);
                    
                    instanaceAnimator.ApplyOverrides(overrides);
                    animator.runtimeAnimatorController = instanaceAnimator;
                    _overrideControllers[animator] = instanaceAnimator; 

                    overrideController = animator.runtimeAnimatorController as AnimatorOverrideController;
                }
            }
            else if (controller is GameObject go)
            {
                Animator animatorComponent = go.GetComponent<Animator>();
                if (animatorComponent != null)
                {
                    overrideController = animatorComponent.runtimeAnimatorController as AnimatorOverrideController;

                    if (Application.isPlaying)
                    {
                        SaveOriginalOverrides(overrideController);
                        AnimatorOverrideController instanaceAnimator = new(animatorComponent.runtimeAnimatorController);
                        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
                        overrideController.GetOverrides(overrides);

                        instanaceAnimator.ApplyOverrides(overrides);
                        animatorComponent.runtimeAnimatorController = instanaceAnimator;

                        overrideController = animatorComponent.runtimeAnimatorController as AnimatorOverrideController;
                    }
                }
            }
            else if (controller is AnimatorOverrideController oc)
            {
                overrideController = oc;
            }

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

        private void SaveOriginalOverrides(AnimatorOverrideController overrideController)
        {
            if (originalOverrides == null)
            {
                originalOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
                overrideController.GetOverrides(originalOverrides);
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