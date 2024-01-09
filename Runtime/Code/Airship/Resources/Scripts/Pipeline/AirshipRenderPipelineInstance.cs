using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RendererUtils;

[LuauAPI]
public static class AirshipRenderPipelineStatistics
{
    public static int numPasses = 0;
    public static int numWorldTriangles = 0;
    public static int numMeshRenderers = 0;
    public static int numShadowCasters = 0;
    public static int numSkinnedMeshRenderers = 0;
    public static int numSkinnedTriangles = 0;
    public static int numTriangles = 0;
    public static int numVisibleSkinnedMeshRenderers = 0;
    public static int numVisibleMeshRenderers = 0;

    public static bool captureRenderingStats = false;
    public static void CaptureRenderingStats()
    {
        captureRenderingStats = true;
    }

    public static void Reset()
    {
        numMeshRenderers = 0;
        numShadowCasters = 0;
        numSkinnedMeshRenderers = 0;
        numSkinnedTriangles = 0;
        numTriangles = 0;
        numVisibleSkinnedMeshRenderers = 0;
        numVisibleMeshRenderers = 0;
        numPasses = 0;
    }

    public static void Print()
    {
 
        Debug.Log("numMeshRenderers: " + numMeshRenderers);
        Debug.Log("numShadowCasters: " + numShadowCasters);
        Debug.Log("numSkinnedMeshRenderers: " + numSkinnedMeshRenderers);
        Debug.Log("numSkinnedTriangles: " + numSkinnedTriangles);
        Debug.Log("numTriangles: " + numTriangles);
        Debug.Log("numVisibleSkinnedMeshRenderers: " + numVisibleSkinnedMeshRenderers);
        Debug.Log("numVisibleMeshRenderers: " + numVisibleMeshRenderers);
    }

    public static void ExtractStatsFromScene()
    {
        if (captureRenderingStats == false)
        {
            return;
        }

        Reset();

        MeshRenderer[] meshRenderers = GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        SkinnedMeshRenderer[] skinnedMeshRenderers = GameObject.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i].enabled == false)
            {
                continue;
            }

            numMeshRenderers++;
            MeshFilter meshFilter = meshRenderers[i].GetComponent<MeshFilter>();
            if (meshFilter)
            {
                numTriangles += meshFilter.sharedMesh.triangles.Length / 3;
            }
            if (meshRenderers[i].isVisible == true)
            {
                numVisibleMeshRenderers++;
            }
            if (meshRenderers[i].shadowCastingMode != ShadowCastingMode.Off)
            {
                numShadowCasters++;
            }
        }
        
        //Do skinned meshes now
        for (int i = 0; i < skinnedMeshRenderers.Length; i++)
        {
            if (skinnedMeshRenderers[i].enabled == false)
            {
                continue;
            }

            numSkinnedMeshRenderers++;
            
            if (skinnedMeshRenderers[i].sharedMesh)
            {
                numSkinnedTriangles += skinnedMeshRenderers[i].sharedMesh.triangles.Length / 3;
            }
            if (skinnedMeshRenderers[i].isVisible == true)
            {
                numVisibleSkinnedMeshRenderers++;
            }

            if (skinnedMeshRenderers[i].shadowCastingMode != ShadowCastingMode.Off)
            {
                numShadowCasters++;
            }
        }

        //Debug
        //Print();

        captureRenderingStats = false;
    }
}

public class AirshipRenderPipelineInstance : RenderPipeline
{
    public AirshipRenderPipelineInstance(float renderScaleSet, int MSAA, AirshipPostProcessingStack postStack, bool HDR)
    {
        hdr = HDR;
        msaaSamples = MSAA;
        renderScale = renderScaleSet;
        postProcessingStack = postStack;
                
        QualitySettings.antiAliasing = msaaSamples;
        this.msaaSamples = Mathf.Max(QualitySettings.antiAliasing, 1);
    }
     
    public class RenderTargetGroup
    {
        //Array of cameras
        public List<Camera> cameras = new();
        public CameraType cameraType = 0;
        //Render target or null
        public RenderTexture renderTexture;

        public bool drawAirshipOnly = true;
        public bool doPostProcessing = true;
        public bool forceClearBackground = false;
        public bool allowScaledRendering = true;
        public bool colorGradeOnly = false;
    };

    public MaterialPropertyBlock globalPropertyBlock;
    private float renderScale = 1;
    private int msaaSamples = 4;
    private const bool debugging = false;

    AirshipPostProcessingStack postProcessingStack;

    static int upscaledCameraColorTextureId = Shader.PropertyToID("_UpscaledCameraColorTexture");
    static int upscaledCameraColorTextureMrtId = Shader.PropertyToID("_UpscaledCameraColorTextureMrt");
    static int upscaledCameraDepthTextureId = Shader.PropertyToID("_UpscaledCameraDepthTexture");

    static int nativeScaledCameraColorTextureId = Shader.PropertyToID("_NativeSCaledCameraColorTexture");
    static int nativeScaledCameraColorTextureMrtId = Shader.PropertyToID("_NativeScaledCameraColorTextureMrt");
    static int nativeScaledCameraDepthTextureId = Shader.PropertyToID("_NativeScaledCameraDepthTexture");

    static int globalShadowTexture0Id = Shader.PropertyToID("_GlobalShadowTexture0");
    static int globalShadowTexture1Id = Shader.PropertyToID("_GlobalShadowTexture1");

    static int blurColorTextureId = Shader.PropertyToID("_BlurColorTexture");
    static int mainTexId = Shader.PropertyToID("_CameraOpaqueTexture");
    static int mainTexMrtId = Shader.PropertyToID("_CameraOpaqueTextureMrt");

    static int halfSizeTexId = Shader.PropertyToID("_CameraHalfSize");
    static int quarterSizeTexId = Shader.PropertyToID("_CameraQuarterSize");
    static int halfSizeTexMrtId = Shader.PropertyToID("_CameraHalfSizeMrt");
    static int quarterSizeTexMrtId = Shader.PropertyToID("_CameraQuarterSizeMrt");
    static int halfSizeDepthTextureId = Shader.PropertyToID("_CameraHalfSizeDepthTexture");
    static int quarterSizeDepthTextureId = Shader.PropertyToID("_CameraQuarterSizeDepthTexture");
    
    Vector4[] shAmbientData = new Vector4[9];
    Vector3 sunDirection = Vector3.down;
    
    [NonSerialized]
    private float capturedTime = 0;

    class MeshRendererDesc
    {
        public MeshRenderer renderer;
        public uint filter;
        public MeshRendererDesc(MeshRenderer renderer, uint filter)
        {
            this.renderer = renderer;
            this.filter = filter;
        }
    }
    
    const int shadowWidth = 2048;
    const int shadowHeight = 2048;
    readonly int[] cascadeSize = new int[] { 8, 64 };
    
    [NonSerialized]
    Camera[] shadowMapCamera = new Camera[2];
    [NonSerialized]
    GameObject[] shadowMapCameraObject = new GameObject[2];


    bool hdr = true;
    static int numBlurPasses = 6;
        
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
    [NonSerialized]
    Material errorMaterial;
    [NonSerialized]
    Material depthMaterial;
    
