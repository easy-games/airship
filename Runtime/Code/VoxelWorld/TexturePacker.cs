using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

public class TexturePacker : IDisposable
{

    static int anisoLevel = 1;

    // Dictionary to store the UVs of each packed texture
    private Dictionary<Texture2D, Rect> uvs;

    public RenderTexture diffuse;
    public RenderTexture normals;

    public void Dispose()
    {
        if (diffuse != null)
        {
            diffuse.Release();
            diffuse = null;
        }
        if (normals != null)
        {
            normals.Release();
            normals = null;
        }
    }

    public class TextureSet
    {
        public Texture2D diffuse;
        public Texture2D normals;
        public Texture2D smoothTexture;
        public Texture2D metalTexture;
        public Texture2D emissiveTexture;

        public float smoothness;
        public float metallic;
        public float normalScale;
        public float emissive;
        public float brightness;

        public TextureSet(Texture2D diffuse, Texture2D normals, Texture2D smoothTex, Texture2D metalTex, Texture2D emissiveTex, float smoothness, float metallic, float normalScale, float emissive, float brightness)
        {
            this.diffuse = diffuse;
            this.normals = normals;
            this.smoothTexture = smoothTex;
            this.metalTexture = metalTex;
            this.smoothness = smoothness;
            this.metallic = metallic;
            this.normalScale = normalScale;
            this.emissiveTexture = emissiveTex;
            this.emissive = emissive;
            this.brightness = brightness;
        }

    }
 
    // Function for getting the UVs of a packed texture
    public Rect GetUVs(Texture2D sourceTexture)
    {
        if (uvs.ContainsKey(sourceTexture))
        {
            return uvs[sourceTexture];
        }
        else
        {
            return new Rect();
        }
    }

    

    public void PackTextures(Dictionary<int, TextureSet> textures, int desiredPadding, int width, int height, int numMips, int normalizedSize)
    {
        Profiler.BeginSample("PackTextures");
        //grab the time
        float startTime = Time.realtimeSinceStartup;


        // Initialize the dictionary for storing the UVs
        uvs = new();
        
        int pad = (int)desiredPadding;
        int doublePadding = pad * 2;
 
        //Create a renderTargetDesc
        RenderTextureDescriptor textureDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32);
        textureDesc.useMipMap = true;
        textureDesc.mipCount = numMips;
        textureDesc.sRGB = false;
        textureDesc.autoGenerateMips = false;
        textureDesc.depthBufferBits = 0;

        var activeRt = RenderTexture.active;
        diffuse = new RenderTexture(textureDesc);
        diffuse.anisoLevel = anisoLevel;
        diffuse.filterMode = FilterMode.Trilinear;

        normals = new RenderTexture(textureDesc);
        normals.anisoLevel = anisoLevel;
        normals.filterMode = FilterMode.Trilinear;

        RenderBuffer[] renderTargets = new RenderBuffer[2];
        renderTargets[0] = diffuse.colorBuffer;
        renderTargets[1] = normals.colorBuffer;

        RenderTargetSetup renderTargetSetup = new RenderTargetSetup(renderTargets, diffuse.depthBuffer);

        //Grab the flat normals texture
        Texture2D flatNormals = Resources.Load<Texture2D>("BaseTextures/flatnormal");

        //Create a black texture
        Texture2D blackTexture = new Texture2D(1, 1,TextureFormat.RGBA32, false,true);
        blackTexture.SetPixel(0, 0, Color.black);
        blackTexture.Apply();

        //create a white texture
        Texture2D whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

        //create a diffuse Material using the Airship/AtlasDiffuse shader
        Material diffuseMat = null;
        if (RunCore.IsClient() || !Application.isPlaying) {
            diffuseMat = new Material(Shader.Find("Airship/AtlasDiffuse"));
        }

        // Pack the textures into the resulting texture
        int fixedSizeX = diffuse.width;
        int fixedSizeY = diffuse.height;

