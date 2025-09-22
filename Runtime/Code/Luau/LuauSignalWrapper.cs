using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;

namespace Luau {
    public class LuauSignalWrapper {
        internal class LuauSignalDestroyWatcher : MonoBehaviour {
            internal event Action<bool> DestroyCallbacks;
            internal LuauContext Context;
            
            private bool _destroyed = false;

            private void HandleDestroyedEvent(bool contextReset) {
                if (_destroyed) return;
                
                _destroyed = true;
                LuauCore.onResetInstance -= OnContextReset;
                DestroyCallbacks?.Invoke(contextReset);
            }
            
            private void OnContextReset(LuauContext ctx) {
                if (ctx != Context) return;

                HandleDestroyedEvent(true);
            }
            
            private void Awake() {
                hideFlags = HideFlags.HideInInspector;
                LuauCore.onResetInstance += OnContextReset;
            }
            
            private void OnDestroy() {
                HandleDestroyedEvent(false);
            }
        }

        private static int _instanceIdGen = 0;
        private static readonly Dictionary<System.Object, int> InstanceIds = new();

        private static int GetOrCreateId(object obj) {
            var idFound = InstanceIds.TryGetValue(obj, out var id);
            if (!idFound) {
                id = _instanceIdGen++;
                InstanceIds[obj] = id;
            }
            return id;
        }

        internal event Action RequestDisconnect;
        
        private readonly LuauContext _context;
        private readonly IntPtr _thread;
        private readonly int _instanceId;
        private readonly ulong _propNameHash;
        
#if UNITY_EDITOR
        private static List<LuauSignalWrapper> _staticSignalWrappers = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            foreach (var signalWrapper in _staticSignalWrappers) {
                signalWrapper.Destroy();
            }
            _staticSignalWrappers = new List<LuauSignalWrapper>();
            _instanceIdGen = 0;
            InstanceIds.Clear();
        }
#endif
        
        private static void WritePropertyToThread(IntPtr thread, object parameter) {
            if (parameter == null) {
                LuauCore.WritePropertyToThread(thread, null, null);
            } else {
                LuauCore.WritePropertyToThread(thread, parameter, parameter.GetType());
            }
        }
        
        public LuauSignalWrapper(LuauContext context, IntPtr thread, int instanceId, ulong propNameHash) {
            _context = context;
            _thread = thread;
            _instanceId = instanceId;
            _propNameHash = propNameHash;
        }
        
        public void HandleEvent_0() {
            HandleEvent();
        }
        
        public void HandleEvent_1<T0>(T0 p0) {
            HandleEvent(p0);
        }
        
        public void HandleEvent_2<T0, T1>(T0 p0, T1 p1) {
            HandleEvent(p0, p1);
        }
        
        public void HandleEvent_3<T0, T1, T2>(T0 p0, T1 p1, T2 p2) {
            HandleEvent(p0, p1, p2);
        }
        
        public void HandleEvent_4<T0, T1, T2, T3>(T0 p0, T1 p1, T2 p2, T3 p3) {
            HandleEvent(p0, p1, p2, p3);
        }

        private void HandleEvent(params object[] p) {
            if (!LuauState.IsContextActive(_context)) {
                // Debug.LogWarning("Attempted to fire MonoSignal, but context was not active; event cancelled");
                return;
            }
            
            Profiler.BeginSample("HandleCSToLuauSignalEvent");
            
            // var threadData = ThreadDataManager.GetThreadDataByPointer(_thread);
            // if (threadData != null && !threadData.m_error) {
                foreach (var param in p) {
                    WritePropertyToThread(_thread, param);
                }

                var alive = LuauPlugin.EmitSignal(_context, _thread, _instanceId, _propNameHash, p.Length);
                if (!alive) {
                    RequestDisconnect?.Invoke();
                }
            // }
            
            Profiler.EndSample();
        }

        public void Destroy() {
            RequestDisconnect?.Invoke();
        }

        private static LuauSignalDestroyWatcher GetSignalDestroyWatcherByContext(GameObject go, LuauContext context) {
            // Try to find existing watcher with the given context:
            foreach (var destroyWatcher in go.GetComponents<LuauSignalDestroyWatcher>()) {
                if (destroyWatcher.Context == context) {
                    return destroyWatcher;
                }
            }
            
            // Create a new one:
            var newDestroyWatcher = go.AddComponent<LuauSignalDestroyWatcher>();
            newDestroyWatcher.Context = context;
            
            return newDestroyWatcher;
        }

