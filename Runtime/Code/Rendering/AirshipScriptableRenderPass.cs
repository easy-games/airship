using System;
using Code.Luau;
using Luau;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AirshipScriptableRenderPass : ScriptableRenderPass {
    public IntPtr Thread { get; }
    public AirshipRenderPassScript Script { get; }
    public int FeatureId { get; }
    public int PassId { get; }
    public string Name { get; }

    public AirshipScriptableRenderPass(IntPtr thread, int featureId, int passId, string name) {
        Thread = thread;
        Name = name;
        FeatureId = featureId;
        PassId = passId;
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
        var cmd = CommandBufferPool.Get(name: Name);
        
        // Execute the render pass
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        LuauPlugin.LuauExecuteRenderPass(LuauContext.Game, Thread, FeatureId, PassId, cmdId); 
        
        CommandBufferPool.Release(cmd);
    }
}
