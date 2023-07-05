using UnityEngine;

using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Chronos/ChronosRenderPipeline")]
public class ChronosRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField, Range(1, 2)]
    int renderScale = 1;

    public enum MSAAMode : int
    {
        Off = 1,
        _2x = 2,
        _4x = 4,
        _8x = 8
    }

    [SerializeField]
    bool HDR = true;

    [SerializeField]
    MSAAMode MSAA = MSAAMode.Off;
        
    [SerializeField]
    ChronosPostProcessingStack postStack;
        
    protected override RenderPipeline CreatePipeline()
    {
        return new ChronosRenderPipelineInstance(renderScale, (int)MSAA, postStack, HDR);
    }
     
}