using System;
using System.Runtime.InteropServices;
using Code.Luau;
using Luau;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace UnityEngine.Build.Pipeline {
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
            var cmdId = ThreadDataManager.GetOrCreateObjectId(cmd);
            LuauPlugin.LuauExecuteRenderPass(LuauContext.Game, Thread, FeatureId, PassId, cmdId); 
            
            CommandBufferPool.Release(cmd);
        }
    }
    
    public class AirshipScriptableRendererFeature : ScriptableRendererFeature {
        private static int _idGen = 0;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _idGen = 0;
        }
        
        private IntPtr _thread;
        private bool _init = false;
        
        [SerializeField]
        private AirshipRenderPassScript script;
        private int FeatureId { get; } = _idGen++;
        private AirshipScriptableRenderPass _renderPass;
        
        public override void Create() {
            if (!Application.isPlaying) return;
            if (_init) return;
            
            LuauCore.CoreInstance.CheckSetup();
            if (!script) return;

            var path = script.m_path;
            var filenameStr = Marshal.StringToCoTaskMemUTF8(path);
            var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned);
            _thread = LuauPlugin.LuauCreateThread(LuauContext.Game, gch.AddrOfPinnedObject(), script.m_bytes.Length,
                filenameStr, path.Length, 0, true);
            
            Marshal.FreeCoTaskMem(filenameStr);
            gch.Free();
            
            // Create the render pass, add it to the registry
            LuauPlugin.LuauCreateRenderPass(LuauContext.Game, _thread, FeatureId);
            _renderPass = new AirshipScriptableRenderPass(this.name, _thread);
            _init = true;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            // Enqueue the scripted pass
            renderer.EnqueuePass(this._renderPass);
        }

        private void OnDestroy() {
            Debug.Log("Should clean up render pass");
            // TODO: Cleanup render pass
        }
    }
}