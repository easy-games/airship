﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Luau
{
    public class CallbackWrapper {
        public LuauContext context;
        public int handle;
        public IntPtr thread;
        public string methodName;
        public delegate void EventHandler();

        private static Dictionary<IntPtr, int> m_threadPinCount = new Dictionary<IntPtr, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            m_threadPinCount.Clear();
        }

        public CallbackWrapper(LuauContext context, IntPtr thread, string methodName, int handle) {
            this.context = context;
            this.thread = thread;
            this.methodName = methodName;
            this.handle = handle;

            m_threadPinCount.TryAdd(this.thread, 0);
            m_threadPinCount[this.thread] += 1;
        }

        //If this object is destroyed, decrement the threadReferenceCount
        // ~CallbackWrapper() {
        public void Destroy() {
            m_threadPinCount[thread] -= 1;
            
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

        unsafe public void HandleEventDelayed1(object param0)
        {
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


        unsafe public void HandleEventDelayed2(object param0, object param1)
        {
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

        unsafe public void HandleEventDelayed3(object param0, object param1, object param2)
        {
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

        unsafe public void HandleEventDelayed4(object param0, object param1, object param2, object param3)
        {
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
