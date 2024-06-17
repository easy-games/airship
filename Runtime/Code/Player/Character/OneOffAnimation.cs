using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[RequireComponent(typeof(Animator))]
public class OneOffAnimation : MonoBehaviour
{
    public Animator Animator;

    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private AnimationClipPlayable _clip;

    public AnimatorControllerPlayable Controller;

    public AnimationScriptPlayable ScriptPlayable;
    public List<string> ExposedCurves;
    public NativeArray<PropertyStreamHandle> Curves;
    public NativeArray<float> Values;

    private void OnValidate()
    {
        if (!Animator) Animator = GetComponent<Animator>();
    }

    private void Start()
    {
        _graph = PlayableGraph.Create($"{gameObject.name} - Graph");
        var playableOutput = AnimationPlayableOutput.Create(_graph, "Animation Output", Animator);
        _mixer = AnimationMixerPlayable.Create(_graph, 1);

        Values = new NativeArray<float>(ExposedCurves.Count, Allocator.Persistent);
        Curves = new NativeArray<PropertyStreamHandle>(ExposedCurves.Count, Allocator.Persistent);
        for (var i = 0; i < ExposedCurves.Count; i++)
        {
            Curves[i] = Animator.BindStreamProperty(Animator.avatarRoot, typeof(Animator), ExposedCurves[i]);
        }
        ScriptPlayable = AnimationScriptPlayable.Create(_graph, new ReadCurveJob { Curves = Curves, Values = Values }, 1);
        ScriptPlayable.ConnectInput(0, _mixer, 0, 1);

        playableOutput.SetSourcePlayable(ScriptPlayable);

        Controller = AnimatorControllerPlayable.Create(_graph, Animator.runtimeAnimatorController);
        _mixer.ConnectInput(0, Controller, 0);

        Animator.runtimeAnimatorController = null;

        _graph.Play();
    }

    private void Update()
    {
        if (_mixer.GetInputCount() == 2)
        {
            var time = _clip.GetTime();
            var normalizedTime = time / _clip.GetDuration();
            var weight = 1d;
            var fadeInNormalizedTime = .1f;
            var fadeOutNormalizedTime = .1f;

            if (normalizedTime < fadeInNormalizedTime)
            {
                weight = normalizedTime / fadeInNormalizedTime;
            }
            else if (normalizedTime > (1 - fadeOutNormalizedTime))
            {
                weight = (1 - normalizedTime) / fadeOutNormalizedTime;
            }

            _mixer.SetInputWeight(0, 1 - (float)weight);
            _mixer.SetInputWeight(1, (float)weight);

            if (_clip.IsDone())
            {
                _mixer.DisconnectInput(1);
                _mixer.SetInputCount(1);
                _clip.Destroy();
            }
        }
        else
        {
            _mixer.SetInputWeight(0, 1);
        }
    }

    private void OnDestroy()
    {
        if (_graph.IsValid())
        {
            _graph.Destroy();
        }

        Curves.Dispose();
        Values.Dispose();
    }

    public void Play(AnimationClip clip)
    {
        if (_clip.IsValid())
        {
            _clip.Destroy();
        }

        _clip = AnimationClipPlayable.Create(_graph, clip);
        _clip.SetDuration(clip.length);

        _mixer.SetInputCount(2);
        _mixer.DisconnectInput(1);
        _mixer.ConnectInput(1, _clip, 0);
    }
}

public struct ReadCurveJob : IAnimationJob
{
    public NativeArray<PropertyStreamHandle> Curves;
    public NativeArray<float> Values;

    public void ProcessAnimation(AnimationStream stream)
    {
        for (var i = 0; i < Curves.Length; i++)
        {
            Values[i] = Curves[i].GetFloat(stream);
        }
    }

    public void ProcessRootMotion(AnimationStream stream) { }
}