using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Luau {
    public class LuauSignalWrapper {
        [DisallowMultipleComponent]
        internal class LuauSignalDestroyWatcher : MonoBehaviour {
            internal Action DestroyCallback;
            private void OnDestroy() {
                DestroyCallback.Invoke();
            }
        }

        internal event Action RequestDisconnect;
        
        private readonly LuauContext _context;
        private readonly IntPtr _thread;
        private readonly int _instanceId;
        private readonly ulong _propNameHash;
        
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
        
        public void HandleEvent_1(object p1) {
            HandleEvent(p1);
        }
        
        public void HandleEvent_2(object p1, object p2) {
            HandleEvent(p1, p2);
        }
        
        public void HandleEvent_3(object p1, object p2, object p3) {
            HandleEvent(p1, p2, p3);
        }
        
        public void HandleEvent_4(object p1, object p2, object p3, object p4) {
            HandleEvent(p1, p2, p3, p4);
        }

        private void HandleEvent(params object[] p) {
            Profiler.BeginSample("HandleCSToLuauSignalEvent");
            
            // var threadData = ThreadDataManager.GetThreadDataByPointer(_thread);
            // if (threadData != null && !threadData.m_error) {
                foreach (var param in p) {
                    WritePropertyToThread(_thread, param);
                }

                var alive = LuauPlugin.LuauEmitSignal(_context, _thread, _instanceId, _propNameHash, p.Length);
                if (!alive) {
                    RequestDisconnect?.Invoke();
                }
            // }
            
            Profiler.EndSample();
        }

        public void Destroy() {
            
        }
    }
}
