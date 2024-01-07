using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

[CreateAssetMenu(menuName = "Airship/PostProcessingStack")]
public class AirshipPostProcessingStack : ScriptableObject
{
    [NonSerialized]
    private Material colorGradeMaterial;

    [NonSerialized]
    static int mainTexId = Shader.PropertyToID("_CameraOpaqueTexture");
    static int bloomTexId = Shader.PropertyToID("_BloomTexture");

    [NonSerialized]
    static int bloomColorTextureId = Shader.PropertyToID("_BloomColorTexture");
    [NonSerialized]
    static int horizontalTextureId = Shader.PropertyToID("_DownscaleHorizontalId");
    [NonSerialized]
    static int verticalTextureId = Shader.PropertyToID("_DownscaleVerticalId");
    [NonSerialized]
    private Material downscaleMaterial;
    [NonSerialized]
    private Material horizontalBlurMaterial;
    [NonSerialized]
    private Material verticalBlurMaterial;
    [NonSerialized]
    private Material bloomMaterial;

    
    static int numBlurPasses = 6;

    [SerializeField, Range(0, 1)]
    float bloomCutoff = 0.95f;
    
    [SerializeField, Range(0, 2)]
    float bloomScale = 0.5f;

    [SerializeField, Range(0, 2)]
    float contrast = 1.0f;

    [SerializeField, Range(0, 2)]
    float saturation = 1.0f;
        
    [SerializeField, Range(0, 3)]
    float value = 1.0f;
    
    [SerializeField, Range(-1, 1)]
    float hue = 0.0f;

    [SerializeField, Range(0, 1)]
    float master = 1.0f;
    
    public void Render(ScriptableRenderContext context, CommandBuffer cmd, int cameraColorId, int screenWidth, int screenHeight, int halfResolutionMrtId,  RenderTexture targetTexture, bool colorGradeOnly)
    {
        //CommandBuffer cmd = CommandBufferPool.Get();

        //BuildBloom(context, cmd, screenWidth / 4, screenHeight / 4, quarterResolutionMrtId);
        BuildBloom(context, cmd, screenWidth / 2, screenHeight / 2, halfResolutionMrtId);

        if (colorGradeMaterial == null)
        {
            colorGradeMaterial = Resources.Load("ColorGrade") as Material;
        }
    
        if (colorGradeOnly == true)
        {
            colorGradeMaterial.SetFloat("BloomScale", 0);
            colorGradeMaterial.SetFloat("Contrast", 1);

            colorGradeMaterial.SetFloat("Hue", 0);
            colorGradeMaterial.SetFloat("Saturation", 1);
            colorGradeMaterial.SetFloat("Value", 1);
        }
        else
        {
            colorGradeMaterial.SetFloat("BloomScale", bloomScale);
            colorGradeMaterial.SetFloat("Contrast", contrast);

            colorGradeMaterial.SetFloat("Hue", hue);
            colorGradeMaterial.SetFloat("Saturation", saturation);
            colorGradeMaterial.SetFloat("Value", value);
            colorGradeMaterial.SetFloat("Master", master);
        }

        cmd.SetGlobalTexture(mainTexId, cameraColorId);
        
        if (targetTexture != null)
        {
            // If the camera has a specific render target, use it
            cmd.SetRenderTarget(targetTexture);
        }
        else
        {
            // If the camera does not have a specific render target, render to the screen
            // This is typically done by setting the render target to BuiltinRenderTextureType.CameraTarget
            cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
        }

        //cmd.SetRenderTarget(new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget, 0, CubemapFace.Unknown, -1));
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, colorGradeMaterial);
 
