using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "BaseClipPreset", menuName = "Airship/Animation/BaseReplacerClipPreset", order = 1)]
public class BaseClipPreset : ScriptableObject
{
    public AnimatorOverrideController animatorOverrideController;
    public List<BaseClipSelection> baseClipSelections = new List<BaseClipSelection>();

    public void LoadBaseClipNames()
    {
        baseClipSelections.Clear();

        if (animatorOverrideController == null)
        {
            Debug.LogWarning("AnimatorOverrideController is not assigned.");
            return;
        }

        var controller = animatorOverrideController.runtimeAnimatorController;
        if (controller != null)
        {
            foreach (var clip in controller.animationClips)
            {
                baseClipSelections.Add(new BaseClipSelection { clipName = clip.name });
            }
        }
    }

    public void ClearAllSelections()
    {
        baseClipSelections.Clear();
    }
}

[System.Serializable]
public class BaseClipSelection
{
    public string clipName;
}
