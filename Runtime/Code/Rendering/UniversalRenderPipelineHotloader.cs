using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UniversalRenderPipelineHotloader : MonoBehaviour {
    // [SerializeField]
    // private UniversalRenderPipelineAsset defaultAsset;
    [CanBeNull] public UniversalRenderPipelineAsset activeAsset;
    
    private void Start() {
        var time = Time.time;
        //GraphicsSettings.defaultRenderPipeline = defaultAsset;
        QualitySettings.renderPipeline = activeAsset;
        Debug.Log($"Changed pipeline to {QualitySettings.renderPipeline.name}", QualitySettings.renderPipeline);
        // UniversalRenderPipelineAsset pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
        // pipeline.renderers
        
        var timeTaken = Time.time - time;
        Debug.Log($"Time taken: {timeTaken}");
    }

    private void OnDestroy() {
        if (activeAsset == null) return;
        QualitySettings.renderPipeline = null;
    }
}
