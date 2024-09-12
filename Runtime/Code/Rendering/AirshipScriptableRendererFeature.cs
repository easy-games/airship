using System;
using System.Runtime.InteropServices;
using Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class AirshipScriptableRendererFeature : ScriptableRendererFeature {
    private IntPtr _thread;
    private bool _init;

    [CanBeNull][SerializeField] public LuauMetadata m_metadata = new();
    [CanBeNull] public AirshipRenderPassScript script;
    private AirshipScriptableRenderPass _renderPass;

    private int InstanceId { get; set; }
    private int PassId { get; } = 32;

    internal void ReconcileMetadata() {
        if (script != null) {
            LuauMetadata.ReconcileMetadata(script.m_metadata, m_metadata);
        }
        else {
            m_metadata = new LuauMetadata();
        }
    }
    
    private bool CreateThread() {
        if (_thread != IntPtr.Zero) {
            return false;
        }

        if (!script || !script.m_compiled) {
            Debug.LogError("[RenderFeature] Could not load RenderFeature without script or non-compiled script");
            return false;
        }

        var cleanPath = AirshipScriptUtils.CleanupFilePath(script.m_path);
        LuauCore.CoreInstance.CheckSetup();

        // Try fetching the cached module
        var thread = LuauPlugin.LuauCreateThreadWithCachedModule(LuauContext.RenderPass, cleanPath, 0);
        if (thread != IntPtr.Zero) {
            _thread = thread;
            var featureObjectId = ThreadDataManager.AddObjectReference(_thread, this);
            InstanceId = featureObjectId;
            LuauPlugin.LuauCreateRenderPass(LuauContext.RenderPass, _thread, InstanceId);
            Debug.Log("[RenderFeature] Create cached thread for feature");
            return true;
        }
        
        // Else we'll try creating the thread
        var filenameStr = Marshal.StringToCoTaskMemUTF8(cleanPath);
        var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned);
        _thread = LuauPlugin.LuauCreateThread(LuauContext.RenderPass, gch.AddrOfPinnedObject(), script.m_bytes.Length,
            filenameStr, cleanPath.Length, 0, true);
        
        Marshal.FreeCoTaskMem(filenameStr);
        gch.Free();
        if (_thread == IntPtr.Zero) {
            Debug.Log("[RenderFeature] Null ptr");
            return false;
        }
        
        // Resume thread
        int ret = LuauPlugin.LuauRunThread(_thread);
        if (ret == 1) { // yielded
            Debug.Log("[RenderFeature] Thread yielded");
            return false;
        } else if (ret == -1) {
            // errored
            Debug.Log("[RenderFeature] Thread errored");
            return false;
        }
        else {
            // Create & cache render pass
            LuauPlugin.LuauCacheModuleOnThread(_thread, cleanPath);
            var featureObjectId = ThreadDataManager.AddObjectReference(_thread, this);
            InstanceId = featureObjectId;
            LuauPlugin.LuauCreateRenderPass(LuauContext.RenderPass, _thread, InstanceId);
            Debug.Log("[RenderFeature] Create non-cached thread for feature");
            return true;
        }
    }
    
    public override void Create() {
        if (!Application.isPlaying) return;
        if (script == null) return;
       
        
        var path = script.m_path;
        if (_init || LuauCore.CoreInstance == null) return;
        LuauCore.CoreInstance.CheckSetup();
        if (!CreateThread()) return;

        _renderPass = new AirshipScriptableRenderPass(_thread, InstanceId, PassId, name);
        
        _init = true;
        Debug.Log($"Created RendererFeature with featureId {InstanceId}");
    }
    
    protected override void Dispose(bool disposing) {
        if (!disposing) return;
        if (_thread == IntPtr.Zero) return;
        
        Debug.Log("Disposing renderer");
        _init = false;
        
        if (InstanceId != -1) ThreadDataManager.DeleteObjectReference(InstanceId);
        InstanceId = -1;
        
        LuauPlugin.LuauDestroyThread(_thread);
        _thread = IntPtr.Zero;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
        // Enqueue the scripted pass
        if (_renderPass != null && _init) {
            renderer.EnqueuePass(_renderPass);
        }
    }
}
