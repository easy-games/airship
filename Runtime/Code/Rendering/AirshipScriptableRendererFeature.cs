using System;
using System.Runtime.InteropServices;
using Code.Luau;
using Luau;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Build.Pipeline {
    public class AirshipScriptableRendererFeature : ScriptableRendererFeature {
        public AirshipRenderPassScript script;
        private IntPtr _thread;

        private bool _init = false;
        
        public override void Create() {
            if (!Application.isPlaying) return;
            if (_init) return;
            
            LuauCore.CoreInstance.CheckSetup();
            Debug.Log($"RenderFeature should now be loading {script.m_path}");
            if (!script) return;

            var path = script.m_path;
            var filenameStr = Marshal.StringToCoTaskMemUTF8(path);
            var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned);
            _thread = LuauPlugin.LuauCreateThread(LuauContext.Game, gch.AddrOfPinnedObject(), script.m_bytes.Length,
                filenameStr, path.Length, 0, true);
            
            Marshal.FreeCoTaskMem(filenameStr);
            gch.Free();
            
            Debug.Log($"Created thread id {_thread} for {path}");
            
            // TODO: Create render pass reference

            _init = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            
        }

        private void OnDestroy() {
            Debug.Log("Should clean up render pass");
            // TODO: Cleanup render pass
        }
    }
}