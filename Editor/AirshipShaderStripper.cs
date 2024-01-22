#if UNITY_EDITOR && !AIRSHIP_PLAYER
using System.Collections.Generic;
using Editor.Packages;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

public class AirshipShaderStripper : IPreprocessShaders, IPreprocessComputeShaders {
    public int callbackOrder {
        get { return 0; }
    }
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) {
        if (true) return;
        if (AirshipPackagesWindow.buildingPackageId == "@Easy/CoreMaterials") {
            Debug.Log("Allowing " + AirshipPackagesWindow.buildingPackageId + " to include shader " + shader.name);
            // if (shader.name.Contains("WorldShaderPBR")) {
            //     Debug.Log("Allowing WorldShaderPBR");
            //     return;
            // }
        }
        if (
            shader.name.Contains("AirshipShaderVariants")
            || shader.name.Contains("TextMeshPro")
        ) {
            Debug.Log("IGNORING " + shader.name);
            return;
        }
        Debug.Log("Shader name: " + shader.name);
        Debug.Log("[Airship]: Clearing " + data.Count + " shaders from compilation. packageId=" + AirshipPackagesWindow.buildingPackageId);
        data.Clear();
    }

    public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data) {
        if (true) return;
        Debug.Log("[Airship]: Clearing " + data.Count + " compute shaders from compilation.");
        data.Clear();
    }
}
#endif