        if (normalizedSize > 0)
        {
            if (fixedSizeX > fixedSizeY)
            {
                float ratio = (float)fixedSizeY / (float)fixedSizeX;
                fixedSizeX = (int)((float)normalizedSize * ratio);
                fixedSizeY = normalizedSize;
            }
            else
            {
                float ratio = (float)fixedSizeX / (float)fixedSizeY;
                fixedSizeX = normalizedSize;
                fixedSizeY = (int)((float)normalizedSize * ratio);
            }
        }

        for (int i = 0; i < numMips; i++)
        {
            renderTargetSetup.mipLevel = i;
            Graphics.SetRenderTarget(renderTargetSetup);
            GL.Clear(true, true, Color.black);
        
            Rect rect = new Rect(pad, pad, fixedSizeX, fixedSizeY);
            int fattestHeight = 0;
            foreach (var textureItem in textures)
            { 
                //string name = textureItem.Key;
                Texture2D diffuseMap = textureItem.Value.diffuse;
                Texture2D normalMap = textureItem.Value.normals;
                Texture2D smoothMap = textureItem.Value.smoothTexture;
                Texture2D metalMap = textureItem.Value.metalTexture;
                Texture2D emissiveMap = textureItem.Value.emissiveTexture;

                if (normalMap == null)
                {
                    normalMap = flatNormals;
                }

                if (diffuseMat != null) {
                    diffuseMat.SetTexture("_NormalMap", normalMap);
                    if (smoothMap == null)
                    {
                        diffuseMat.SetTexture("_SmoothMap", whiteTexture);
                        diffuseMat.SetFloat("_Smoothness", textureItem.Value.smoothness);
                    }
                    else
                    {
                        diffuseMat.SetTexture("_SmoothMap", smoothMap);
                        diffuseMat.SetFloat("_Smoothness", -1);
                    }
            
                    if (metalMap == null)
                    {
                        diffuseMat.SetTexture("_MetalMap", blackTexture);
                        diffuseMat.SetFloat("_Metallic", textureItem.Value.metallic);
                    }
                    else
                    {
                        diffuseMat.SetTexture("_MetalMap", metalMap);
                        diffuseMat.SetFloat("_Metallic", -1);
                    }
            
                    if (emissiveMap == null)
                    {
                        diffuseMat.SetTexture("_EmissiveMap", blackTexture);
                        diffuseMat.SetFloat("_Emissive", textureItem.Value.emissive);
                    }
                    else
                    {
                        diffuseMat.SetTexture("_EmissiveMap", emissiveMap);
                        diffuseMat.SetFloat("_Emissive", -1);
                    }
                    diffuseMat.SetFloat("_Brightness", textureItem.Value.brightness);   
                }
            
                rect.width = fixedSizeX;
                //does it fit on the X axis?
                if (rect.x + rect.width + pad > width)
                {
                    rect.x = pad;
                    rect.y += rect.height + doublePadding;
                    fattestHeight = 0;
                }

                if (fixedSizeY > fattestHeight)
                {
                    //Fatten the height of the row out if we have missized textures
                    fattestHeight = fixedSizeY;
                }
                rect.height = fixedSizeY;

                if (rect.y + fattestHeight + pad > height)
                {
                    Debug.LogError("Atlas is full!");
                    break;
                }
                // Calculate the UVs for the packed texture
                float uvX = (float)rect.x / width;
                float uvY = (float)rect.y / height;
                float uvWidth = (float)rect.width / width;
                float uvHeight = (float)rect.height / height;
                Rect uv = new Rect(uvX, uvY, uvWidth, uvHeight);
            
                Vector2 scale = new Vector2((float)diffuse.width / (float)fixedSizeX, (float)diffuse.height / (float)fixedSizeY);
                Vector2 offset = new Vector2(uvX, uvY);

                if (diffuseMat != null) {
                    CustomBlit(diffuse, diffuseMap, diffuseMat, (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height, 0, 0, -1, -1);
                    DoPadding(diffuse, diffuseMap, rect, pad, diffuseMat);   
                }
 
                // Add the UVs to the dictionary
                if (i == 0)
                {
                    uvs[diffuseMap] = uv;
                }
                //colors[name] = averageColor;

                //Step it along
                rect.x += rect.width + doublePadding;
            }
        }
        //print the total time elapsed
        // Debug.Log("Atlas generation took " + (Time.realtimeSinceStartup - startTime) + " seconds");
        
        RenderTexture.active = activeRt;
        
        // diffuse.Release();
        // normals.Release();
        
        Profiler.EndSample();
    }
 
