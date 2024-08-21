using Code.Luau;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Build.Pipeline {
    public class AirshipScriptableRendererFeature : ScriptableRendererFeature {
        [FormerlySerializedAs("renderPass")] public AirshipRenderPassScript script;
        
        public override void Create() { }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
    }
}