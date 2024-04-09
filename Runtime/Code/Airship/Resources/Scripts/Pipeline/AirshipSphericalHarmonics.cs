using UnityEngine;

public static class AirshipSphericalHarmonics {

    private static readonly int numberOfSamples = 2000; // Same as in the shader
    private static readonly int numberOfCoefficients = 9; // We're calculating coefficients up to 2nd order

    public static Vector4[] ProcessCubemapIntoSH(Cubemap cubemap) {

        ComputeBuffer shCoefficientsBuffer;
        ComputeShader sphericalHarmonicsShader = Resources.Load<ComputeShader>("CubemapToSphericalHarmonicsShader");
        int kernelID = sphericalHarmonicsShader.FindKernel("SHMain");

        // Create a buffer to hold SH coefficients (9 coefficients * 3 channels)
        shCoefficientsBuffer = new ComputeBuffer(numberOfCoefficients, sizeof(float) * 4); // float3 for each coefficient
        Vector4[] shCoefficients = new Vector4[numberOfCoefficients];

        // Assign the cubemap to the compute shader
        sphericalHarmonicsShader.SetTexture(kernelID, "_Cubemap", cubemap);

        // Assign the result buffer
        sphericalHarmonicsShader.SetBuffer(kernelID, "_SHCoefficients", shCoefficientsBuffer);

        // Dispatch the compute shader
        // 1, 1, 1 since we're only launching one thread for simplicity
        sphericalHarmonicsShader.Dispatch(kernelID, 1, 1, 1);

        // Retrieve the results from the buffer
        shCoefficientsBuffer.GetData(shCoefficients);

        // Output or process your SH coefficients
        //for (int i = 0; i < shCoefficients.Length; i++) {
        //    Debug.Log($"SH Coefficient {i}: {shCoefficients[i]}");
        //}

        // Clean up
        shCoefficientsBuffer.Release();

        return shCoefficients;
    }
}