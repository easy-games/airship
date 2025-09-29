void SampleWithDerivatives_float(UnityTexture2D tex, UnitySamplerState samplerState, float2 uv, float2 dx, float2 dy, out float4 color) {
    // The lower our derivative scale the further out we'll see
    // high quality mips. Because we use a texture atlas that can bleed this needs
    // to be relatively low.
    float derivativeScale = 0.05;
    color = tex.SampleGrad(samplerState, uv, dx * derivativeScale, dy * derivativeScale);
}