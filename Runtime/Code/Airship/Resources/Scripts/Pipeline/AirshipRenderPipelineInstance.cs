using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RendererUtils;
using Airship;
using UnityEngine.Profiling;

public class AirshipRenderPipelineInstance : RenderPipeline {
    public AirshipRenderPipelineInstance(float renderScaleSet, int MSAA, AirshipPostProcessingStack postStack, bool HDR) {
        hdr = HDR;
        msaaSamples = MSAA;
        renderScale = renderScaleSet;
        postProcessingStack = postStack;

        QualitySettings.antiAliasing = msaaSamples;
        this.msaaSamples = Mathf.Max(QualitySettings.antiAliasing, 1);

        GraphicsSettings.useScriptableRenderPipelineBatching = true;

    }

    public class RenderTargetGroup {
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

    //This is usually where this gets created for the whole pipeline
    private AirshipRendererManager airshipRendererManager = AirshipRendererManager.Instance;
    
    private float renderScale = 1;
    private int msaaSamples = 4;
    private const bool debugging = false;

    AirshipPostProcessingStack postProcessingStack;

    static int upscaledCameraColorTextureId = Shader.PropertyToID("_UpscaledCameraColorTexture");
    static int upscaledCameraColorTextureMrtId = Shader.PropertyToID("_UpscaledCameraColorTextureMrt");
    static int upscaledCameraDepthTextureId = Shader.PropertyToID("_UpscaledCameraDepthTexture");

    static int nativeScaledCameraColorTextureId = Shader.PropertyToID("_NativeScaledCameraColorTexture");
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

    static string[] matrixString = { "_ShadowmapMatrix0", "_ShadowmapMatrix1" };
    ShaderTagId airshipShadowPassTagId = new("AirshipShadowPass");

    [NonSerialized]
    Vector4[] shAmbientData = new Vector4[9];
    [NonSerialized]
    Vector3 sunDirection = Vector3.down;

    //Current scene objects
    [NonSerialized]
    Renderer[] renderers;

    class ShadowToggleRenderer {
        public ShadowToggleRenderer(Renderer renderer, uint filterValue) {
            this.renderer = renderer;
            this.previousValue = filterValue;
        }
        public uint previousValue;
        public Renderer renderer;
    }


    [NonSerialized]
    List<ShadowToggleRenderer> shadowToggledRenderers = new List<ShadowToggleRenderer>();


    [NonSerialized]
    private float capturedTime = 0;

    const int shadowWidth = 2048;
    const int shadowHeight = 2048;
    readonly float[] cascadeSize = new float[] { 0.3f, 1 };
    static int numBlurPasses = 6;

    [NonSerialized]
    Camera[] shadowMapCamera = new Camera[2];

    [NonSerialized]
    GameObject[] shadowMapCameraObject = new GameObject[2];

    [NonSerialized]
    bool hdr = true;

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


    CommandBuffer reusedCmdBuffer = new() {
        name = "RenderTarget"
    };

    private void SetupGlobalTextures() {
        Profiler.BeginSample("Setup Global Textures");
        if (ditherTexture == null) {
            this.ditherTexture = Resources.Load<Texture2D>(ditherTexturePath); // AssetBridge.LoadAssetInternal<Texture2D>(this.ditherTexturePath, true);
        }

        Shader.SetGlobalTexture("_DitherTexture", ditherTexture);
        Profiler.EndSample();
    }
    private void DoShadowmapRendering(Camera camera, ScriptableRenderContext context, CommandBuffer cameraCmdBuffer, AirshipRenderSettings renderSettings) {
        PreRenderShadowmaps();
        RenderShadowmap(camera, context, cameraCmdBuffer, 0, renderSettings);
        RenderShadowmap(camera, context, cameraCmdBuffer, 1, renderSettings);
        PostRenderShadowmaps();
    }

    protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras) {

        GatherRenderers();
                
        SetupGlobalTextures();

        //Cleanup
        CleanupShadowmapCameras(cameras);

        Profiler.BeginSample("Sort Render Groups");
        //create a array of render targets used by cameras, including a blank one for rendering directly to the backbuffer
        List<RenderTargetGroup> renderTargetGroup = new();

        foreach (var camera in cameras) {
#if UNITY_EDITOR            
            if (camera == null) {
                continue;
            }

            if (camera.gameObject.name == "Shadowmap Camera") {
                continue;
            }
#endif            

            //See if the camera is in the shadowMapCamera array, and if so skip
            if (shadowMapCamera != null) {
                bool foundCamera = false;
                for (int i = 0; i < shadowMapCamera.Length; i++) {
                    if (camera == shadowMapCamera[i]) {
                        foundCamera = true;
                        break;
                    }
                }
                if (foundCamera) {
                    continue;
                }
            }

            //add to the renderTargetGroup where the camera.renderTarget matches, or create a new renderTargetGroup
            bool found = false;
            foreach (var renderTarget in renderTargetGroup) {
                if (renderTarget.renderTexture == camera.targetTexture && renderTarget.cameraType == camera.cameraType) {
                    renderTarget.cameras.Add(camera);
                    found = true;
                    break;
                }
            }
            if (!found) {
                renderTargetGroup.Add(new RenderTargetGroup() { renderTexture = camera.targetTexture, cameras = new() { camera }, cameraType = camera.cameraType });
            }
        }

        //sort the cameras
        Comparison<Camera> cameraComparison = (camera1, camera2) => { return (int)camera1.depth - (int)camera2.depth; };
        foreach (RenderTargetGroup renderTarget in renderTargetGroup) {
            renderTarget.cameras.Sort(cameraComparison);
        }

        //Setup rendering for previews
        foreach (RenderTargetGroup renderTarget in renderTargetGroup) {
            if (renderTarget.cameraType == CameraType.Preview) {
                renderTarget.drawAirshipOnly = false;
                renderTarget.forceClearBackground = true;
                renderTarget.colorGradeOnly = true;
            }
        }

#if UNITY_IOS || UNITY_ANDROID
	//Disable supersampling on mobile
        foreach (RenderTargetGroup renderTarget in renderTargetGroup) {
		renderTarget.allowScaledRendering = false;
	}
#endif
        Profiler.EndSample();

        //render each renderTargetGroup that has textures
        foreach (RenderTargetGroup renderTarget in renderTargetGroup) {
            if (renderTarget.renderTexture != null && renderTarget.cameraType != CameraType.SceneView && renderTarget.cameraType != CameraType.Game) {
                RenderGroup(renderContext, renderTarget);
            }
        }

        //Do the game one now
        foreach (RenderTargetGroup renderTarget in renderTargetGroup) {
            if (renderTarget.renderTexture == null || renderTarget.cameraType == CameraType.SceneView || renderTarget.cameraType == CameraType.Game) {
                RenderGroup(renderContext, renderTarget);
            }
        }

        AirshipRenderPipelineStatistics.ExtractStatsFromScene();
    }

    void DrawGizmos(ScriptableRenderContext context, Camera camera, CommandBuffer cameraCmdBuffer) {
#if UNITY_EDITOR
        Profiler.BeginSample("DrawGizmos");
        if (Handles.ShouldRenderGizmos()) {
            cameraCmdBuffer.ClearRenderTarget(RTClearFlags.Depth, camera.backgroundColor, 1, 0);

            //Execute any clears
            context.ExecuteCommandBuffer(cameraCmdBuffer);
            cameraCmdBuffer.Clear();

            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }
        Profiler.EndSample();
#endif

    }

    void RenderGroup(ScriptableRenderContext context, RenderTargetGroup group) {
        RenderGroupFullStack(context, group);
    }

    void RenderGroupFullStack(ScriptableRenderContext context, RenderTargetGroup group) {

        Profiler.BeginSample("RenderGroupFullStack");
        
        AirshipRenderPipelineStatistics.numPasses += 1;
        Camera rootCamera = group.cameras[0];

#if UNITY_EDITOR
        if (group.cameraType == CameraType.SceneView) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(rootCamera);
        }
#endif

        Airship.AirshipRenderSettings renderSettings = GetRenderSettingsForCamera(rootCamera);

        Profiler.BeginSample("Setup Passes");
        //Draw opaques
        ShaderTagId[] shaderTagId;