    [NonSerialized]
    private Texture2D ditherTexture;
    [NonSerialized]
    private string ditherTexturePath = "BaseTextures/BayerDither8x8";

 
    CommandBuffer reusedCmdBuffer = new()
    {
        name = "RenderTarget"
    };

    protected override void Dispose(bool disposing)
    {

    }

    private void SetupGlobalTextures()
    {
        //Think: Move this to the SRP?
        if (ditherTexture == null)
        {
            this.ditherTexture = Resources.Load<Texture2D>(ditherTexturePath); // AssetBridge.LoadAssetInternal<Texture2D>(this.ditherTexturePath, true);
        }

        Shader.SetGlobalTexture("_DitherTexture", ditherTexture);
    }
    private void DoShadowmapRendering(Camera camera, ScriptableRenderContext context, CommandBuffer cameraCmdBuffer)
    {
        PreRenderShadowmaps();
        RenderShadowmap(camera, context, cameraCmdBuffer, 0);
        RenderShadowmap(camera, context, cameraCmdBuffer, 1);
        PostRenderShadowmaps();
    }

    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        SetupGlobalTextures();

        SetupGlobalLightingPropertiesForRendering();

        //Cleanup
        CleanupShadowmapCameras(cameras);
        
        //create a array of render targets used by cameras, including a blank one for rendering directly to the backbuffer
        List<RenderTargetGroup> renderTargetGroup = new();

        foreach (var camera in cameras)
        {
#if UNITY_EDITOR            
            if (camera == null)
            {
                continue;
            }
            
            if (camera.gameObject.name == "Shadowmap Camera")
            {
                continue;
            }
#endif            

            //See if the camera is in the shadowMapCamera array, and if so skip
            if (shadowMapCamera != null)
            {
                bool foundCamera = false;
                for (int i = 0; i < shadowMapCamera.Length; i++)
                {
                    if (camera == shadowMapCamera[i])
                    {
                        foundCamera = true;
                        break;
                    }
                }
                if (foundCamera)
                {
                    continue;
                }
            }
            
            //add to the renderTargetGroup where the camera.renderTarget matches, or create a new renderTargetGroup
            bool found = false;
            foreach (var renderTarget in renderTargetGroup)
            {
                if (renderTarget.renderTexture == camera.targetTexture && renderTarget.cameraType == camera.cameraType)
                {
                    renderTarget.cameras.Add(camera);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                renderTargetGroup.Add(new RenderTargetGroup() { renderTexture = camera.targetTexture, cameras = new() { camera }, cameraType = camera.cameraType });
            }
        }
        
        //Setup rendering for previews
        foreach (RenderTargetGroup renderTarget in renderTargetGroup)
        {
            if (renderTarget.cameraType == CameraType.Preview)
            {
                renderTarget.drawAirshipOnly = false;
                renderTarget.forceClearBackground = true;
                renderTarget.colorGradeOnly = true;
            }
        }

        //render each renderTargetGroup that has textures
        foreach (RenderTargetGroup renderTarget in renderTargetGroup)
        {
            if (renderTarget.renderTexture != null && renderTarget.cameraType != CameraType.SceneView && renderTarget.cameraType != CameraType.Game)
            {
                RenderGroup(renderContext, renderTarget);
            }
        }

        //Do the game one now
        foreach (RenderTargetGroup renderTarget in renderTargetGroup)
        {
            if (renderTarget.renderTexture == null || renderTarget.cameraType == CameraType.SceneView || renderTarget.cameraType == CameraType.Game)
            {
                RenderGroup(renderContext, renderTarget);
            }
        }

        AirshipRenderPipelineStatistics.ExtractStatsFromScene();
    }

