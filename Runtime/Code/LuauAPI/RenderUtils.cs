using UnityEngine;

[LuauAPI]
public static class RenderUtils{

    //RENDER TEXTURES
    public static RenderTexture CreateDefaultRenderTexture(int width, int height){
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 8, 0) {
            sRGB = false,
            autoGenerateMips = false,
            useMipMap = false
        };
        return new RenderTexture(descriptor);
    }

    //TEXTURES
    public static Texture2D CreateDefaultTexture2D(int width, int height){
        return new Texture2D(width, height);
    }

    public static Texture2D CreateTexture2D(int width, int height, TextureFormat format, bool mipChain, bool linear){
        return new Texture2D(width, height, format, mipChain, linear);
    }

    //SPRITES
    public static Sprite CreateDefaultSprite(Texture2D texture){
        return Sprite.Create(texture, new Rect(0,0, texture.width, texture.height), new Vector2(texture.width/2, texture.height/2));
    }

    public static Sprite CreateSprite(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit){
        return Sprite.Create(texture, rect, pivot, pixelsPerUnit);
    }

    //RENDERERS
    public static void ClearMaterial(Renderer ren, int materialI){
        ren.materials[materialI] = null;
    }
    
    public static void ClearAllMaterials(Renderer ren){
        for(int i=0; i<ren.materials.Length; i++){
            ren.materials[i] = null;
        }
    }
}
