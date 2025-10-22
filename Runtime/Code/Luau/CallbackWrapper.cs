using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace Luau
{
    public class CallbackWrapper {
        /// <summary>
        /// The LuauContext that this callback was created in (and will fire in). This is only relevant if
        /// validateContext is true (used for stuff like network events where we don't want protected broadcasts
        /// to be listened to in game context).
        /// </summary>
        public LuauContext callbackContext;
        public int handle;
        public int luauRef;
        public IntPtr thread;
        public string methodName;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private string profilerTagName;
#endif
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
            this.callbackContext = context;
            this.thread = thread;
            this.methodName = methodName;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            profilerTagName = $"EngineEvent.{methodName}";
#endif
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
                LuauPlugin.UnpinThread(thread);
                // Debug.Log("Releasing pin " + m_name);
            }
            
        }


        unsafe public void HandleEventDelayed0()
        {
            int numParameters = 0;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {
                if (thread.m_error) return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.BeginSample(profilerTagName);
#endif
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.CallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.EndSample();
#endif
            }
        }

        private bool IsBlockedByInvalidContext(LuauContext callContext) {
            if (!validateContext) return false;
            return callContext != callbackContext;
        }
        
        unsafe public void HandleEventDelayed1<A>(A param0) {
            if (typeof(A) == typeof(LuauContext) && IsBlockedByInvalidContext(UnsafeUtility.As<A, LuauContext>(ref param0))) return;
            
            int numParameters = 1;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {
                if (thread.m_error) return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.BeginSample(profilerTagName);
#endif
                Profiler.BeginSample("WriteProperties");
                LuauCore.WritePropertyToThread(this.thread, param0);
                Profiler.EndSample();
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.CallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.EndSample();
#endif
            }
        }


        unsafe public void HandleEventDelayed2<A, B>(A param0, B param1) {
            if (typeof(A) == typeof(LuauContext) && IsBlockedByInvalidContext(UnsafeUtility.As<A, LuauContext>(ref param0))) return;
            
            int numParameters = 2;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {
                if (thread.m_error)
                {
                    return;
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.BeginSample(profilerTagName);
#endif
                Profiler.BeginSample("WriteProperties");
                LuauCore.WritePropertyToThread<A>(this.thread, param0);
                LuauCore.WritePropertyToThread<B>(this.thread, param1);
                Profiler.EndSample();
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.CallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.EndSample();
#endif
            }
        }

        unsafe public void HandleEventDelayed3<A, B, C>(A param0, B param1, C param2) {
            if (typeof(A) == typeof(LuauContext) && IsBlockedByInvalidContext(UnsafeUtility.As<A, LuauContext>(ref param0))) return;
            
            int numParameters = 3;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {

                if (thread.m_error) return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.BeginSample(profilerTagName);
#endif
                Profiler.BeginSample("WriteProperties");
                LuauCore.WritePropertyToThread<A>(this.thread, param0);
                LuauCore.WritePropertyToThread<B>(this.thread, param1);
                LuauCore.WritePropertyToThread<C>(this.thread, param2);
                Profiler.EndSample();
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.CallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.EndSample();
#endif
            }
        }

        unsafe public void HandleEventDelayed4<A, B, C, D>(A param0, B param1, C param2, D param3) {
            if (typeof(A) == typeof(LuauContext) && IsBlockedByInvalidContext(UnsafeUtility.As<A, LuauContext>(ref param0))) return;
            
            int numParameters = 4;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(this.thread);
            if (thread != null)
            {

                if (thread.m_error) return;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.BeginSample(profilerTagName);
#endif
                Profiler.BeginSample("WriteProperties");
                LuauCore.WritePropertyToThread<A>(this.thread, param0);
                LuauCore.WritePropertyToThread<B>(this.thread, param1);
                LuauCore.WritePropertyToThread<C>(this.thread, param2);
                LuauCore.WritePropertyToThread<D>(this.thread, param3);
                Profiler.EndSample();
                System.Int32 integer = (System.Int32)handle;
                int retValue = LuauPlugin.CallMethodOnThread(this.thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(this.thread);
                }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Profiler.EndSample();
#endif
            }
        }
    }
}
