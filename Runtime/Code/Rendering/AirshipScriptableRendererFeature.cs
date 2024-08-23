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
    
    [SerializeField][CanBeNull]
    private AirshipRenderPassScript script;
    private AirshipScriptableRenderPass _renderPass;

    private int InstanceId { get; set; }

    public override void Create() {
        if (!Application.isPlaying) return;
        if (script == null) return;
       
        
        var path = script.m_path;
        if (_init || LuauCore.CoreInstance == null) return;
        
        Debug.Log("Creating renderer", this);
        LuauCore.CoreInstance.CheckSetup();

        var featureObjectId = ThreadDataManager.AddObjectReference(_thread, this);
        InstanceId = featureObjectId;
        
        var filenameStr = Marshal.StringToCoTaskMemUTF8(path);
        var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned);
        _thread = LuauPlugin.LuauCreateThread(LuauContext.Game, gch.AddrOfPinnedObject(), script.m_bytes.Length,
            filenameStr, path.Length, 0, true);
        
        Marshal.FreeCoTaskMem(filenameStr);
        gch.Free();
        
        // Create the render pass, add it to the registry
        LuauPlugin.LuauCreateRenderPass(LuauContext.Game, _thread, featureObjectId, 0);
        _renderPass = new AirshipScriptableRenderPass(_thread, featureObjectId, 0, path);
        _init = true;
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
