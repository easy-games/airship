#if UNITY_EDITOR && !AIRSHIP_PLAYER && !AIRSHIP_INTERNAL
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

public class AirshipShaderStripper : IPreprocessShaders {
    public int callbackOrder {
        get { return 0; }
    }
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) {
        Debug.Log("[Airship]: Clearing " + data.Count + " shaders from compilation.");
        data.Clear();
    }
}
#endif