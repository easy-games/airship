using System;
using Code.Luau;
using Luau;
using UnityEngine;
using UnityEngine.Assertions;
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

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        LuauPlugin.LuauRenderPassConfigure(LuauContext.Game, Thread, FeatureId, cmdId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
        // TODO: Add renderingData struct support somehow?
        var cmd = CommandBufferPool.Get(name: Name);
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        
        // Execute the render pass
        LuauPlugin.LuauRenderPassExecute(LuauContext.Game, Thread, FeatureId, cmdId); 
        
        CommandBufferPool.Release(cmd);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        LuauPlugin.LuauRenderPassCameraSetup(LuauContext.Game, Thread, FeatureId, cmdId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }

    public override void OnCameraCleanup(CommandBuffer cmd) {
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        LuauPlugin.LuauRenderPassCameraCleanup(LuauContext.Game, Thread, FeatureId, cmdId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }
}