        private static void AddSignalDestroyWatcher(GameObject go, LuauContext context, Action<bool> onDestroy) {
            var destroyWatcher = GetSignalDestroyWatcherByContext(go, context);
            destroyWatcher.DestroyCallbacks += onDestroy;
        }

        private static GameObject GetGameObjectFromObject(object obj) {
            if (obj is GameObject go) return go;
            return obj is not MonoBehaviour behaviour ? null : behaviour.gameObject;
        }
        
        public static int HandleCsEvent(LuauContext context, IntPtr thread, object objectReference, int instanceId, ulong propNameHash, EventInfo eventInfo, bool staticClass) {
            var signalInstanceId = GetOrCreateId(objectReference);
            var newSignalCreated = LuauPlugin.PushSignal(context, thread, signalInstanceId, propNameHash);
            if (newSignalCreated) {
                GameObject go = null;
                if (!staticClass) {
                    go = GetGameObjectFromObject(objectReference);
                    if (go == null) return 0;
                }
            
                LuauPlugin.PinThread(thread);
                
                var signalWrapper = new LuauSignalWrapper(context, thread, signalInstanceId, propNameHash);

                var eventInfoParams = eventInfo.EventHandlerType.GetMethod("Invoke").GetParameters();

                var handlerMethodName = $"HandleEvent_{eventInfoParams.Length}";
                var method = signalWrapper.GetType().GetMethod(handlerMethodName);

                Delegate eventDelegate;
                if (eventInfo.EventHandlerType.IsGenericType) {
                    eventDelegate = Delegate.CreateDelegate(eventInfo.EventHandlerType, signalWrapper, method.MakeGenericMethod(eventInfo.EventHandlerType.GetGenericArguments()));
                }
                else {
                    eventDelegate = Delegate.CreateDelegate(eventInfo.EventHandlerType, signalWrapper, method);
                }
                
                eventInfo.AddEventHandler(objectReference, eventDelegate);
                
                signalWrapper.RequestDisconnect += () => {
                    eventInfo.RemoveEventHandler(objectReference, eventDelegate);
                };

                if (!staticClass) {
                    // Disconnect when the object is destroyed or the context is reset:
                    AddSignalDestroyWatcher(go, context, (contextReset) => {
                        if (!contextReset && LuauState.IsContextActive(context)) {
                            LuauPlugin.DestroySignals(context, thread, signalInstanceId);
                            LuauPlugin.UnpinThread(thread);
                        }
                        eventInfo.RemoveEventHandler(objectReference, eventDelegate);
                        InstanceIds.Remove(objectReference);
                    });
                } else {
                    // Disconnect static C# events when the associated LuauContext is reset:
                    Action<LuauContext> reset = null;
                    reset = (ctx) => {
                        if (ctx != context) return;
                        LuauCore.onResetInstance -= reset;
                        eventInfo.RemoveEventHandler(objectReference, eventDelegate);
                    };
                    LuauCore.onResetInstance += reset;
#if UNITY_EDITOR
                    // Ensure static connections get cleaned up in-editor between plays without domain reloading:
                    _staticSignalWrappers.Add(signalWrapper);
#endif
                }
            }
            return 1;
        }
        
