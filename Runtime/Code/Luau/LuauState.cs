using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

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

        public bool Active { get; private set; } = true;

        private bool _disposed = false;
        private readonly Dictionary<IntPtr, AirshipComponent> _threads = new();
        
        private readonly List<CallbackRecord> _pendingCoroutineResumesA = new();
        private readonly List<CallbackRecord> _pendingCoroutineResumesB = new();
        private List<CallbackRecord> _currentBuffer;

        private static readonly Dictionary<LuauContext, LuauState> StatesPerContext = new();

        private GameObject _luauCoreModulesFolder;
        private GameObject _luauModulesFolder;

        public static LuauState FromContext(LuauContext context) {
            if (StatesPerContext.TryGetValue(context, out var state)) {
                return state;
            }

            var newState = new LuauState(context);
            StatesPerContext[context] = newState;
            
            return newState;
        }

        public static bool IsContextActive(LuauContext context) {
            return StatesPerContext.TryGetValue(context, out var state) && state.Active;
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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.BeginSample($"Update{Enum.GetName(typeof(LuauContext), state.Context)}");
#endif
                try {
                    state.OnUpdate();
                } finally {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    Profiler.EndSample();
#endif
                }
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
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnSystemReload() {
            ShutdownAll();
        }

        private LuauState(LuauContext context) {
            Context = context;
            _currentBuffer = _pendingCoroutineResumesA;
            // if (!LuauPlugin.LuauOpenState(Context)) {
            //     throw new Exception("failed to open luau state");
            // }
            LuauPlugin.OpenState(Context);
        }

        public GameObject GetRequireGameObject() {
            if (_luauModulesFolder == null) {
                if (_luauCoreModulesFolder == null) {
                    var coreGo = GameObject.Find("AirshipCore");
                    if (!coreGo) {
                        coreGo = new GameObject("AirshipCore");
                    }

                    var modulesFolder = GameObject.Find("LuauModules");
                    if (modulesFolder == null) {
                        modulesFolder = new GameObject("LuauModules");
                        modulesFolder.transform.SetParent(coreGo.transform);
                    }

                    _luauCoreModulesFolder = modulesFolder;
                }
                _luauModulesFolder = new GameObject(Context.ToString());
                _luauModulesFolder.transform.SetParent(_luauCoreModulesFolder.transform);
            }

            return _luauModulesFolder;
        }

        public void Reset() {
            Active = false;
            _currentBuffer?.Clear();
            
            var uniqueInstanceIds = LuauPlugin.GetUniqueInstanceIds(Context);
            ThreadDataManager.DeleteObjectReferencesList(uniqueInstanceIds);
            
            LuauPlugin.Reset(Context);
            if (_luauModulesFolder != null) {
                Object.Destroy(_luauModulesFolder);
                _luauModulesFolder = null;
                _luauCoreModulesFolder = null;
            }
            Active = true;
        }

        public void AddThread(IntPtr thread, AirshipComponent binding) {
            _threads.TryAdd(thread, binding);
        }

        public void RemoveThread(IntPtr thread) {
            _threads.Remove(thread);
        }

        public int ResumeScript(AirshipComponent binding) {
            return LuauPlugin.RunThread(binding.thread);
        }

        public bool TryGetScriptBindingFromThread(IntPtr thread, out AirshipComponent binding) {
            return _threads.TryGetValue(thread, out binding);
        }

        public void AddCallbackToBuffer(IntPtr thread, string res) {
            _currentBuffer.Add(new CallbackRecord(thread, res));
        }

        public void Shutdown() {
            Active = false;
            Dispose();
        }
        
        public void Dispose() {
            DisposeResources(true);
            GC.SuppressFinalize(this);
        }

        private void DisposeResources(bool disposing) {
            if (_disposed) return;
            _disposed = true;
            
            LuauPlugin.CloseState(Context);
        }

        ~LuauState() {
            DisposeResources(false);
        }

        private void OnUpdate() {
            LuauPlugin.ResetTimeCache(Context, false);
            
            var runBuffer = _currentBuffer;
            if (_currentBuffer == _pendingCoroutineResumesA) {
                _currentBuffer = _pendingCoroutineResumesB;
            } else {
                _currentBuffer = _pendingCoroutineResumesA;
            }

            Profiler.BeginSample("RunYieldedThreads");
            foreach (CallbackRecord coroutineCallback in runBuffer) {
                // Context of the callback is in coroutineCallback.trace
                ThreadDataManager.SetThreadYielded(coroutineCallback.callback, false);
                LuauPlugin.RunThread(coroutineCallback.callback);
            }
            Profiler.EndSample();
            runBuffer.Clear();
            
            Profiler.BeginSample("RunTaskScheduler");
            LuauPlugin.RunTaskScheduler(Context);
            Profiler.EndSample();
            Profiler.BeginSample("UpdateAirshipComponents");
            LuauPlugin.UpdateAllAirshipComponents(Context, AirshipComponentUpdateType.AirshipUpdate, Time.deltaTime);
            Profiler.EndSample();
        }

        private void OnLateUpdate() {
            LuauPlugin.UpdateAllAirshipComponents(Context, AirshipComponentUpdateType.AirshipLateUpdate, Time.deltaTime);
        }

        private void OnFixedUpdate() {
            LuauPlugin.ResetTimeCache(Context, true);
            LuauPlugin.UpdateAllAirshipComponents(Context, AirshipComponentUpdateType.AirshipFixedUpdate, Time.fixedDeltaTime);
            LuauPlugin.ResetTimeCache(Context, false);
        }
    }
}
