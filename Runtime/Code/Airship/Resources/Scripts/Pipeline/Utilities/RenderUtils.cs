using UnityEngine;

[LuauAPI]
public static class RenderUtils{
    public static RenderTexture CreateDefaultRenderTexture(int width, int height){
        Debug.Log("Creating render textgure");
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 0,0);
        Debug.Log("Setting render textgure srgb");
        descriptor.sRGB = false;
        Debug.Log("Returning render textgure");
        return new RenderTexture(descriptor);
    }
}
