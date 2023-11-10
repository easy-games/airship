using Animancer;
using UnityEngine;
using UnityEngine.Scripting;

[LuauAPI][Preserve]
public static class AnimancerBridge
{
    
    public static AnimancerState PlayOnLayer(AnimancerComponent component, AnimationClip animationClip, int layer,
        float fadeDuration, FadeMode fadeMode, WrapMode wrapMode = WrapMode.Default) {
        if (wrapMode != WrapMode.Default) {
            animationClip.wrapMode = wrapMode;
        }
        var state = component.Layers[layer].Play(animationClip, fadeDuration, fadeMode);
        //Create an empty event otherwise the event manager will skip it and typscript won't be able to access it. 
        state.Events.OnEnd += () => { };
        return state;
    }
    
    public static AnimancerState PlayOnceOnLayer(AnimancerComponent component, AnimationClip animationClip, int layer,
        float fadeInDuration, float fadeOutDuration, FadeMode fadeMode, WrapMode wrapMode)
    {
        var state = component.Layers[layer].Play(animationClip, fadeInDuration, fadeMode);
        if (wrapMode != WrapMode.Default) {
            animationClip.wrapMode = wrapMode;
        }

        state.Events.OnEnd += () => {
            component.Layers[layer].StartFade(0, fadeOutDuration);
        };
        
        return state;
    }

    public static AnimancerLayer GetLayer(AnimancerComponent component, int layer) {
        return component.Layers[layer];
    }

    public static void SetGlobalSpeed(AnimancerComponent component, float speed) {
        component.Playable.Speed = speed;
    }
}