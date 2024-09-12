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

    private bool _useLifecycleExecute;
    private bool _useLifecycleOnCameraSetup;
    private bool _useLifecycleOnCameraCleanup;
    private bool _useLifecycleConfigure;

    public AirshipScriptableRenderPass(IntPtr thread, int featureId, int passId, string name) {
        Thread = thread;
        Name = name;
        FeatureId = featureId;
        PassId = passId;



        Debug.Log($"Created RenderPass, lifecycles {{ execute={_useLifecycleExecute}, configure={_useLifecycleConfigure}, cameraSetup={_useLifecycleOnCameraSetup}, cameraCleanup={_useLifecycleOnCameraCleanup} }}");
    }

    internal void EnableLifecycleMethods() {
        _useLifecycleExecute = LuauPlugin.LuauHasRenderPassMethod(LuauContext.RenderPass, Thread, FeatureId,
            AirshipScriptableRenderPassMethod.AirshipExecute);
        
        _useLifecycleOnCameraSetup = LuauPlugin.LuauHasRenderPassMethod(LuauContext.RenderPass, Thread, FeatureId,
            AirshipScriptableRenderPassMethod.AirshipOnCameraSetup);
        
        _useLifecycleOnCameraCleanup = LuauPlugin.LuauHasRenderPassMethod(LuauContext.RenderPass, Thread, FeatureId,
            AirshipScriptableRenderPassMethod.AirshipOnCameraCleanup);
        
        _useLifecycleConfigure = LuauPlugin.LuauHasRenderPassMethod(LuauContext.RenderPass, Thread, FeatureId,
            AirshipScriptableRenderPassMethod.AirshipConfigure);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
        if (!_useLifecycleConfigure) return;
        
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        var texDescId = ThreadDataManager.AddObjectReference(Thread, cameraTextureDescriptor);
        LuauPlugin.LuauRenderPassConfigure(LuauContext.RenderPass, Thread, FeatureId, cmdId, texDescId);
        
        ThreadDataManager.DeleteObjectReference(texDescId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
        if (!_useLifecycleExecute) return;
        
        // TODO: Add renderingData struct support somehow?
        var cmd = CommandBufferPool.Get(name: Name);
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        
        var renderingDataId = ThreadDataManager.AddObjectReference(Thread, renderingData);
        
        // Execute the render pass
        LuauPlugin.LuauRenderPassExecute(LuauContext.RenderPass, Thread, FeatureId, cmdId, renderingDataId); 
        
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        
        ThreadDataManager.DeleteObjectReference(renderingDataId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
        if (!_useLifecycleOnCameraSetup) return;
        
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        var renderingDataId = ThreadDataManager.AddObjectReference(Thread, renderingData);
        
        LuauPlugin.LuauRenderPassCameraSetup(LuauContext.RenderPass, Thread, FeatureId, cmdId, renderingDataId);
        
        ThreadDataManager.DeleteObjectReference(renderingDataId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }

    public override void OnCameraCleanup(CommandBuffer cmd) {
        if (!_useLifecycleOnCameraCleanup) return;
        
        var cmdId = ThreadDataManager.AddObjectReference(Thread, cmd);
        LuauPlugin.LuauRenderPassCameraCleanup(LuauContext.RenderPass, Thread, FeatureId, cmdId);
        ThreadDataManager.DeleteObjectReference(cmdId);
    }
}
