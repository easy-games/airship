using System;
using System.Collections.Generic;
using UnityEngine;

namespace Luau {
    public sealed class LuauState : IDisposable {
        private class CallbackRecord {
            public readonly IntPtr callback;
            public readonly string trace;
            public CallbackRecord(IntPtr callback, string trace) {
                this.callback = callback;
                this.trace = trace;
            }
        }
        
        public LuauContext Context { get; }

        private bool _disposed = false;
        private readonly Dictionary<IntPtr, ScriptBinding> _threads = new();
        
        private readonly List<CallbackRecord> _pendingCoroutineResumesA = new();
        private readonly List<CallbackRecord> _pendingCoroutineResumesB = new();
        private List<CallbackRecord> _currentBuffer;

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
        private static void OnSystemReload() {
            ShutdownAll();
        }

        private LuauState(LuauContext context) {
            Context = context;
            _currentBuffer = _pendingCoroutineResumesA;
            if (!LuauPlugin.LuauOpenState(Context)) {
                throw new Exception("failed to open luau state");
            }
        }

        public void Reset() {
            _currentBuffer?.Clear();
            LuauPlugin.LuauReset(Context);
        }

        public void AddThread(IntPtr thread, ScriptBinding binding) {
            _threads.TryAdd(thread, binding);
        }

        public int ResumeScript(ScriptBinding binding) {
            return LuauPlugin.LuauRunThread(binding.m_thread);
        }

        public bool TryGetScriptBindingFromThread(IntPtr thread, out ScriptBinding binding) {
            return _threads.TryGetValue(thread, out binding);
        }

        public void AddCallbackToBuffer(IntPtr thread, string res) {
            _currentBuffer.Add(new CallbackRecord(thread, res));
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
            var runBuffer = _currentBuffer;
            if (_currentBuffer == _pendingCoroutineResumesA) {
                _currentBuffer = _pendingCoroutineResumesB;
            } else {
                _currentBuffer = _pendingCoroutineResumesA;
            }

            foreach (CallbackRecord coroutineCallback in runBuffer) {
                // Context of the callback is in coroutineCallback.trace
                ThreadDataManager.SetThreadYielded(coroutineCallback.callback, false);
                LuauPlugin.LuauRunThread(coroutineCallback.callback);
            }
            runBuffer.Clear();
            
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