        if (group.drawAirshipOnly == true) {
            shaderTagId = new ShaderTagId[]
            {
                new("UniversalPipeline"),
                new("AirshipForwardPass"),

            };
        }
        else {
            shaderTagId = new ShaderTagId[]
            {
                    new ShaderTagId("AirshipForwardPass"),
                    new ShaderTagId("ForwardBase"),
                    new ShaderTagId("Always"),
                    new ShaderTagId("ForwardAdd"),
                    new ShaderTagId("PrepassFinal"),
                    new ShaderTagId("Vertex"),
                    new ShaderTagId("VertexLMRGBM"),
                    new ShaderTagId("VertexLM"),

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

        Profiler.EndSample();

        //Start rendering
        CommandBuffer cameraCmdBuffer = reusedCmdBuffer;
        cameraCmdBuffer.Clear();

        //Do per camera lighting Settings
        SetupGlobalLightingPropertiesForRendering(renderSettings);

        //Generate our shadowmaps
        DoShadowmapRendering(rootCamera, context, cameraCmdBuffer, renderSettings);

        Profiler.BeginSample("Allocate Textures");
        //Grab our scenes temporary textures
        AllocateUpscaledRenderTextures(upscaledRenderWidth, upscaledRenderHeight, scaledRendering, cameraCmdBuffer);
        AllocateTemporaryRenderTextures(nativeScreenWidth, nativeScreenHeight, cameraCmdBuffer);
        AllocateBlurTextures(blurBufferWidth, blurBufferHeight, cameraCmdBuffer);

        context.ExecuteCommandBuffer(cameraCmdBuffer);
        cameraCmdBuffer.Clear();

        Profiler.EndSample();


        Profiler.BeginSample("Emit Camera Geom");
        //Start layering the cameras into this buffer
        //Eg: camera0 - clear all, draw world, draw sky
        //    camera1 - clear depth, draw fps hands
        foreach (Camera camera in group.cameras) {
            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = context.Cull(ref cullingParameters);
            context.SetupCameraProperties(camera);
            CameraClearFlags clearFlags = camera.clearFlags;

            //if we're going to be rendering the skybox, or the first target in the chain, clear everything
            if (group.forceClearBackground == true || clearFlags == CameraClearFlags.Skybox || clearFlags == CameraClearFlags.Color || clearFlags == CameraClearFlags.SolidColor) {
                // Clear the MRT 2nd buffer separately
                if (scaledRendering) {
                    cameraCmdBuffer.SetRenderTarget(upscaledCameraColorTextureMrtId);
                }
                else {
                    cameraCmdBuffer.SetRenderTarget(nativeScaledCameraColorTextureMrtId);
                }
                cameraCmdBuffer.ClearRenderTarget(RTClearFlags.Color, Color.black);

                //clear the main buffer
                if (scaledRendering) {
                    cameraCmdBuffer.SetRenderTarget(upscaledCameraColorTextureId);
                }
                else {
                    cameraCmdBuffer.SetRenderTarget(nativeScaledCameraColorTextureId);
                }
                cameraCmdBuffer.ClearRenderTarget(RTClearFlags.All, camera.backgroundColor, 1, 0);
                context.ExecuteCommandBuffer(cameraCmdBuffer);
                cameraCmdBuffer.Clear();

                //Clear depth buffer
                //Todo: revise this - can we get away with not clearing this depth buffer on its own?
                if (scaledRendering) {
                    cameraCmdBuffer.SetRenderTarget(upscaledCameraDepthTextureId);
                }
                else {
                    cameraCmdBuffer.SetRenderTarget(nativeScaledCameraDepthTextureId);
                }
                //just depth
                cameraCmdBuffer.ClearRenderTarget(RTClearFlags.Depth, Color.white, 1, 0);
                context.ExecuteCommandBuffer(cameraCmdBuffer);
                cameraCmdBuffer.Clear();
            }
            else {
                //Do what was requested on the camera itself
                if (clearFlags == CameraClearFlags.Depth) {
                    //clear the main buffer
                    if (scaledRendering) {
                        cameraCmdBuffer.SetRenderTarget(upscaledCameraDepthTextureId);
                    }
                    else {
                        cameraCmdBuffer.SetRenderTarget(nativeScaledCameraDepthTextureId);
                    }
                    //just depth
                    cameraCmdBuffer.ClearRenderTarget(RTClearFlags.Depth, camera.backgroundColor, 1, 0);
                    context.ExecuteCommandBuffer(cameraCmdBuffer);
                    cameraCmdBuffer.Clear();
                }

            }

            //Set the renderTarget
            //because airship uses MRT, we have to capture to a few textures first, and then composite to the final texture later
            //!Note! Every time we call context.SetupCameraProperties, we have to rebind our render targets
            //       which is why we're doing this INSIDE the multi camera loop
            if (scaledRendering) {
                RenderTargetIdentifier[] upscaledCameraColorTextureArray = new RenderTargetIdentifier[2];
                upscaledCameraColorTextureArray[0] = upscaledCameraColorTextureId;
                upscaledCameraColorTextureArray[1] = upscaledCameraColorTextureMrtId;
                cameraCmdBuffer.SetRenderTarget(upscaledCameraColorTextureArray, upscaledCameraDepthTextureId);
            }
            else {
                RenderTargetIdentifier[] nativeCameraColorTextureArray = new RenderTargetIdentifier[2];
                nativeCameraColorTextureArray[0] = nativeScaledCameraColorTextureId;
                nativeCameraColorTextureArray[1] = nativeScaledCameraColorTextureMrtId;
                cameraCmdBuffer.SetRenderTarget(nativeCameraColorTextureArray, nativeScaledCameraDepthTextureId);
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
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null) {
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
            DrawGizmos(context, camera, cameraCmdBuffer);

            //Execute
            context.ExecuteCommandBuffer(cameraCmdBuffer);
            cameraCmdBuffer.Clear();
        }
        Profiler.EndSample();

        Profiler.BeginSample("Texure Resolve and Post");
        //Take what we've rendered (at high res or other) and put it into the nativeTextures
        //So after this point, nativeTexture contains our scene regardless if its scaledRendering or not
        BuildResolvedTextures(context, cameraCmdBuffer, scaledRendering);

        //Build downscaled textures from our "resolved texture"
        BuildHalfAndQuarterSizedTextures(context, cameraCmdBuffer, nativeScreenWidth, nativeScreenHeight);

        //Build our Frosted Glass Texture
        BuildFrostedGlassBlur(context, cameraCmdBuffer, blurBufferWidth, blurBufferHeight, blurColorTextureId, quarterSizeTexId);

        Profiler.EndSample();
        
        Profiler.BeginSample("PostProcess");
        //Let the post stack final composite run now  
        postProcessingStack.Render(context, cameraCmdBuffer, nativeScaledCameraColorTextureId, nativeScreenWidth, nativeScreenHeight, halfSizeTexMrtId, group.renderTexture, group.colorGradeOnly);
        Profiler.EndSample();

        Profiler.BeginSample("Free Textures");
        //Free the shadow texture
        cameraCmdBuffer.ReleaseTemporaryRT(globalShadowTexture0Id);
        cameraCmdBuffer.ReleaseTemporaryRT(globalShadowTexture1Id);

        //Free up the scaled rendering RTs
        if (scaledRendering) {
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

        Profiler.EndSample();

        //Submit and quit
        Profiler.BeginSample("Context Submit");
        context.Submit();
        Profiler.EndSample();
        
        Profiler.EndSample();
    }

    void AllocateUpscaledRenderTextures(int upscaledRenderWidth, int upscaledRenderHeight, bool scaledRendering, CommandBuffer cameraCmdBuffer) {
        if (scaledRendering == false) {
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

    void AllocateTemporaryRenderTextures(int nativeScreenWidth, int nativeScreenHeight, CommandBuffer cameraCmdBuffer) {
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
    }

    void AllocateBlurTextures(int blurBufferWidth, int blurBufferHeight, CommandBuffer cameraCmdBuffer) {
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
    void DrawDefaultPipeline(CullingResults cullingResults, ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer) {

        Profiler.BeginSample("DefaultPipeline");
        if (errorMaterial == null) {
            Shader errorShader = Shader.Find("Hidden/AirshipErrorShader");
            errorMaterial = new Material(errorShader) {
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
        rendererDesc.renderQueueRange = RenderQueueRange.all;

        RendererList rendererList = context.CreateRendererList(rendererDesc);
        commandBuffer.DrawRendererList(rendererList);
        Profiler.EndSample();
    }

    void DrawCanvases(CullingResults cullingResults, ScriptableRenderContext context, Camera camera, CommandBuffer commandBuffer) {
        Profiler.BeginSample("Canvases");
        int layerMask = LayerMask.NameToLayer("UI");

        ShaderTagId[] passNames = {
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        RendererListDesc rendererDesc = new RendererListDesc(passNames, cullingResults, camera);
        rendererDesc.layerMask = 1 << layerMask;
        rendererDesc.renderQueueRange = RenderQueueRange.all;

        RendererList rendererList = context.CreateRendererList(rendererDesc);
        commandBuffer.DrawRendererList(rendererList);
        Profiler.EndSample();
    }


    public void BuildResolvedTextures(ScriptableRenderContext context, CommandBuffer cmd, bool scaledRendering) {
        //Either the upscaled renderTarget textures are bound right now (scaledRendering)
        //or the native renderTarget textures are bound right now (not scaledRendering)

        if (scaledRendering == false) {
            //It's native, so we dont have anything to resolve
            return;
        }

        if (downscaleMaterial == null) {
            downscaleMaterial = Resources.Load("DownScaleMat") as Material;
            if (downscaleMaterial == null) {
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
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, downscaleMaterial, 0, 0);

    }

    public void BuildHalfAndQuarterSizedTextures(ScriptableRenderContext context, CommandBuffer cmd, int nativeSceneWidth, int nativeSceneHeight) {

        if (downscaleMaterial == null) {
            downscaleMaterial = Resources.Load("DownScaleMat") as Material;
            if (downscaleMaterial == null) {
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
        cmd.SetRenderTarget(quarterSizeRt, quarterSizeDepthTextureId);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetGlobalTexture(mainTexId, halfSizeTexId); //texture to render with
        cmd.SetGlobalTexture(mainTexMrtId, halfSizeTexMrtId); //texture to render with
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, downscaleMaterial);
    }

    public void BuildFrostedGlassBlur(ScriptableRenderContext context, CommandBuffer cmd, int bufferWidth, int bufferHeight, int outputTextureId, int sourceTextureId) {
        if (horizontalBlurMaterial == null) {
            horizontalBlurMaterial = Resources.Load("HorizontalBlurMat") as Material;
            if (horizontalBlurMaterial == null) {
                Debug.LogError("Missing Material: HorizontalBlurMat");
            }

        }
        if (verticalBlurMaterial == null) {
            verticalBlurMaterial = Resources.Load("VerticalBlurMat") as Material;
            if (verticalBlurMaterial == null) {
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
        for (int j = 0; j < numBlurPasses; j++) {
            //On the final pass, write directly to the output texture
            int writeRT = verticalTextureId;
            if (j == numBlurPasses - 1) {
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

    private Bounds CalculateFrustumBounds(Camera camera, Matrix4x4 worldToLightspace, float maxDistance) {
        var worldFrustumCorners = new Vector3[8];
        CalculateFrustumCorners(camera, camera.nearClipPlane, worldFrustumCorners);
        CalculateFrustumCorners(camera, maxDistance, worldFrustumCorners, 4);

        //Todo: trim this down 
        float maxZ = 256;
        Vector3 cameraPos = camera.transform.position;
        Vector3 cameraPosLightspace = worldToLightspace.MultiplyPoint(cameraPos);

        //Transform these points into light space to encapsulate them
        var lightViewSpaceCorners = new Vector3[8];
        for (int i = 0; i < 8; i++) {
            lightViewSpaceCorners[i] = worldToLightspace.MultiplyPoint(worldFrustumCorners[i]);
        }
        
        //Encapsulate the frustrum as seen from the light
        var frustumBounds = new Bounds(lightViewSpaceCorners[0], Vector3.zero);
        for (int i = 1; i < lightViewSpaceCorners.Length; i++) {
            frustumBounds.Encapsulate(lightViewSpaceCorners[i]);
        }

        //Push the min up to capture more shadowcasters
        frustumBounds.min = new Vector3(frustumBounds.min.x, frustumBounds.min.y, cameraPosLightspace.z - maxZ);
        frustumBounds.max = new Vector3(frustumBounds.max.x, frustumBounds.max.y, cameraPosLightspace.z + maxZ);

        //Take the center
        Vector3 center = frustumBounds.center;
        //create a new bounds with fixed x and y size
        frustumBounds = new Bounds(center, new Vector3(maxDistance * 2, maxDistance * 2, maxZ * 2));


        //calculate worldTexelsize
        Vector3 worldTexelSize = new Vector3(frustumBounds.size.x / shadowWidth, frustumBounds.size.y / shadowHeight, 1);

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

    private void CalculateFrustumCorners(Camera camera, float distance, Vector3[] corners, int startIndex = 0) {
        // Top left
        corners[startIndex] = camera.ViewportToWorldPoint(new Vector3(0, 1, distance));
        // Top right
        corners[startIndex + 1] = camera.ViewportToWorldPoint(new Vector3(1, 1, distance));
        // Bottom left
        corners[startIndex + 2] = camera.ViewportToWorldPoint(new Vector3(0, 0, distance));
        // Bottom right
        corners[startIndex + 3] = camera.ViewportToWorldPoint(new Vector3(1, 0, distance));
    }

    Camera[] CleanupShadowmapCameras(Camera[] sceneCameras) {

        Profiler.BeginSample("Cleanup Shadowmap Cameras");
        //Sanitize the scene, we only want one of these.
        foreach (var camera in sceneCameras) {
            //See if it exists in the shadowmapCamera array
            bool found = false;
            foreach (var validCamera in shadowMapCamera) {
                if (validCamera == camera) {
                    found = true;
                    break;
                }
            }
            if (found) {
                continue;
            }

            if (camera.name == "Shadowmap Camera" || camera.name == "Shadowmap Camera 0" || camera.name == "Shadowmap Camera 1") {
                if (Application.isPlaying == true) {
                    GameObject.Destroy(camera.gameObject);
                }
                else {
                    GameObject.DestroyImmediate(camera.gameObject);
                }
            }
        }
        Profiler.EndSample();
        return sceneCameras;
    }

    void GatherRenderers() {
        
        Profiler.BeginSample("GatherRenderers");
        
        airshipRendererManager.PerFrameUpdate();
        airshipRendererManager.PreRender();

        renderers = airshipRendererManager.GetRenderers();
        
        Profiler.EndSample();
        
    }

    void PreRenderShadowmaps() {
        //We want to be able to turn shadow casting off on certain objects
        //Because we cant filter for this directly, we need to move stuff to a different renderFilterLayer
        Profiler.BeginSample("PreRenderShadowmaps");
        
        foreach (Renderer renderer in renderers) {
            if (renderer && renderer.shadowCastingMode == ShadowCastingMode.Off) {
                // Debug.Log(renderer.gameObject.name);
                shadowToggledRenderers.Add(new ShadowToggleRenderer(renderer, renderer.renderingLayerMask));
                renderer.renderingLayerMask = 1 << 15;
            }
        }

        Profiler.EndSample();
    }

    void PostRenderShadowmaps() {
        //Reset the renderFilterLayer
        foreach (ShadowToggleRenderer renderer in shadowToggledRenderers) {
            renderer.renderer.renderingLayerMask = renderer.previousValue;
        }
        shadowToggledRenderers.Clear();
    }

    void RenderShadowmap(Camera mainCamera, ScriptableRenderContext context, CommandBuffer commandBuffer, int index, AirshipRenderSettings renderSettings) {

        Profiler.BeginSample("RenderShadowmap");
        // commandBuffer.BeginSample("Shadowmaps");

        int renderTargetId = 0;
        if (index == 0) {
            renderTargetId = globalShadowTexture0Id;
        }
        if (index == 1) {
            renderTargetId = globalShadowTexture1Id;
        }


        //Generate a shadowmap rendertarget
        if (true) {
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

        if (depthMaterial == null) {
            Shader depthShader = Shader.Find("Airship/DepthToTexture");
            depthMaterial = new Material(depthShader) {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        //Generate a camera that will be used to render the shadowmap

        //Check to see if "Shadowmap camera" already exists, and use that
        if (shadowMapCamera[index] == null) {

            // Get all root objects in the scene
            shadowMapCameraObject[index] = GameObject.Find("Shadowmap Camera " + index);
            if (shadowMapCameraObject[index]) {
                shadowMapCamera[index] = shadowMapCameraObject[index].GetComponent<Camera>();
            }

            if (shadowMapCamera[index] == null) {
                //Debug.Log("Didn't find shadowmap camera " + index);
            }
        }

        //create it?
        if (shadowMapCamera[index] == null) {
            //Debug.Log("Creating shadowmap Camera");
            shadowMapCameraObject[index] = new GameObject("Shadowmap Camera " + index);
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

        float shadowDistance = 100;
        if (renderSettings) {
            shadowDistance = renderSettings.shadowRange;
        }

        // Position the shadowmap camera to cover the main camera's frustum
        float maxDistance = cascadeSize[index] * shadowDistance; // Set this to the number of units you want to capture

        shadowCamera.transform.position = Vector3.zero;

        if (Math.Abs(Vector3.Dot(Vector3.up, sunDirection)) > 0.95f) {
            shadowCamera.transform.rotation = Quaternion.LookRotation(sunDirection, Vector3.right);
        }
        else {
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
             
        shadowCamera.TryGetCullingParameters(out var cullingParameters);
        cullingParameters.cullingOptions = CullingOptions.ShadowCasters;
        shadowCamera.overrideSceneCullingMask = 0;

        CullingResults cullingResults = context.Cull(ref cullingParameters);

        RendererListDesc rendererDesc = new RendererListDesc(airshipShadowPassTagId, cullingResults, shadowCamera);

        //Mask all except bit 15
        rendererDesc.renderingLayerMask = 0xFFFF7FFF;
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

        Profiler.BeginSample("Context Submit");
        context.Submit();
        Profiler.EndSample();

        //Set the globals with our shiny new texture  projectionMatrix * viewMatrix;
        Matrix4x4 shadowMatrix = shadowCamera.projectionMatrix * shadowCamera.worldToCameraMatrix;
        Shader.SetGlobalMatrix(matrixString[index], shadowMatrix);

        Profiler.EndSample();
    }

    Airship.AirshipRenderSettings GetRenderSettingsForCamera(Camera camera) {
        Profiler.BeginSample("GetRenderSettingsForCamera");
        //See if the given camera has an AirshipCameraExtension on it
        AirshipCameraExtension cameraExtension = camera.GetComponent<AirshipCameraExtension>();
        if (cameraExtension && cameraExtension.enabled && cameraExtension.airshipRenderSettings != null) {
            Profiler.EndSample();
            return cameraExtension.airshipRenderSettings;
        }

        //Else search the active scene for it
        Airship.AirshipRenderSettings renderSettings = AirshipRenderSettings.GetFirstOne();
        Profiler.EndSample();
        return renderSettings;
    }


    private void SetupGlobalLightingPropertiesForRendering(AirshipRenderSettings renderSettings) {

        Profiler.BeginSample("SetupGlobalLightingPropertiesForRendering");
        sunDirection = Vector3.Normalize(new Vector3(0.5f, -2, -1.3f));
        float sunBrightness = 1;
        float sunShadow = 0.85f;
        float ambientBrightness = 0.25f;
        Color sunColor = Color.white;
        Color ambientTint = Color.white;
        float ambientOcclusion = 0.25f;
        float fogStart = 75;
        float fogEnd = 500;
        bool fogEnabled = true;

        Color fogColor = Color.white;
        float skySaturation = 0.3f;
        //Per cascade
        float shadowRange = 100;
        Cubemap cubeMap = null;


        //Fetch them from voxelworld if that exists


        if (renderSettings) {
            sunBrightness = renderSettings.sunBrightness;
            sunDirection = renderSettings._sunDirectionNormalized;
            sunColor = renderSettings.sunColor;
            sunShadow = renderSettings.sunShadow;
            ambientBrightness = renderSettings.globalAmbientBrightness;
            ambientTint = renderSettings.globalAmbientLight;
            ambientOcclusion = renderSettings.globalAmbientOcclusion;
            skySaturation = renderSettings.skySaturation;
            cubeMap = renderSettings.cubeMap;

            fogStart = renderSettings.fogStart;
            fogEnd = renderSettings.fogEnd;
            fogColor = renderSettings.fogColor;

            shadowRange = renderSettings.shadowRange;

            fogEnabled = renderSettings.fogEnabled;
        }

        Shader.SetGlobalFloat("globalSunBrightness", sunBrightness);
        Shader.SetGlobalFloat("globalAmbientBrightness", ambientBrightness);
        Shader.SetGlobalFloat("globalSunShadow", sunShadow);
        Shader.SetGlobalVector("globalSunDirection", sunDirection);
        Shader.SetGlobalVector("globalSunColor", sunColor * sunBrightness);
        Shader.SetGlobalFloat("globalAmbientOcclusion", ambientOcclusion);
        //Set fogs

        if (fogEnabled) {
            Shader.SetGlobalFloat("globalFogStart", fogStart);
            Shader.SetGlobalFloat("globalFogEnd", fogEnd);
            Shader.SetGlobalColor("globalFogColor", fogColor);
        }
        else {
            Shader.SetGlobalFloat("globalFogStart", 100000);
            Shader.SetGlobalFloat("globalFogEnd", 100001);
            Shader.SetGlobalColor("globalFogColor", Color.white);
        }
        Shader.SetGlobalTexture("_CubeTex", cubeMap);

        //Calculate a new shadow bias based upon render distance
        //Vector4 shadowBias = new Vector4(0.03f, 0.06f, 0, 0);
        float unitsPerTexel = shadowRange / 2048.0f;
        float bias = unitsPerTexel * 4.0f;

        Shader.SetGlobalVector("_ShadowBias", new Vector4(bias * cascadeSize[0], bias * cascadeSize[1], 0, 0));


        if (renderSettings != null) {
            for (int j = 0; j < 9; j++) {
                shAmbientData[j] = new Vector4(renderSettings.cubeMapSHData[j].x, renderSettings.cubeMapSHData[j].y, renderSettings.cubeMapSHData[j].z, 0);
            }
        }

        if (renderSettings == null) {
            if (shAmbientData == null) {
                shAmbientData = new Vector4[9];
            }

            //Add a bunch of cool random lights for ambient
            UnityEngine.Rendering.SphericalHarmonicsL2 ambientSH = new UnityEngine.Rendering.SphericalHarmonicsL2();
            ambientSH.AddAmbientLight(new Color(0.9f, 0.9f, 1) * 0.5f);

            //pack it in
            for (int j = 0; j < 9; j++) {
                shAmbientData[j] = new Vector4(ambientSH[0, j], ambientSH[1, j], ambientSH[2, j], 0);
            }
        }

        //Make the ambient light more interesting
        if (true) {
            float intensity = 1f;
            float downScale = 1f;

            SphericalHarmonicsL2 sourceSH = new();
            for (int j = 0; j < 9; j++) {
                sourceSH[0, j] = shAmbientData[j].x * downScale;
                sourceSH[1, j] = shAmbientData[j].y * downScale;
                sourceSH[2, j] = shAmbientData[j].z * downScale;
            }

            SphericalHarmonicsL2 ambientSH = new();

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

            for (int j = 0; j < 9; j++) {
                shAmbientData[j] = new Vector4(ambientSH[0, j], ambientSH[1, j], ambientSH[2, j], 0);
            }
        }


        //Adjust saturation
        for (int j = 0; j < 9; j++) {
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

        if (capturedTime == 0) {
            capturedTime = Time.realtimeSinceStartup;
        }

        float t = Time.realtimeSinceStartup - capturedTime;
        Shader.SetGlobalVector("_RealTime", new Vector4(t / 20.0f, t, t * 2.0f, t * 3.0f));

        Profiler.EndSample();
    }

}