using UnityEngine;

[LuauAPI]
public static class RenderUtils{
    public static RenderTexture CreateDefaultRenderTexture(int width, int height){
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 8, 0) {
            sRGB = false,
            autoGenerateMips = false,
            useMipMap = false
        };
        return new RenderTexture(descriptor);
    }

    public static Texture2D CreateDefaultTexture2D(int width, int height){
        return new Texture2D(width, height);
    }

    public static Texture2D CreateTexture2D(int width, int height, TextureFormat format, bool mipChain, bool linear){
        return new Texture2D(width, height, format, mipChain, linear);
    }
}