        CleanupBloom(cmd);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }


    public void BuildBloom(ScriptableRenderContext context, CommandBuffer cmd, int bufferWidth, int bufferHeight, int sourceTextureId)
    {

        if (bloomMaterial == null)
        {
            bloomMaterial = Resources.Load("BloomMat") as Material;
            if (bloomMaterial == null)
            {
                Debug.LogError("Failed to loaded bloomMat");
            }
        }
        if (horizontalBlurMaterial == null)
        {
            horizontalBlurMaterial = Resources.Load("HorizontalBlurMat") as Material;
        }
        if (verticalBlurMaterial == null)
        {
            verticalBlurMaterial = Resources.Load("VerticalBlurMat") as Material;
        }
        
        float kernelScale = 1.0f;
        horizontalBlurMaterial.SetVector("_BlurScale", new Vector4(1 * kernelScale, 0, 0, 0));
        horizontalBlurMaterial.SetVector("_TextureSize", new Vector4(bufferWidth, bufferHeight, 0, 0));

        verticalBlurMaterial.SetVector("_BlurScale", new Vector4(0, 1 * kernelScale, 0, 0));
        verticalBlurMaterial.SetVector("_TextureSize", new Vector4(bufferWidth, bufferHeight, 0, 0));
        
        //The blurred screen texture for frosting
        RenderTextureDescriptor textureDescBloom = new RenderTextureDescriptor();
        textureDescBloom.autoGenerateMips = false;
        textureDescBloom.colorFormat = RenderTextureFormat.Default;
        textureDescBloom.msaaSamples = 1;
        textureDescBloom.sRGB = false;
        textureDescBloom.useMipMap = false;
        textureDescBloom.width = bufferWidth;
        textureDescBloom.height = bufferHeight;
        textureDescBloom.enableRandomWrite = false;
        textureDescBloom.volumeDepth = 1;
        textureDescBloom.depthBufferBits = 24;
        textureDescBloom.dimension = TextureDimension.Tex2D;
        cmd.GetTemporaryRT(bloomColorTextureId, textureDescBloom, FilterMode.Bilinear);
        
        RenderTextureDescriptor textureDescA = new RenderTextureDescriptor();
        textureDescA.autoGenerateMips = false;
        textureDescA.colorFormat = RenderTextureFormat.Default;
        textureDescA.msaaSamples = 1;
        textureDescA.sRGB = false;
        textureDescA.useMipMap = false;
        textureDescA.width = bufferWidth;  //using quarter resolution of native target here
        textureDescA.height = bufferHeight;
        textureDescA.enableRandomWrite = false;
        textureDescA.volumeDepth = 1;
        textureDescA.depthBufferBits = 24;
        textureDescA.dimension = TextureDimension.Tex2D;

        cmd.GetTemporaryRT(horizontalTextureId, textureDescA, FilterMode.Bilinear);
        cmd.GetTemporaryRT(verticalTextureId, textureDescA, FilterMode.Bilinear);

        bloomMaterial.SetFloat("BloomCutoff", bloomCutoff);

        //Blit the source texture to the vertical texture with the bloom material
        cmd.SetRenderTarget(verticalTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetGlobalTexture(mainTexId, sourceTextureId); //texture to render with
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, bloomMaterial);

        for (int j = 0; j < numBlurPasses; j++)
        {
            //On the final pass, write directly to the output texture
            int writeRT = verticalTextureId;
            if (j == numBlurPasses - 1)
            {
                writeRT = bloomColorTextureId;
            }

            //Switch to the horizontal buffer, and render that 
            cmd.SetRenderTarget(horizontalTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.ClearRenderTarget(true, true, Color.black);
            cmd.SetGlobalTexture(mainTexId, verticalTextureId); //texture to render with
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, horizontalBlurMaterial);

            //Switch to the vertical buffer, and render that. 
            cmd.SetRenderTarget(writeRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.ClearRenderTarget(true, true, Color.black);
            cmd.SetGlobalTexture(mainTexId, horizontalTextureId); //texture to render with
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, verticalBlurMaterial);
        }

        cmd.ReleaseTemporaryRT(horizontalTextureId);
        cmd.ReleaseTemporaryRT(verticalTextureId);
    }

    public void CleanupBloom(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(bloomColorTextureId);
    }
}
