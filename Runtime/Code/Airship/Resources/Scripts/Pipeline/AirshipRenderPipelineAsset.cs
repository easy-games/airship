using UnityEngine;

using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Airship/AirshipRenderPipeline")]
public class AirshipRenderPipelineAsset : RenderPipelineAsset
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
    AirshipPostProcessingStack postStack;
        
    protected override RenderPipeline CreatePipeline()
    {
        return new AirshipRenderPipelineInstance(renderScale, (int)MSAA, postStack, HDR);
    }
     
}