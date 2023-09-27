using System;
using System.Collections.Generic;
using UnityEngine.Profiling;

namespace Luau
{
    public class CallbackWrapper
    {
        public int m_handle;
        public IntPtr m_thread;
        public string m_name;
        public delegate void EventHandler();

        private static Dictionary<IntPtr, int> m_threadPinCount = new Dictionary<IntPtr, int>();

        public CallbackWrapper(IntPtr thread, string methodName, int handle)
        {
            m_thread = thread;
            m_name = methodName;
            m_handle = handle;

            if (m_threadPinCount.ContainsKey(m_thread) == false)
            {
                m_threadPinCount.Add(m_thread, 0);
            }
            m_threadPinCount[m_thread] += 1;
        }

        //If this object is destroyed, decrement the threadReferenceCount
        ~CallbackWrapper()
        {
            
            m_threadPinCount[m_thread] -= 1;
            
            if (m_threadPinCount[m_thread] <= 0)
            {
                m_threadPinCount.Remove(m_thread);
                LuauPlugin.LuauUnpinThread(m_thread);
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
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(m_thread);
            if (thread != null)
            {
                if (thread.m_error) return;

                Profiler.BeginSample("HandleEventDelayed0");
                System.Int32 integer = (System.Int32)m_handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(m_thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(m_thread);
                }
                Profiler.EndSample();
            }
        }

        unsafe public void HandleEventDelayed1(object param0)
        {
            int numParameters = 1;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(m_thread);
            if (thread != null)
            {
                if (thread.m_error) return;

                Profiler.BeginSample("HandleEventDelayed1");
                WritePropertyToThread(m_thread, param0);
                System.Int32 integer = (System.Int32)m_handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(m_thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(m_thread);
                }
                Profiler.EndSample();
            }
        }


        unsafe public void HandleEventDelayed2(object param0, object param1)
        {
            int numParameters = 2;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(m_thread);
            if (thread != null)
            {
                if (thread.m_error)
                {
                    return;
                }

                Profiler.BeginSample("HandleEventDelayed2");
                WritePropertyToThread(m_thread, param0);
                WritePropertyToThread(m_thread, param1);
                System.Int32 integer = (System.Int32)m_handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(m_thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(m_thread);
                }
                Profiler.EndSample();
            }
        }

        unsafe public void HandleEventDelayed3(object param0, object param1, object param2)
        {
            int numParameters = 3;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(m_thread);
            if (thread != null)
            {

                if (thread.m_error) return;

                Profiler.BeginSample("HandleEventDelayed3");
                WritePropertyToThread(m_thread, param0);
                WritePropertyToThread(m_thread, param1);
                WritePropertyToThread(m_thread, param2);
                System.Int32 integer = (System.Int32)m_handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(m_thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(m_thread);
                }
                Profiler.EndSample();
            }
        }

        unsafe public void HandleEventDelayed4(object param0, object param1, object param2, object param3)
        {
            int numParameters = 4;
            ThreadData thread = ThreadDataManager.GetThreadDataByPointer(m_thread);
            if (thread != null)
            {

                if (thread.m_error) return;

                Profiler.BeginSample("HandleEventDelayed4");
                WritePropertyToThread(m_thread, param0);
                WritePropertyToThread(m_thread, param1);
                WritePropertyToThread(m_thread, param2);
                WritePropertyToThread(m_thread, param3);
                System.Int32 integer = (System.Int32)m_handle;
                int retValue = LuauPlugin.LuauCallMethodOnThread(m_thread, new IntPtr(value: &integer), 0, numParameters);
                if (retValue < 0)
                {
                    ThreadDataManager.Error(m_thread);
                }
                Profiler.EndSample();
            }
        }
    }
}
