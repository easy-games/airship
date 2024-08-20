using Code.Luau;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URP {
    public class AirshipScriptableRendererFeature : ScriptableRendererFeature {
        public AirshipRenderPassScript renderPass;
        
        public override void Create() { }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
    }

    // public class TestPass : ScriptableRenderPass {
    //     public Material material;
    //     
    //     public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
    //         var cmd = CommandBufferPool.Get("TestPass");
    //         context.ExecuteCommandBuffer(cmd);
    //         CommandBufferPool.Release(cmd);
    //     }
    // }
    
    
}

// [CustomEditor(typeof(AirshipScriptableRendererFeature))]
// [SupportedOnRenderPipeline]
// public class AirshipRenderPipelineEditor : UnityEditor.Editor {
//     public override void OnInspectorGUI() {
//         EditorGUILayout.HelpBox("This may be inefficient to use and is entirely experimental", MessageType.Warning);
//     }
// }