        public static int HandleUnityEvent0(LuauContext context, IntPtr thread, object objectReference, int instanceId, ulong propNameHash, UnityEvent unityEvent) {
            var signalInstanceId = GetOrCreateId(objectReference);
            var newSignalCreated = LuauPlugin.PushSignal(context, thread, signalInstanceId, propNameHash);
            if (newSignalCreated) {
                var go = GetGameObjectFromObject(objectReference);
                if (go == null) return 0;
            
                LuauPlugin.PinThread(thread);
            
                var signalWrapper = new LuauSignalWrapper(context, thread, signalInstanceId, propNameHash);
                unityEvent.AddListener(signalWrapper.HandleEvent_0);
                signalWrapper.RequestDisconnect += () => {
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_0);
                };

                AddSignalDestroyWatcher(go, context, (contextReset) => {
                    if (!contextReset && LuauState.IsContextActive(context)) {
                        LuauPlugin.DestroySignals(context, thread, signalInstanceId);
                        LuauPlugin.UnpinThread(thread);
                    }
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_0);
                    InstanceIds.Remove(objectReference);
                });
            }
            return 1;
        }
        
        public static int HandleUnityEvent1<T0>(LuauContext context, IntPtr thread, object objectReference, int instanceId, ulong propNameHash, UnityEvent<T0> unityEvent) {
            var signalInstanceId = GetOrCreateId(objectReference);
            var newSignalCreated = LuauPlugin.PushSignal(context, thread, signalInstanceId, propNameHash);
            if (newSignalCreated) {
                var go = GetGameObjectFromObject(objectReference);
                if (go == null) return 0;
            
                LuauPlugin.PinThread(thread);
            
                var signalWrapper = new LuauSignalWrapper(context, thread, signalInstanceId, propNameHash);
                unityEvent.AddListener(signalWrapper.HandleEvent_1);
                signalWrapper.RequestDisconnect += () => {
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_1);
                };

                AddSignalDestroyWatcher(go, context, (contextReset) => {
                    if (!contextReset && LuauState.IsContextActive(context)) {
                        LuauPlugin.DestroySignals(context, thread, signalInstanceId);
                        LuauPlugin.UnpinThread(thread);
                    }
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_1);
                    InstanceIds.Remove(objectReference);
                });
            }
            return 1;
        }
        
        public static int HandleUnityEvent2<T0, T1>(LuauContext context, IntPtr thread, object objectReference, int instanceId, ulong propNameHash, UnityEvent<T0, T1> unityEvent) {
            var signalInstanceId = GetOrCreateId(objectReference);
            var newSignalCreated = LuauPlugin.PushSignal(context, thread, signalInstanceId, propNameHash);
            if (newSignalCreated) {
                var go = GetGameObjectFromObject(objectReference);
                if (go == null) return 0;
            
                LuauPlugin.PinThread(thread);
            
                var signalWrapper = new LuauSignalWrapper(context, thread, signalInstanceId, propNameHash);
                unityEvent.AddListener(signalWrapper.HandleEvent_2);
                signalWrapper.RequestDisconnect += () => {
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_2);
                };

                AddSignalDestroyWatcher(go, context, (contextReset) => {
                    if (!contextReset && LuauState.IsContextActive(context)) {
                        LuauPlugin.DestroySignals(context, thread, signalInstanceId);
                        LuauPlugin.UnpinThread(thread);
                    }
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_2);
                    InstanceIds.Remove(objectReference);
                });
            }
            return 1;
        }
        
        public static int HandleUnityEvent3<T0, T1, T2>(LuauContext context, IntPtr thread, object objectReference, int instanceId, ulong propNameHash, UnityEvent<T0, T1, T2> unityEvent) {
            var signalInstanceId = GetOrCreateId(objectReference);
            var newSignalCreated = LuauPlugin.PushSignal(context, thread, signalInstanceId, propNameHash);
            if (newSignalCreated) {
                var go = GetGameObjectFromObject(objectReference);
                if (go == null) return 0;
            
                LuauPlugin.PinThread(thread);
            
                var signalWrapper = new LuauSignalWrapper(context, thread, signalInstanceId, propNameHash);
                unityEvent.AddListener(signalWrapper.HandleEvent_3);
                signalWrapper.RequestDisconnect += () => {
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_3);
                };

                AddSignalDestroyWatcher(go, context, (contextReset) => {
                    if (!contextReset && LuauState.IsContextActive(context)) {
                        LuauPlugin.DestroySignals(context, thread, signalInstanceId);
                        LuauPlugin.UnpinThread(thread);
                    }
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_3);
                    InstanceIds.Remove(objectReference);
                });
            }
            return 1;
        }
        
        public static int HandleUnityEvent4<T0, T1, T2, T3>(LuauContext context, IntPtr thread, object objectReference, int instanceId, ulong propNameHash, UnityEvent<T0, T1, T2, T3> unityEvent) {
            var signalInstanceId = GetOrCreateId(objectReference);
            var newSignalCreated = LuauPlugin.PushSignal(context, thread, signalInstanceId, propNameHash);
            if (newSignalCreated) {
                var go = GetGameObjectFromObject(objectReference);
                if (go == null) return 0;
            
                LuauPlugin.PinThread(thread);
            
                var signalWrapper = new LuauSignalWrapper(context, thread, signalInstanceId, propNameHash);
                unityEvent.AddListener(signalWrapper.HandleEvent_4);
                signalWrapper.RequestDisconnect += () => {
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_4);
                };

                AddSignalDestroyWatcher(go, context, (contextReset) => {
                    if (!contextReset && LuauState.IsContextActive(context)) {
                        LuauPlugin.DestroySignals(context, thread, signalInstanceId);
                        LuauPlugin.UnpinThread(thread);
                    }
                    unityEvent.RemoveListener(signalWrapper.HandleEvent_4);
                    InstanceIds.Remove(objectReference);
                });
            }
            return 1;
        }
    }
}