    public static void CustomBlit(RenderTexture renderTarget, Texture sourceTexture, Material material, int destX, int destY, int destWidth, int destHeight, int srcX = 0, int srcY = 0, int srcWidth = -1, int srcHeight = -1)
    {
        // Check if the render target and source texture are valid
        if (renderTarget == null || sourceTexture == null)
        {
            Debug.LogError("CustomBlit: Render target or source texture is null.");
            return;
        }

        // Set default source dimensions if not provided
        if (srcWidth < 0) srcWidth = sourceTexture.width;
        if (srcHeight < 0) srcHeight = sourceTexture.height;

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, renderTarget.width, renderTarget.height, 0);

        // Define source and destination rectangles
        Rect destRect = new Rect(destX, destY, destWidth, destHeight);
        Rect srcRect = new Rect((float)srcX / (float)sourceTexture.width, (float)srcY / (float)sourceTexture.height, (float)srcWidth / (float)sourceTexture.width, (float)srcHeight / (float)sourceTexture.height) ;

        
        // If a material is provided, use Graphics.DrawTexture with the material
        Graphics.DrawTexture(destRect, sourceTexture, srcRect, 0, 0, 0, 0, material);
        
      
        GL.PopMatrix();
    }

    
    public static void DoPadding(RenderTexture target, Texture2D source, Rect rect, int pad, Material flipMaterial)
    {
    
        // Dimensions
        int x = (int)rect.x;
        int y = (int)rect.y;
        int width = (int)rect.width;
        int height = (int)rect.height;
        int swidth = (int)source.width;
        int sheight = (int)source.height;
        int spad = pad / 4;
        // Top
        flipMaterial.SetFloat("_FlipVertical", 1);
        CustomBlit(target, source, flipMaterial, x, y - pad, width, pad, 0, 0, swidth, spad);

        // Bottom
        flipMaterial.SetFloat("_FlipVertical", 0);
        CustomBlit(target, source, flipMaterial, x, y + height, width, pad, 0, height - spad, swidth, spad);

        // Left
        flipMaterial.SetFloat("_FlipHorizontal", 1);
        CustomBlit(target, source, flipMaterial, x - pad, y, pad, height, 0, 0, spad, sheight);

        // Right
        flipMaterial.SetFloat("_FlipHorizontal", 0);
        CustomBlit(target, source, flipMaterial, x + width, y, pad, height, width - spad, 0, spad, sheight);

        // Corners
        flipMaterial.SetFloat("_FlipHorizontal", 1);
        flipMaterial.SetFloat("_FlipVertical", 1);
        CustomBlit(target, source, flipMaterial, x - pad, y - pad, pad, pad, 0, 0, spad, spad); // Top Left

        flipMaterial.SetFloat("_FlipHorizontal", 0);
        CustomBlit(target, source, flipMaterial, x + width, y - pad, pad, pad, width - spad, 0, spad, spad); // Top Right

        flipMaterial.SetFloat("_FlipVertical", 0);
        CustomBlit(target, source, flipMaterial, x + width, y + height, pad, pad, width - spad, height - spad, spad, spad); // Bottom Right

        flipMaterial.SetFloat("_FlipHorizontal", 1);
        CustomBlit(target, source, flipMaterial, x - pad, y + height, pad, pad, 0, height - spad, spad, spad); // Bottom Left

        //Fix it
        flipMaterial.SetFloat("_FlipVertical", 0);
        flipMaterial.SetFloat("_FlipHorizontal", 0);
    }
}
 