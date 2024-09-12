using System;
using System.Runtime.InteropServices;
using Code.Luau;
using JetBrains.Annotations;
using Luau;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class AirshipScriptableRendererFeature : ScriptableRendererFeature {
    private static int _idGen = 0;
    private static int NextFeatureId => _idGen++;

    private IntPtr _thread;
    private bool _init;

    [CanBeNull][SerializeField] public LuauMetadata m_metadata = new();
    [CanBeNull] public AirshipRenderPassScript script;
    private AirshipScriptableRenderPass _renderPass;
    private int FeatureId { get; set; } = -1;

    internal void ReconcileMetadata() {
        if (script != null) {
            LuauMetadata.ReconcileMetadata(script.m_metadata, m_metadata);
        }
        else {
            m_metadata = new LuauMetadata();
        }
    }
    
    private bool CreateThread(out AirshipScriptableRenderPass renderPass) {
        if (_thread != IntPtr.Zero) {
            renderPass = null;
            return false;
        }

        if (!script || !script.m_compiled) {
            Debug.LogError("[RenderFeature] Could not load RenderFeature without script or non-compiled script");
            renderPass = null;
            return false;
        }

        var cleanPath = AirshipScriptUtils.CleanupFilePath(script.m_path);
        LuauCore.CoreInstance.CheckSetup();

        var featureObjectId = ThreadDataManager.GetOrCreateObjectId(this);
        FeatureId = featureObjectId;
        
        // Try fetching the cached module
        var thread = LuauPlugin.LuauCreateThreadWithCachedModule(LuauContext.RenderPass, cleanPath, 0);
        if (thread != IntPtr.Zero) {
            _thread = thread;
            
            renderPass = new AirshipScriptableRenderPass(_thread, featureObjectId, 0, name);
            var renderPassInstanceId = ThreadDataManager.GetOrCreateObjectId(renderPass);
            
            LuauPlugin.LuauCreateRenderPass(LuauContext.RenderPass, _thread, featureObjectId, renderPassObjectId: renderPassInstanceId);
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
            renderPass = null;
            return false;
        }
        
        // Resume thread
        int ret = LuauPlugin.LuauRunThread(_thread);
        if (ret == 1) { // yielded
            renderPass = null;
            Debug.Log("[RenderFeature] Thread yielded");
            return false;
        } else if (ret == -1) {
            // errored
            renderPass = null;
            Debug.Log("[RenderFeature] Thread errored");
            return false;
        }
        else {
            // Create & cache render pass
            LuauPlugin.LuauCacheModuleOnThread(_thread, cleanPath);
            
            renderPass = new AirshipScriptableRenderPass(_thread, featureObjectId, 0, name);
            var renderPassInstanceId = ThreadDataManager.GetOrCreateObjectId(renderPass);
            
            LuauPlugin.LuauCreateRenderPass(LuauContext.RenderPass, _thread, FeatureId, renderPassObjectId: renderPassInstanceId);
            return true;
        }
    }
    
    public override void Create() {
        if (!Application.isPlaying) return;
        if (script == null) return;
       
        
        var path = script.m_path;
        if (_init || LuauCore.CoreInstance == null) return;
        LuauCore.CoreInstance.CheckSetup();
        
        if (!CreateThread(out var renderPass)) return;
        _renderPass = renderPass;
        _renderPass.EnableLifecycleMethods();
        
        _init = true;
        Debug.Log($"Created RendererFeature with featureId {FeatureId}");
    }
    
    protected override void Dispose(bool disposing) {
        if (!disposing) return;
        if (_thread == IntPtr.Zero) return;
        
        Debug.Log("Disposing renderer");
        _init = false;
        
        if (FeatureId != -1) ThreadDataManager.DeleteObjectReference(FeatureId);
        FeatureId = -1;
        
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