    void DrawGizmos(ScriptableRenderContext context, Camera camera)
    {
#if UNITY_EDITOR    
        if (Handles.ShouldRenderGizmos())
        {
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
#endif        
    }

    void RenderGroup(ScriptableRenderContext context, RenderTargetGroup group)
    {
        AirshipRenderPipelineStatistics.numPasses += 1;
        Camera rootCamera = group.cameras[0];

#if UNITY_EDITOR
        if (group.cameraType == CameraType.SceneView)
        {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(rootCamera);
        }
#endif

        //Draw opaques
        ShaderTagId[] shaderTagId;

        if (group.drawAirshipOnly == true)
        {
            shaderTagId = new ShaderTagId[]
            {
                    new("AirshipForwardPass")
            };
        }
        else
        {
            shaderTagId = new ShaderTagId[]
            {
                    new ShaderTagId("AirshipForwardPass"),
                    new ShaderTagId("ForwardBase"),
                    new ShaderTagId("Always"),
                    new ShaderTagId("ForwardAdd"),
                    new ShaderTagId("PrepassFinal"),
                    new ShaderTagId("Vertex"),
                    new ShaderTagId("VertexLMRGBM"),
                    new ShaderTagId("VertexLM")
            };
        }

        bool scaledRendering = renderScale != 1.0f && group.allowScaledRendering == true; 

        //Resolution of the resolved texture
        int nativeScreenWidth = rootCamera.pixelWidth;
        int nativeScreenHeight = rootCamera.pixelHeight;

        //Resolution of the actual rendering
        int upscaledRenderWidth = (int)(nativeScreenWidth * renderScale);
        int upscaledRenderHeight = (int)(nativeScreenHeight * renderScale);

        //Resolutoon of blurBuffer
        int blurBufferWidth = (int)Mathf.Ceil(nativeScreenWidth / 4);
        int blurBufferHeight = (int)Mathf.Ceil(nativeScreenHeight / 4);

        //Start rendering
        CommandBuffer cameraCmdBuffer = reusedCmdBuffer;
        cameraCmdBuffer.Clear();

        //Generate our shadowmaps
        DoShadowmapRendering(rootCamera, context, cameraCmdBuffer);

        //Grab our scenes temporary textures
        AllocateUpscaledRenderTextures(upscaledRenderWidth, upscaledRenderHeight, scaledRendering, cameraCmdBuffer);
        AllocateTemporaryRenderTextures(nativeScreenWidth, nativeScreenHeight, blurBufferWidth, blurBufferHeight, cameraCmdBuffer);
        
        context.ExecuteCommandBuffer(cameraCmdBuffer);
        cameraCmdBuffer.Clear();


        context.ExecuteCommandBuffer(cameraCmdBuffer);
        cameraCmdBuffer.Clear();

        //Start layering the cameras into this buffer
        //Eg: camera0 - clear all, draw world, draw sky
        //    camera1 - clear depth, draw fps hands
        foreach (Camera camera in group.cameras)
        {
            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = context.Cull(ref cullingParameters);
            context.SetupCameraProperties(camera);
            CameraClearFlags clearFlags = camera.clearFlags;

            //Set the renderTarget
            //because airship uses MRT, we have to capture to a few textures first, and then composite to the final texture later
            //!Note! Every time we call context.SetupCameraProperties, we have to rebind our render targets
            //       which is why we're doing this INSIDE the multi camera loop
            if (scaledRendering)
            {
                RenderTargetIdentifier[] upscaledCameraColorTextureArray = new RenderTargetIdentifier[2];
                upscaledCameraColorTextureArray[0] = upscaledCameraColorTextureId;
                upscaledCameraColorTextureArray[1] = upscaledCameraColorTextureMrtId;
                cameraCmdBuffer.SetRenderTarget(upscaledCameraColorTextureArray, upscaledCameraDepthTextureId);
            }
            else
            {
                RenderTargetIdentifier[] nativeCameraColorTextureArray = new RenderTargetIdentifier[2];
                nativeCameraColorTextureArray[0] = nativeScaledCameraColorTextureId;
                nativeCameraColorTextureArray[1] = nativeScaledCameraColorTextureMrtId;
                cameraCmdBuffer.SetRenderTarget(nativeCameraColorTextureArray, nativeScaledCameraDepthTextureId);
            }

            if (group.forceClearBackground)
            {
                //preview window color
                cameraCmdBuffer.ClearRenderTarget(RTClearFlags.ColorDepth, new Color(0.05f,0.05f,0.1f), 1, 0);
            }
            else
            {
                //if we're going to be rendering the skybox, or the first target in the chain, clear everything
                if (camera.clearFlags == CameraClearFlags.Skybox )//|| firstCamera == true)
                {
                    cameraCmdBuffer.ClearRenderTarget(
                        true,
                        true,
                        Color.black
                    );
                }
                else
                {
                    //Do what was requested on the camera itself
                    if (clearFlags == CameraClearFlags.Depth)
                    {
                        cameraCmdBuffer.ClearRenderTarget(RTClearFlags.Depth, camera.backgroundColor, 1, 0);
                    }
                    else
                    if (clearFlags == CameraClearFlags.Color || clearFlags == CameraClearFlags.Skybox || clearFlags == CameraClearFlags.SolidColor)
                    {
                        cameraCmdBuffer.ClearRenderTarget(RTClearFlags.ColorDepth, camera.backgroundColor, 1, 0);
                    }
                }
            }
            
            //Execute any clears
            context.ExecuteCommandBuffer(cameraCmdBuffer);
            cameraCmdBuffer.Clear();
                        
            //Opaque geometry
            RendererListDesc opaqueDesc = new RendererListDesc(shaderTagId, cullingResults, camera);
            opaqueDesc.renderQueueRange = RenderQueueRange.opaque;
            RendererList opaqueRenderList = context.CreateRendererList(opaqueDesc);
            cameraCmdBuffer.DrawRendererList(opaqueRenderList);

            //skybox (if required)
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            {
                RendererList skyRendererList = context.CreateSkyboxRendererList(camera);
                cameraCmdBuffer.DrawRendererList(skyRendererList);
            }
 
            //Transparent geometry
            RendererListDesc transparentDesc = new RendererListDesc(shaderTagId, cullingResults, camera);
            transparentDesc.renderQueueRange = RenderQueueRange.transparent;
            transparentDesc.sortingCriteria = SortingCriteria.CommonTransparent;
            RendererList transparentRenderList = context.CreateRendererList(transparentDesc);
            cameraCmdBuffer.DrawRendererList(transparentRenderList);
                        
            //Draw in Canvases
            DrawCanvases(cullingResults, context, camera, cameraCmdBuffer);

            //Draw in default stuff ("pink" errorshader objects)
            DrawDefaultPipeline(cullingResults, context, camera, cameraCmdBuffer);

            //Draw in gizmos
            DrawGizmos(context, camera);

            //Execute
            context.ExecuteCommandBuffer(cameraCmdBuffer);
            cameraCmdBuffer.Clear();
        }

        //Take what we've rendered (at high res or other) and put it into the nativeTextures
        //So after this point, nativeTexture contains our scene regardless if its scaledRendering or not
        BuildResolvedTextures(context, cameraCmdBuffer, scaledRendering);
                
        //Build downscaled textures from our "resolved texture"
        BuildHalfAndQuarterSizedTextures(context, cameraCmdBuffer, nativeScreenWidth, nativeScreenHeight);
        
        //Build our Frosted Glass Texture
        BuildFrostedGlassBlur(context, cameraCmdBuffer, blurBufferWidth, blurBufferHeight, blurColorTextureId, quarterSizeTexId);

        //Let the post stack final composite run now  
        postProcessingStack.Render(context, cameraCmdBuffer, nativeScaledCameraColorTextureId, nativeScreenWidth, nativeScreenHeight, halfSizeTexMrtId, group.renderTexture, group.colorGradeOnly);


        //Free the shadow texture
        cameraCmdBuffer.ReleaseTemporaryRT(globalShadowTexture0Id);
        cameraCmdBuffer.ReleaseTemporaryRT(globalShadowTexture1Id);

        //Free up the scaled rendering RTs
        if (scaledRendering)
        {
            cameraCmdBuffer.ReleaseTemporaryRT(upscaledCameraColorTextureId);
            cameraCmdBuffer.ReleaseTemporaryRT(upscaledCameraColorTextureMrtId);
            cameraCmdBuffer.ReleaseTemporaryRT(upscaledCameraDepthTextureId);
        }

        //Free the temporarily allocated textures
        cameraCmdBuffer.ReleaseTemporaryRT(nativeScaledCameraColorTextureId);
        cameraCmdBuffer.ReleaseTemporaryRT(nativeScaledCameraColorTextureMrtId);
        cameraCmdBuffer.ReleaseTemporaryRT(nativeScaledCameraDepthTextureId);

        cameraCmdBuffer.ReleaseTemporaryRT(blurColorTextureId);
        cameraCmdBuffer.ReleaseTemporaryRT(halfSizeTexId);
        cameraCmdBuffer.ReleaseTemporaryRT(quarterSizeTexId);


        //Final execute of all the frees
        context.ExecuteCommandBuffer(cameraCmdBuffer);
        cameraCmdBuffer.Clear();

        //Submit and quit
        context.Submit();
      
    }

    /*
     *     }
            else
            {
                if (camera.targetTexture != null)
                {
                    // If the camera has a specific render target, use it
                    cameraCmdBuffer.SetRenderTarget(camera.targetTexture);
                }
                else
                {
                    // If the camera does not have a specific render target, render to the screen
                    cameraCmdBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                }
            }
*/

    void AllocateUpscaledRenderTextures(int upscaledRenderWidth, int upscaledRenderHeight, bool scaledRendering, CommandBuffer cameraCmdBuffer)
    {
        if (scaledRendering == false)
        {
            return;
        }

        //we render into this, possibly at double scale
        RenderTextureDescriptor textureDesc = new RenderTextureDescriptor();
        textureDesc.autoGenerateMips = false;
        textureDesc.colorFormat = RenderTextureFormat.ARGB32;
        textureDesc.msaaSamples = msaaSamples;
        textureDesc.sRGB = false;
        textureDesc.useMipMap = false;
        textureDesc.width = upscaledRenderWidth;
        textureDesc.height = upscaledRenderHeight;
        textureDesc.enableRandomWrite = false;
        textureDesc.volumeDepth = 1;
        textureDesc.depthBufferBits = 24;
        textureDesc.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(upscaledCameraColorTextureId, textureDesc, FilterMode.Bilinear);

        RenderTextureDescriptor textureDescMrt = new RenderTextureDescriptor();
        textureDescMrt.autoGenerateMips = false;
        textureDescMrt.colorFormat = RenderTextureFormat.ARGB32;
        textureDescMrt.msaaSamples = msaaSamples;
        textureDescMrt.sRGB = false;
        textureDescMrt.useMipMap = false;
        textureDescMrt.width = upscaledRenderWidth;
        textureDescMrt.height = upscaledRenderHeight;
        textureDescMrt.enableRandomWrite = false;
        textureDescMrt.volumeDepth = 1;
        textureDescMrt.depthBufferBits = 24;
        textureDescMrt.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(upscaledCameraColorTextureMrtId, textureDescMrt, FilterMode.Bilinear);

        RenderTextureDescriptor depthDesc = new RenderTextureDescriptor();
        depthDesc.autoGenerateMips = false;
        depthDesc.colorFormat = RenderTextureFormat.RFloat;
        depthDesc.msaaSamples = msaaSamples;
        depthDesc.sRGB = false;
        depthDesc.useMipMap = false;
        depthDesc.width = upscaledRenderWidth;
        depthDesc.height = upscaledRenderHeight;
        depthDesc.enableRandomWrite = false;
        depthDesc.volumeDepth = 1;
        depthDesc.depthBufferBits = 24;
        depthDesc.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(upscaledCameraDepthTextureId, depthDesc, FilterMode.Point);
    }

    void AllocateTemporaryRenderTextures(int nativeScreenWidth, int nativeScreenHeight, int blurBufferWidth, int blurBufferHeight, CommandBuffer cameraCmdBuffer)
    {
        //Final resolved texture
        RenderTextureDescriptor resolvedTextureDesc = new RenderTextureDescriptor();
        resolvedTextureDesc.autoGenerateMips = false;
        resolvedTextureDesc.colorFormat = RenderTextureFormat.ARGB32;
        resolvedTextureDesc.msaaSamples = 1;
        resolvedTextureDesc.sRGB = false;
        resolvedTextureDesc.useMipMap = false;
        resolvedTextureDesc.width = nativeScreenWidth;
        resolvedTextureDesc.height = nativeScreenHeight;
        resolvedTextureDesc.enableRandomWrite = false;
        resolvedTextureDesc.volumeDepth = 1;
        resolvedTextureDesc.depthBufferBits = 24;
        resolvedTextureDesc.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(nativeScaledCameraColorTextureId, resolvedTextureDesc, FilterMode.Bilinear);

        RenderTextureDescriptor resolvedTextureDescMrt = new RenderTextureDescriptor();
        resolvedTextureDescMrt.autoGenerateMips = false;
        resolvedTextureDescMrt.colorFormat = RenderTextureFormat.ARGB32;
        resolvedTextureDescMrt.msaaSamples = 1;
        resolvedTextureDescMrt.sRGB = false;
        resolvedTextureDescMrt.useMipMap = false;
        resolvedTextureDescMrt.width = nativeScreenWidth;
        resolvedTextureDescMrt.height = nativeScreenHeight;
        resolvedTextureDescMrt.enableRandomWrite = false;
        resolvedTextureDescMrt.volumeDepth = 1;
        resolvedTextureDescMrt.depthBufferBits = 24;
        resolvedTextureDescMrt.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(nativeScaledCameraColorTextureMrtId, resolvedTextureDescMrt, FilterMode.Bilinear);

        RenderTextureDescriptor textureDepthDesc = new RenderTextureDescriptor();
        textureDepthDesc.autoGenerateMips = false;
        textureDepthDesc.colorFormat = RenderTextureFormat.RFloat;
        textureDepthDesc.msaaSamples = 1;
        textureDepthDesc.sRGB = false;
        textureDepthDesc.useMipMap = false;
        textureDepthDesc.width = nativeScreenWidth;
        textureDepthDesc.height = nativeScreenHeight;
        textureDepthDesc.enableRandomWrite = false;
        textureDepthDesc.volumeDepth = 1;
        textureDepthDesc.depthBufferBits = 24;
        textureDepthDesc.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(nativeScaledCameraDepthTextureId, textureDepthDesc, FilterMode.Point);
        

        //The blurred screen texture for frosting
        RenderTextureDescriptor textureDescBlur = new RenderTextureDescriptor();
        textureDescBlur.autoGenerateMips = false;
        textureDescBlur.colorFormat = RenderTextureFormat.ARGB32;
        textureDescBlur.msaaSamples = 1;
        textureDescBlur.sRGB = false;
        textureDescBlur.useMipMap = false;
        textureDescBlur.width = blurBufferWidth;
        textureDescBlur.height = blurBufferHeight;
        textureDescBlur.enableRandomWrite = false;
        textureDescBlur.volumeDepth = 1;
        textureDescBlur.depthBufferBits = 24;
        textureDescBlur.dimension = TextureDimension.Tex2D;
        cameraCmdBuffer.GetTemporaryRT(blurColorTextureId, textureDescBlur, FilterMode.Bilinear);
    }


    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("UNITY_EDITOR")]
    void DrawDefaultPipeline(CullingResults cullingResults, ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
    {
        
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/AirshipErrorShader");
            errorMaterial = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        ShaderTagId[] passNames = { 
            new ShaderTagId("ForwardBase"), 
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardAdd"),
            new ShaderTagId("PrepassFinal"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        RendererListDesc rendererDesc = new RendererListDesc(passNames, cullingResults, camera);
        rendererDesc.overrideMaterial = errorMaterial;
        
        RendererList rendererList = context.CreateRendererList(rendererDesc);
        commandBuffer.DrawRendererList(rendererList);
    }

    void DrawCanvases(CullingResults cullingResults, ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer)
    {
        int layerMask = LayerMask.NameToLayer("UI");

        ShaderTagId[] passNames = { new ShaderTagId("ForwardBase"), new ShaderTagId("SRPDefaultUnlit") };

        RendererListDesc rendererDesc = new RendererListDesc(passNames, cullingResults, camera);
        rendererDesc.layerMask = 1 << layerMask;
        
        RendererList rendererList = context.CreateRendererList(rendererDesc);
        commandBuffer.DrawRendererList(rendererList);
    }


    public void BuildResolvedTextures(ScriptableRenderContext context, CommandBuffer cmd, bool scaledRendering)
    {
        //Either the upscaled renderTarget textures are bound right now (scaledRendering)
        //or the native renderTarget textures are bound right now (not scaledRendering)

        if (scaledRendering == false)
        {
            //It's native, so we dont have anything to resolve
            return;
        }

        if (downscaleMaterial == null)
        {
            downscaleMaterial = Resources.Load("DownScaleMat") as Material;
            if (downscaleMaterial == null)
            {
                Debug.LogError("Missing Material: DownScaleMat");
            }
        }

        //Final Resolve!
        //Blit the source texture to the resolved texture
        RenderTargetIdentifier[] rt = new RenderTargetIdentifier[2];
        rt[0] = new RenderTargetIdentifier(nativeScaledCameraColorTextureId, 0, CubemapFace.Unknown, 0);
        rt[1] = new RenderTargetIdentifier(nativeScaledCameraColorTextureMrtId, 0, CubemapFace.Unknown, 0);

        cmd.SetRenderTarget(rt, nativeScaledCameraDepthTextureId);
        cmd.ClearRenderTarget(true, true, Color.black);
        
        cmd.SetGlobalTexture(mainTexId, upscaledCameraColorTextureId); //texture to render with
        cmd.SetGlobalTexture(mainTexMrtId, upscaledCameraColorTextureMrtId); //texture to render with
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, downscaleMaterial);
    }

    public void BuildHalfAndQuarterSizedTextures(ScriptableRenderContext context, CommandBuffer cmd, int nativeSceneWidth, int nativeSceneHeight)
    {
        
        if (downscaleMaterial == null)
        {
            downscaleMaterial = Resources.Load("DownScaleMat") as Material;
            if (downscaleMaterial == null)
            {
                Debug.LogError("Missing Material: DownScaleMat");
            }
        }
         
        RenderTextureDescriptor halfSizeTextureDesc = new RenderTextureDescriptor();
        halfSizeTextureDesc.autoGenerateMips = false;
        halfSizeTextureDesc.colorFormat = RenderTextureFormat.Default;
        halfSizeTextureDesc.msaaSamples = 1;
        halfSizeTextureDesc.sRGB = false;
        halfSizeTextureDesc.useMipMap = false;
        halfSizeTextureDesc.width = nativeSceneWidth / 2;
        halfSizeTextureDesc.height = nativeSceneHeight / 2;
        halfSizeTextureDesc.enableRandomWrite = false;
        halfSizeTextureDesc.volumeDepth = 1;
        halfSizeTextureDesc.depthBufferBits = 24;
        halfSizeTextureDesc.dimension = TextureDimension.Tex2D;

        RenderTextureDescriptor halfSizeDepthDesc = new RenderTextureDescriptor();
        halfSizeDepthDesc.autoGenerateMips = false;
        halfSizeDepthDesc.colorFormat = RenderTextureFormat.RFloat;
        halfSizeDepthDesc.msaaSamples = 1;
        halfSizeDepthDesc.sRGB = false;
        halfSizeDepthDesc.useMipMap = false;
        halfSizeDepthDesc.width = nativeSceneWidth / 2; 
        halfSizeDepthDesc.height = nativeSceneHeight / 2;
        halfSizeDepthDesc.enableRandomWrite = false;
        halfSizeDepthDesc.volumeDepth = 1;
        halfSizeDepthDesc.depthBufferBits = 24;
        halfSizeDepthDesc.dimension = TextureDimension.Tex2D;
        
        RenderTextureDescriptor quarterSizeTextureDesc = new RenderTextureDescriptor();
        quarterSizeTextureDesc.autoGenerateMips = false;
        quarterSizeTextureDesc.colorFormat = RenderTextureFormat.Default;
        quarterSizeTextureDesc.msaaSamples = 1;
        quarterSizeTextureDesc.sRGB = false;
        quarterSizeTextureDesc.useMipMap = false;
        quarterSizeTextureDesc.width = nativeSceneWidth / 4;
        quarterSizeTextureDesc.height = nativeSceneHeight / 4;
        quarterSizeTextureDesc.enableRandomWrite = false;
        quarterSizeTextureDesc.volumeDepth = 1;
        quarterSizeTextureDesc.depthBufferBits = 24;
        quarterSizeTextureDesc.dimension = TextureDimension.Tex2D;

        RenderTextureDescriptor quarterSizeDepthDesc = new RenderTextureDescriptor();
        quarterSizeDepthDesc.autoGenerateMips = false;
        quarterSizeDepthDesc.colorFormat = RenderTextureFormat.RFloat;
        quarterSizeDepthDesc.msaaSamples = 1;
        quarterSizeDepthDesc.sRGB = false;
        quarterSizeDepthDesc.useMipMap = false;
        quarterSizeDepthDesc.width = nativeSceneWidth / 4;
        quarterSizeDepthDesc.height = nativeSceneHeight / 4;
        quarterSizeDepthDesc.enableRandomWrite = false;
        quarterSizeDepthDesc.volumeDepth = 1;
        quarterSizeDepthDesc.depthBufferBits = 24;
        quarterSizeDepthDesc.dimension = TextureDimension.Tex2D;
        

        cmd.GetTemporaryRT(halfSizeTexId, halfSizeTextureDesc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(quarterSizeTexId, quarterSizeTextureDesc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(halfSizeTexMrtId, halfSizeTextureDesc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(quarterSizeTexMrtId, quarterSizeTextureDesc, FilterMode.Bilinear);
        cmd.GetTemporaryRT(halfSizeDepthTextureId, halfSizeDepthDesc, FilterMode.Point);
        cmd.GetTemporaryRT(quarterSizeDepthTextureId, quarterSizeDepthDesc, FilterMode.Point);

        RenderTargetIdentifier[] halfSizeRt = new RenderTargetIdentifier[2];
        halfSizeRt[0] = new RenderTargetIdentifier(halfSizeTexId, 0, CubemapFace.Unknown, 0);
        halfSizeRt[1] = new RenderTargetIdentifier(halfSizeTexMrtId, 0, CubemapFace.Unknown, 0);

        //Blit the source texture to the halfSize texture
        cmd.SetRenderTarget(halfSizeRt, halfSizeDepthTextureId);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetGlobalTexture(mainTexId, nativeScaledCameraColorTextureId); //texture to render with
        cmd.SetGlobalTexture(mainTexMrtId, nativeScaledCameraColorTextureMrtId); //texture to render with
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, downscaleMaterial);

        RenderTargetIdentifier[] quarterSizeRt = new RenderTargetIdentifier[2];
        quarterSizeRt[0] = new RenderTargetIdentifier(quarterSizeTexId, 0, CubemapFace.Unknown, 0);
        quarterSizeRt[1] = new RenderTargetIdentifier(quarterSizeTexMrtId, 0, CubemapFace.Unknown, 0);

        //Blit the halfSize texture to the quarterSize texture
        cmd.SetRenderTarget(quarterSizeRt,quarterSizeDepthTextureId);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetGlobalTexture(mainTexId, halfSizeTexId); //texture to render with
        cmd.SetGlobalTexture(mainTexMrtId, halfSizeTexMrtId); //texture to render with
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, downscaleMaterial);
    }

    public void BuildFrostedGlassBlur(ScriptableRenderContext context, CommandBuffer cmd, int bufferWidth, int bufferHeight, int outputTextureId, int sourceTextureId)
    {
        if (horizontalBlurMaterial == null)
        {
            horizontalBlurMaterial = Resources.Load("HorizontalBlurMat") as Material;
            if (horizontalBlurMaterial == null)
            {
                Debug.LogError("Missing Material: HorizontalBlurMat");
            }

        }
        if (verticalBlurMaterial == null)
        {
            verticalBlurMaterial = Resources.Load("VerticalBlurMat") as Material;
            if (verticalBlurMaterial == null)
            {
                Debug.LogError("Missing Material: VerticalBlurMat");
            }

        }
 
        float kernelScale = 1.0f;
        horizontalBlurMaterial.SetVector("_BlurScale", new Vector4(1 * kernelScale, 0, 0, 0));
        horizontalBlurMaterial.SetVector("_TextureSize", new Vector4(bufferWidth, bufferHeight, 0, 0));
        
        verticalBlurMaterial.SetVector("_BlurScale", new Vector4(0, 1 * kernelScale, 0, 0));
        verticalBlurMaterial.SetVector("_TextureSize", new Vector4(bufferWidth, bufferHeight, 0, 0));
        
        RenderTextureDescriptor textureDescA = new RenderTextureDescriptor();
        textureDescA.autoGenerateMips = false;
        textureDescA.colorFormat = RenderTextureFormat.Default;
        textureDescA.msaaSamples = 1;
        textureDescA.sRGB = false;
        textureDescA.useMipMap = false;
        textureDescA.width = bufferWidth;
        textureDescA.height = bufferHeight;
        textureDescA.enableRandomWrite = false;
        textureDescA.volumeDepth = 1;
        textureDescA.depthBufferBits = 24;
        textureDescA.dimension = TextureDimension.Tex2D;
        
        cmd.GetTemporaryRT(horizontalTextureId, textureDescA, FilterMode.Bilinear);
        cmd.GetTemporaryRT(verticalTextureId, textureDescA, FilterMode.Bilinear);
        
        int inputTextureId = sourceTextureId;
        for (int j = 0; j < numBlurPasses; j++)
        {
            //On the final pass, write directly to the output texture
            int writeRT = verticalTextureId;
            if (j == numBlurPasses - 1)
            {
                writeRT = outputTextureId;
            }

            //Switch to the horizontal buffer, and render that 
            cmd.SetRenderTarget(horizontalTextureId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.ClearRenderTarget(true, true, Color.black);
            cmd.SetGlobalTexture(mainTexId, inputTextureId); //texture to render with
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, horizontalBlurMaterial);

            //Switch to the vertical buffer, and render that. 
            cmd.SetRenderTarget(writeRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.ClearRenderTarget(true, true, Color.black);
            cmd.SetGlobalTexture(mainTexId, horizontalTextureId); //texture to render with
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, verticalBlurMaterial);

            //After the first pass the inputTextureId changes to the last rendered target
            inputTextureId = writeRT;
        }

        cmd.ReleaseTemporaryRT(horizontalTextureId);
        cmd.ReleaseTemporaryRT(verticalTextureId);
    }

    private Bounds CalculateFrustumBounds(Camera camera, Matrix4x4 worldToLightspace, float maxDistance)
    {
        var worldFrustumCorners = new Vector3[8];
        CalculateFrustumCorners(camera, camera.nearClipPlane, worldFrustumCorners);
        CalculateFrustumCorners(camera, maxDistance, worldFrustumCorners, 4);

        //Todo: trim this down 
        float maxZ = 256;
        Vector3 cameraPos = camera.transform.position;
        Vector3 cameraPosLightspace = worldToLightspace.MultiplyPoint(cameraPos);

        //Transform these points into light space to encapsulate them
        var lightViewSpaceCorners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            lightViewSpaceCorners[i] = worldToLightspace.MultiplyPoint(worldFrustumCorners[i]);
        }

        //Clip these points to the max distance on the X and Y axis
        for (int i = 0; i < 8; i++)
        {
           //lightViewSpaceCorners[i].x = Mathf.Clamp(lightViewSpaceCorners[i].x, cameraPosLightspace.x- maxDistance, cameraPosLightspace.x+maxDistance);
           //lightViewSpaceCorners[i].y = Mathf.Clamp(lightViewSpaceCorners[i].y, cameraPosLightspace.y - maxDistance, cameraPosLightspace.y+maxDistance);
         // lightViewSpaceCorners[i].z = Mathf.Clamp(lightViewSpaceCorners[i].z, cameraPosLightspace.z - maxZ, cameraPosLightspace.z +maxZ);
        }

        //Encapsulate the frustrum as seen from the light
        var frustumBounds = new Bounds(lightViewSpaceCorners[0], Vector3.zero);
        for (int i = 1; i < lightViewSpaceCorners.Length; i++)
        {
            frustumBounds.Encapsulate(lightViewSpaceCorners[i]);
        }

        //Add the corners to make the size always the same on xy
        // frustumBounds.Encapsulate(new Vector3(-maxDistance, -maxDistance, -maxDistance));
        //frustumBounds.Encapsulate(new Vector3(maxDistance, maxDistance, maxDistance));

        //Push the min up to capture more shadowcasters
        //Vector3 highestShadowWorldPoint = camera.transform.position + new Vector3(0, 50, 0);
        //frustumBounds.Encapsulate(worldToLightspace.MultiplyPoint(highestShadowWorldPoint));
        //Vector3 lowestShadowWorldPoint = camera.transform.position + new Vector3(0, -50, 0);
        //frustumBounds.Encapsulate(worldToLightspace.MultiplyPoint(lowestShadowWorldPoint));
        frustumBounds.min = new Vector3(frustumBounds.min.x, frustumBounds.min.y, cameraPosLightspace.z - maxZ);
        frustumBounds.max = new Vector3(frustumBounds.max.x, frustumBounds.max.y, cameraPosLightspace.z + maxZ);


        //Take the center
        Vector3 center = frustumBounds.center;
        //create a new bounds with fixed x and y size
        frustumBounds = new Bounds(center, new Vector3(maxDistance * 2, maxDistance * 2, maxZ * 2));


        //calculate worldTexelsize
        Vector3 worldTexelSize = new Vector3(frustumBounds.size.x / shadowWidth, frustumBounds.size.y/ shadowHeight, 1);
          
        //Snap the x and y mins to worldTexelSize 
        Vector3 min = frustumBounds.min;
        min.x = Mathf.Floor(min.x / worldTexelSize.x) * worldTexelSize.x;
        min.y = Mathf.Floor(min.y / worldTexelSize.y) * worldTexelSize.y;
        frustumBounds.min = min;
        //maxes
        Vector3 max = frustumBounds.max;
        max.x = Mathf.Floor(max.x / worldTexelSize.x) * worldTexelSize.x;
        max.y = Mathf.Floor(max.y / worldTexelSize.y) * worldTexelSize.y;
        frustumBounds.max = max;

        return frustumBounds;
    }

    private void CalculateFrustumCorners(Camera camera, float distance, Vector3[] corners, int startIndex = 0)
    {
        // Top left
        corners[startIndex] = camera.ViewportToWorldPoint(new Vector3(0, 1, distance));
        // Top right
        corners[startIndex + 1] = camera.ViewportToWorldPoint(new Vector3(1, 1, distance));
        // Bottom left
        corners[startIndex + 2] = camera.ViewportToWorldPoint(new Vector3(0, 0, distance));
        // Bottom right
        corners[startIndex + 3] = camera.ViewportToWorldPoint(new Vector3(1, 0, distance));
    }

    Camera[] CleanupShadowmapCameras(Camera[] sceneCameras)
    {
     
        //Sanitize the scene, we only want one of these.
        foreach (var camera in sceneCameras)
        {
            //See if it exists in the shadowmapCamera array
            bool found = false;
            foreach (var validCamera in shadowMapCamera)
            {
                if (validCamera == camera)
                {
                    found = true;
                    break;
                }
            }
            if (found)
            {
                continue;
            }
                       
            if (camera.name == "Shadowmap Camera"  || camera.name == "Shadowmap Camera 0" || camera.name == "Shadowmap Camera 1")
            {
                if (Application.isPlaying == true)
                {
                    GameObject.Destroy(camera.gameObject);
                }
                else
                {
                    GameObject.DestroyImmediate(camera.gameObject);
                }
            }
        }
        return sceneCameras;
    }
 


    void PreRenderShadowmaps()
    {
        //We want to be able to turn shadow casting off on certain objects
        //Because we cant filter for this directly, we need to move stuff to a different renderFilterLayer

      
        
        
        /*@@
        List<VoxelWorldMeshUpdater> meshUpdaters = VoxelWorldStuff.SingletonClassManager<VoxelWorldMeshUpdater>.Instance.GetAllActiveItems();
        
        foreach (VoxelWorldMeshUpdater updater in meshUpdaters)
        {
            Renderer renderer = updater.GetRenderer();
            
            if (renderer && renderer.shadowCastingMode == ShadowCastingMode.Off)
            {
                updater.SetStoredFilter(renderer.renderingLayerMask);
                renderer.renderingLayerMask = 1 << 15;
               
            }
        }*/
}

void PostRenderShadowmaps()
    {
        /*
        List<VoxelWorldMeshUpdater> meshUpdaters = VoxelWorldStuff.SingletonClassManager<VoxelWorldMeshUpdater>.Instance.GetAllActiveItems();

        //Put the filters back
        foreach (VoxelWorldMeshUpdater updater in meshUpdaters)
        {
            Renderer renderer = updater.GetRenderer();
            if (renderer && renderer.shadowCastingMode == ShadowCastingMode.Off)
            {
                renderer.renderingLayerMask = updater.GetStoredFilter();
            }
        }*/
    }

    void RenderShadowmap(Camera mainCamera, ScriptableRenderContext context, CommandBuffer commandBuffer, int index)
    {
        // commandBuffer.BeginSample("Shadowmaps");

        int renderTargetId = 0;
        if (index == 0)
        {
            renderTargetId = globalShadowTexture0Id;
        }
        if (index == 1)
        {
            renderTargetId = globalShadowTexture1Id;
        }
        

        //Generate a shadowmap rendertarget
        if (true)
        {
            RenderTextureDescriptor shadowTextureDesc = new RenderTextureDescriptor();
            shadowTextureDesc.autoGenerateMips = false;
            shadowTextureDesc.colorFormat = RenderTextureFormat.Shadowmap;
            shadowTextureDesc.msaaSamples = 1;
            shadowTextureDesc.sRGB = false;
            shadowTextureDesc.useMipMap = false;
            shadowTextureDesc.width = shadowWidth;
            shadowTextureDesc.height = shadowHeight;
            shadowTextureDesc.enableRandomWrite = false;
            shadowTextureDesc.volumeDepth = 1;
            shadowTextureDesc.depthBufferBits = 24;
            shadowTextureDesc.dimension = TextureDimension.Tex2D;
            shadowTextureDesc.shadowSamplingMode = ShadowSamplingMode.CompareDepths;

            commandBuffer.GetTemporaryRT(renderTargetId, shadowTextureDesc, FilterMode.Bilinear);
        }

        if (depthMaterial == null)
        {
            Shader depthShader = Shader.Find("Airship/DepthToTexture");
            depthMaterial = new Material(depthShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        //Generate a camera that will be used to render the shadowmap

        //Check to see if "Shadowmap camera" already exists, and use that
        if (shadowMapCamera[index] == null)
        {

            // Get all root objects in the scene
            shadowMapCameraObject[index] = GameObject.Find("Shadowmap Camera " + index);
            if (shadowMapCameraObject[index])
            {
                shadowMapCamera[index] = shadowMapCameraObject[index].GetComponent<Camera>();
            }

            if (shadowMapCamera[index] == null)
            {
                //Debug.Log("Didn't find shadowmap camera " + index);
            }
        } 
         
        //create it?
        if (shadowMapCamera[index] == null)
        {
            //Debug.Log("Creating shadowmap Camera");
            shadowMapCameraObject[index] = new GameObject("Shadowmap Camera "+ index);
            shadowMapCamera[index] = shadowMapCameraObject[index].AddComponent<Camera>();
            shadowMapCamera[index].gameObject.hideFlags = HideFlags.HideAndDontSave;

            //Move to destroyOnLoad
            //GameObject.DontDestroyOnLoad(shadowMapCameraObject);
        }

        //Set camera to orthagonal 
        Camera shadowCamera = shadowMapCamera[index];
        shadowCamera.orthographic = true;
     
        //Set the camera to render only to the depth buffer
        shadowCamera.clearFlags = 0;
        shadowCamera.depthTextureMode = DepthTextureMode.None;
        shadowCamera.renderingPath = RenderingPath.Forward;
        shadowCamera.allowMSAA = false;
        shadowCamera.allowHDR = false;
        shadowCamera.useOcclusionCulling = false;
        shadowCamera.cullingMask = -1;
        shadowCamera.aspect = 1;
                
        
        
        // Position the shadowmap camera to cover the main camera's frustum
        float maxDistance = cascadeSize[index]; // Set this to the number of units you want to capture

        shadowCamera.transform.position = Vector3.zero;

        if (Math.Abs(Vector3.Dot(Vector3.up, sunDirection)) > 0.95f)
        {
            shadowCamera.transform.rotation = Quaternion.LookRotation(sunDirection, Vector3.right);
        }
        else
        {
            shadowCamera.transform.rotation = Quaternion.LookRotation(sunDirection, Vector3.up);
        }
 
        Matrix4x4 lightToWorld = shadowCamera.transform.localToWorldMatrix;
        Matrix4x4 worldToLight = shadowCamera.transform.worldToLocalMatrix;
              
   
        //Build a lightspace box covering what we want, enforced to be maxDistance
        Bounds frustumBoundsLightspace = CalculateFrustumBounds(mainCamera, worldToLight, maxDistance);
        Vector3 snappedLightspaceFrustumCenter = frustumBoundsLightspace.center;

        Vector3 frustumCenter = lightToWorld.MultiplyPoint(snappedLightspaceFrustumCenter);
        shadowCamera.transform.position = frustumCenter;

        // shadowCamera clipping planes to cover the bounds returned
        shadowCamera.nearClipPlane = -frustumBoundsLightspace.size.z / 2;
        shadowCamera.farClipPlane = frustumBoundsLightspace.size.z / 2;

        //This should always just be maxDistance, but we'll calculate it to be sure
        shadowCamera.orthographicSize = Mathf.Max(frustumBoundsLightspace.size.x, frustumBoundsLightspace.size.y) / 2;
        //Debug.Log("Size" + shadowMapCamera.orthographicSize);
               
        ShaderTagId shaderTagId = new("AirshipShadowPass");
        shadowCamera.TryGetCullingParameters(out var cullingParameters);
        cullingParameters.cullingOptions = CullingOptions.ShadowCasters;
        shadowCamera.overrideSceneCullingMask = 0;

        CullingResults cullingResults = context.Cull(ref cullingParameters);

        RendererListDesc rendererDesc = new RendererListDesc(shaderTagId, cullingResults, shadowCamera);
        rendererDesc.renderQueueRange = RenderQueueRange.opaque;
        RendererList rendererList = context.CreateRendererList(rendererDesc);

        //Clear
        commandBuffer.SetRenderTarget(renderTargetId);
        commandBuffer.ClearRenderTarget(
                            true,
                            true,
                            Color.black
                        );

        //Execute draws
        context.SetupCameraProperties(shadowCamera);
        commandBuffer.DrawRendererList(rendererList);

        //execute and clear
        context.ExecuteCommandBuffer(commandBuffer);
        commandBuffer.Clear();
        
        //Set the globals with our shiny new texture  projectionMatrix * viewMatrix;
        Matrix4x4 shadowMatrix = shadowCamera.projectionMatrix * shadowCamera.worldToCameraMatrix;
        Shader.SetGlobalMatrix("_ShadowmapMatrix"+index, shadowMatrix);
    }

    private void SetupGlobalLightingPropertiesForRendering()
    {
        sunDirection = Vector3.Normalize(new Vector3(-1, -2, -1));
        float sunBrightness = 0.5f;
        float ambientBrightness = 0.5f;
        Color sunColor = Color.white;
        Color ambientTint = Color.white;
        float ambientOcclusion = 0.25f;
        float fogStart = 70;
        float fogEnd = 500;
        Color fogColor = Color.white;
        float skySaturation = 0.3f;

        Cubemap cubeMap = null;

        Airship.AirshipRenderSettings renderSettings = GameObject.FindFirstObjectByType<Airship.AirshipRenderSettings>();

        //Fetch them from voxelworld if that exists
        if (renderSettings)
        {
            sunBrightness = renderSettings.sunBrightness;
            sunDirection = renderSettings._sunDirectionNormalized;
            sunColor = renderSettings.sunColor;
            ambientBrightness = renderSettings.globalAmbientBrightness;
            ambientTint = renderSettings.globalAmbientLight;
            ambientOcclusion = renderSettings.globalAmbientOcclusion;
            skySaturation = renderSettings.skySaturation;
            cubeMap = renderSettings.cubeMap;

            fogStart = renderSettings.fogStart;
            fogEnd = renderSettings.fogEnd;
            fogColor = renderSettings.fogColor;
        }

        Shader.SetGlobalFloat("globalSunBrightness", sunBrightness);
        Shader.SetGlobalFloat("globalAmbientBrightness", ambientBrightness);
        Shader.SetGlobalVector("globalSunDirection", sunDirection);  
        Shader.SetGlobalVector("globalSunColor", sunColor * sunBrightness);
        Shader.SetGlobalFloat("globalAmbientOcclusion", ambientOcclusion);
        //Set fogs
        Shader.SetGlobalFloat("globalFogStart", fogStart);
        Shader.SetGlobalFloat("globalFogEnd", fogEnd);
        Shader.SetGlobalColor("globalFogColor", fogColor);

        Shader.SetGlobalTexture("_CubeTex", cubeMap);

      
        if (renderSettings != null)
        {
            for (int j = 0; j < 9; j++)
            {
                shAmbientData[j] = new Vector4(renderSettings.cubeMapSHData[j].x, renderSettings.cubeMapSHData[j].y, renderSettings.cubeMapSHData[j].z, 0);
            }
        }

        if (renderSettings == null)
        {
            if (shAmbientData == null)
            {
                shAmbientData = new Vector4[9];
            }
            
            //Add a bunch of cool random lights for ambient
            UnityEngine.Rendering.SphericalHarmonicsL2 ambientSH = new UnityEngine.Rendering.SphericalHarmonicsL2();
            ambientSH.AddAmbientLight(new Color(0.9f, 0.9f,1) * 0.5f);
            
            //pack it in
            for (int j = 0; j < 9; j++)
            {
                shAmbientData[j] = new Vector4(ambientSH[0, j], ambientSH[1, j], ambientSH[2, j], 0);
            }
        }
        
        //Make the ambient light more interesting
        if (true)
        {
            float intensity = 1f;
            float downScale = 1f;
                
            UnityEngine.Rendering.SphericalHarmonicsL2 sourceSH = new UnityEngine.Rendering.SphericalHarmonicsL2();
            for (int j = 0; j < 9; j++)
            {
                sourceSH[0, j] = shAmbientData[j].x * downScale;
                sourceSH[1, j] = shAmbientData[j].y * downScale;
                sourceSH[2, j] = shAmbientData[j].z * downScale;
            }
                
            UnityEngine.Rendering.SphericalHarmonicsL2 ambientSH = new UnityEngine.Rendering.SphericalHarmonicsL2();
        
            float normalizedDown = 0.75f;
            float normalizedUp = 1.5f;
            float normalizedLeft = 1.0f;
            float normalizedRight = 1.0f;
            float normalizedForward = 1.25f;
            float normalizedBack = 1.25f;
        
            // Adding directional lights with normalized intensities
            Vector3[] directions =
            {
                Vector3.down,
                Vector3.up,
                Vector3.left,
                Vector3.right,
                Vector3.forward,
                Vector3.back
            };
            Color[] colors = new Color[6];
            sourceSH.Evaluate(directions, colors);
               
            ambientSH.AddDirectionalLight(Vector3.down, colors[0] * normalizedDown, intensity);
            ambientSH.AddDirectionalLight(Vector3.up, colors[1] * normalizedUp, intensity); //downward light
            ambientSH.AddDirectionalLight(Vector3.left, colors[2] * normalizedLeft, intensity);
            ambientSH.AddDirectionalLight(Vector3.right, colors[3] * normalizedRight, intensity);
            ambientSH.AddDirectionalLight(Vector3.forward, colors[4] * normalizedForward, intensity);
            ambientSH.AddDirectionalLight(Vector3.back, colors[5] * normalizedBack, intensity);

            for (int j = 0; j < 9; j++)
            {
                shAmbientData[j] = new Vector4(ambientSH[0, j], ambientSH[1, j], ambientSH[2, j], 0);
            }
        }
        

        //Adjust saturation
        for (int j = 0; j < 9; j++)
        {
            Color input = new Color(shAmbientData[j].x, shAmbientData[j].y, shAmbientData[j].z);
            Color.RGBToHSV(input, out float h, out float s, out float v);
            s *= skySaturation;
            Color res = Color.HSVToRGB(h, s, v);
            shAmbientData[j] = new Vector4(res.r, res.g, res.b, 0);

        }

        Shader.SetGlobalVectorArray("globalAmbientLight", shAmbientData);
        Shader.SetGlobalColor("globalAmbientTint", ambientTint * ambientBrightness);


        //Calculate and set our own _Time variable, called _RealTime
        //So that viewports update nicely
        
        if (capturedTime == 0)
        {
            capturedTime = Time.realtimeSinceStartup;
        }

        float t = Time.realtimeSinceStartup - capturedTime;
        Shader.SetGlobalVector("_RealTime", new Vector4(t / 20.0f, t, t * 2.0f, t * 3.0f));
    }

}