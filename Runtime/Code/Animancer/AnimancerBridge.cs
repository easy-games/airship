using Animancer;
using UnityEngine;
using UnityEngine.Scripting;

[LuauAPI][Preserve]
public static class AnimancerBridge
{
    
    public static AnimancerState Play(AnimancerComponent component, AnimationClip animationClip, int layer,
        float fadeDuration, FadeMode fadeMode, WrapMode wrapMode = WrapMode.Default) {
        if (wrapMode != WrapMode.Default) {
            animationClip.wrapMode = wrapMode;
        }
        var state = component.Layers[layer].Play(animationClip, fadeDuration, fadeMode);
        //Create an empty event otherwise the event manager will skip it and typscript won't be able to access it. 
        state.Events.OnEnd += () => { };
        return state;
    }
    
    public static AnimancerState PlayOnce(AnimancerComponent component, AnimationClip animationClip, int layer,
        float fadeDuration, FadeMode fadeMode)
    {
        var state = component.Layers[layer].Play(animationClip, fadeDuration, fadeMode);

        state.Events.OnEnd += () =>
        {
            state.StartFade(0, fadeDuration);
            // animancerComponent.Layers[layer].StartFade(fadeDuration);
        };
        
        return state;
    }

    public static AnimancerLayer GetLayer(AnimancerComponent component, int layer)
    {
        return component.Layers[layer];
    }
}