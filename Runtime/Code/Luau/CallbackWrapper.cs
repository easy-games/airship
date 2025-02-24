using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Luau
{
    public class CallbackWrapper {
        public LuauContext context;
        public int handle;
        public int luauRef;
        public IntPtr thread;
        public string methodName;
        /// <summary>
        /// If true we will not send an event if the first variable (context) doesn't match the creation context
        /// </summary>
        public bool validateContext;
        public delegate void EventHandler();

        private static Dictionary<IntPtr, int> m_threadPinCount = new Dictionary<IntPtr, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            m_threadPinCount.Clear();
        }

        public CallbackWrapper(LuauContext context, IntPtr thread, string methodName, int handle, bool validateContext) {
            this.context = context;
            this.thread = thread;
            this.methodName = methodName;
            this.handle = handle;
            this.validateContext = validateContext;

            LuauPluginRaw.PushThread(thread);
            this.luauRef = LuauPluginRaw.Ref(thread, -1);
            LuauPluginRaw.Pop(thread, 1);

            m_threadPinCount.TryAdd(this.thread, 0);
            m_threadPinCount[this.thread] += 1;
        }

        //If this object is destroyed, decrement the threadReferenceCount
        // ~CallbackWrapper() {
        public void Destroy() {
            m_threadPinCount[thread] -= 1;

            LuauPluginRaw.Unref(thread, luauRef);
            
            if (m_threadPinCount[thread] <= 0)
            {
                m_threadPinCount.Remove(thread);
                LuauPlugin.LuauUnpinThread(thread);
                // Debug.Log("Releasing pin " + m_name);
            }
            
        }

        static void WritePropertyToThread(IntPtr thread, object parameter)
        {
            if (parameter == null)
            {
                LuauCore.WritePropertyToThread(thread, null, null);
            }
            else
            {
                LuauCore.WritePropertyToThread(thread, parameter, parameter.GetType());
            }
        }


        unsafe public void HandleEventDelayed0()
        {
            int numParameters = 0;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {
                if (thread.m_error) return;

                Profiler.BeginSample("HandleEventDelayed0 " + this.methodName);
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
                Profiler.EndSample();
            }
        }

        private bool IsBlockedByInvalidContext(object param0) {
            if (!validateContext) return false;
            if (param0 is null) return false;
            if (param0 is not LuauContext lc) return false;
            var isBlocked = lc != context;
            // if (isBlocked) Debug.Log("Blocked by invalid context: lc=" + lc + " context=" + context + " mn=" + methodName);
            return isBlocked;
        }
        
        unsafe public void HandleEventDelayed1(object param0) {
            if (IsBlockedByInvalidContext(param0)) return;
            
            int numParameters = 1;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {
                if (thread.m_error) return;

                Profiler.BeginSample("EngineEvent." + this.methodName);
                WritePropertyToThread(this.thread, param0);
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
                Profiler.EndSample();
            }
        }


        unsafe public void HandleEventDelayed2(object param0, object param1) {
            if (IsBlockedByInvalidContext(param0)) return;
            
            int numParameters = 2;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {
                if (thread.m_error)
                {
                    return;
                }

                Profiler.BeginSample("EngineEvent." + this.methodName);
                WritePropertyToThread(this.thread, param0);
                WritePropertyToThread(this.thread, param1);
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
                Profiler.EndSample();
            }
        }

        unsafe public void HandleEventDelayed3(object param0, object param1, object param2) {
            if (IsBlockedByInvalidContext(param0)) return;
            
            int numParameters = 3;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {

                if (thread.m_error) return;

                Profiler.BeginSample("EngineEvent." + this.methodName);
                WritePropertyToThread(this.thread, param0);
                WritePropertyToThread(this.thread, param1);
                WritePropertyToThread(this.thread, param2);
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
                Profiler.EndSample();
            }
        }

        unsafe public void HandleEventDelayed4(object param0, object param1, object param2, object param3) {
            if (IsBlockedByInvalidContext(param0)) return;
            
            int numParameters = 4;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {

                if (thread.m_error) return;

                Profiler.BeginSample("EngineEvent." + this.methodName);
                WritePropertyToThread(this.thread, param0);
                WritePropertyToThread(this.thread, param1);
                WritePropertyToThread(this.thread, param2);
                WritePropertyToThread(this.thread, param3);
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
                Profiler.EndSample();
            }
        }
    }
}
