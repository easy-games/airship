using System;
using System.Collections.Generic;
using UnityEngine;

namespace Luau {
    public sealed class LuauState : IDisposable {
        public LuauContext Context { get; }

        private bool _disposed = false;
        private Dictionary<IntPtr, ScriptBinding> threads = new();

        private static readonly Dictionary<LuauContext, LuauState> StatesPerContext = new();

        public static LuauState FromContext(LuauContext context) {
            if (StatesPerContext.TryGetValue(context, out var state)) {
                return state;
            }

            var newState = new LuauState(context);
            StatesPerContext[context] = newState;
            
            return newState;
        }

        public static void ShutdownAll() {
            foreach (var pair in StatesPerContext) {
                pair.Value.Dispose();
            }
            StatesPerContext.Clear();
        }

        public static void Shutdown(LuauContext context) {
            if (StatesPerContext.TryGetValue(context, out var state)) {
                state.Shutdown();
            }
        }

        public static void UpdateAll() {
            foreach (var (_, state) in StatesPerContext) {
                state.OnUpdate();
            }
        }

        public static void LateUpdateAll() {
            foreach (var (_, state) in StatesPerContext) {
                state.OnLateUpdate();
            }
        }

        public static void FixedUpdateAll() {
            foreach (var (_, state) in StatesPerContext) {
                state.OnFixedUpdate();
            }
        }

        public static void UpdateAllAtEndOfFrame() {
            foreach (var (context, _) in StatesPerContext) {
                ThreadDataManager.RunEndOfFrame(context);
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reload() {
            ShutdownAll();
        }

        private LuauState(LuauContext context) {
            Context = context;
            if (!LuauPlugin.LuauOpenState(Context)) {
                throw new Exception("failed to open luau state");
            }
        }

        public void AddThread(IntPtr thread, ScriptBinding binding) {
            threads.TryAdd(thread, binding);
        }

        public bool TryGetScriptBindingFromThread(IntPtr thread, out ScriptBinding binding) {
            return threads.TryGetValue(thread, out binding);
        }

        public void Shutdown() {
            Dispose();
        }
        
        public void Dispose() {
            DisposeResources(true);
            GC.SuppressFinalize(this);
        }

        private void DisposeResources(bool disposing) {
            if (_disposed) return;
            _disposed = true;
            
            LuauPlugin.LuauCloseState(Context);
        }

        ~LuauState() {
            DisposeResources(false);
        }

        private void OnUpdate() {
            LuauPlugin.LuauRunTaskScheduler(Context);
            LuauPlugin.LuauUpdateAllAirshipComponents(Context, AirshipComponentUpdateType.AirshipUpdate, Time.fixedDeltaTime);
        }

        private void OnLateUpdate() {
            LuauPlugin.LuauUpdateAllAirshipComponents(Context, AirshipComponentUpdateType.AirshipLateUpdate, Time.fixedDeltaTime);
        }

        private void OnFixedUpdate() {
            LuauPlugin.LuauUpdateAllAirshipComponents(Context, AirshipComponentUpdateType.AirshipFixedUpdate, Time.fixedDeltaTime);
        }
    }
}
