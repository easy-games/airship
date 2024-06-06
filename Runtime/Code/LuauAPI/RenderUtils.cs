using UnityEngine;

[LuauAPI]
public static class RenderUtils{
    public static RenderTexture CreateDefaultRenderTexture(int width, int height){
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 8,0);
        descriptor.sRGB = false;
        descriptor.autoGenerateMips = false;
        descriptor.useMipMap = false;
        return new RenderTexture(descriptor);
    }
}
