using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UTimeControl
    {
        public object instance { get; private set; }

        private Func<object, float> dg_get_currentTime;
        private Action<object, float> dg_set_currentTime;
        private Action<float> dg_set_nextCurrentTime;
        private Func<object, float> dg_get_startTime;
        private Action<object, float> dg_set_startTime;
        private Func<object, float> dg_get_stopTime;
        private Action<object, float> dg_set_stopTime;
        private Func<object, bool> dg_get_loop;
        private Action<object, bool> dg_set_loop;
        private Action dg_Update;
        private Func<float> dg_get_deltaTime;
        private Action<float> dg_set_deltaTime;
        private Func<object, bool> dg_get_m_DeltaTimeSet;
        private Func<bool> dg_get_playing;
        private Action<bool> dg_set_playing;

        public UTimeControl(object instance)
        {
            this.instance = instance;

            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var timeControlType = asmUnityEditor.GetType("UnityEditor.TimeControl");

            {
                var fi_currentTime = timeControlType.GetField("currentTime");
                dg_get_currentTime = EditorCommon.CreateGetFieldDelegate<float>(fi_currentTime);
                dg_set_currentTime = EditorCommon.CreateSetFieldDelegate<float>(fi_currentTime);
            }
            {
                var pi_nextCurrentTime = timeControlType.GetProperty("nextCurrentTime");
                dg_set_nextCurrentTime = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), instance, pi_nextCurrentTime.GetSetMethod());
            }
            {
                var fi_startTime = timeControlType.GetField("startTime");
                dg_get_startTime = EditorCommon.CreateGetFieldDelegate<float>(fi_startTime);
                dg_set_startTime = EditorCommon.CreateSetFieldDelegate<float>(fi_startTime);
            }
            {
                var fi_stopTime = timeControlType.GetField("stopTime");
                dg_get_stopTime = EditorCommon.CreateGetFieldDelegate<float>(fi_stopTime);
                dg_set_stopTime = EditorCommon.CreateSetFieldDelegate<float>(fi_stopTime);
            }
            {
                var fi_loop = timeControlType.GetField("loop");
                dg_get_loop = EditorCommon.CreateGetFieldDelegate<bool>(fi_loop);
                dg_set_loop = EditorCommon.CreateSetFieldDelegate<bool>(fi_loop);
            }

            dg_Update = (Action)Delegate.CreateDelegate(typeof(Action), instance, timeControlType.GetMethod("Update"));
            {
                var pi_deltaTime = timeControlType.GetProperty("deltaTime");
                dg_get_deltaTime = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), instance, pi_deltaTime.GetGetMethod());
                dg_set_deltaTime = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), instance, pi_deltaTime.GetSetMethod());
            }
            {
                var fi_m_DeltaTimeSet = timeControlType.GetField("m_DeltaTimeSet", BindingFlags.NonPublic | BindingFlags.Instance);
                dg_get_m_DeltaTimeSet = EditorCommon.CreateGetFieldDelegate<bool>(fi_m_DeltaTimeSet);
            }
            {
                var pi_playing = timeControlType.GetProperty("playing");
                dg_get_playing = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), instance, pi_playing.GetGetMethod());
                dg_set_playing = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), instance, pi_playing.GetSetMethod());
            }
        }

        public void Update()
        {
            dg_Update();
        }

        public float currentTime
        {
            get
            {
                return dg_get_currentTime(instance);
            }
            set
            {
                dg_set_currentTime(instance, value);
            }
        }
        public float nextCurrentTime
        {
            set
            {
                dg_set_nextCurrentTime(value);
            }
        }
        public float startTime
        {
            get
            {
                return dg_get_startTime(instance);
            }
            set
            {
                dg_set_startTime(instance, value);
            }
        }
        public float stopTime
        {
            get
            {
                return dg_get_stopTime(instance);
            }
            set
            {
                dg_set_stopTime(instance, value);
            }
        }
        public bool loop
        {
            get
            {
                return dg_get_loop(instance);
            }
            set
            {
                dg_set_loop(instance, value);
            }
        }
        public float deltaTime
        {
            get
            {
                return dg_get_deltaTime();
            }
            set
            {
                dg_set_deltaTime(value);
            }
        }
        public bool GetDeltaTimeSet()
        {
            return dg_get_m_DeltaTimeSet(instance);
        }
        public bool playing
        {
            get
            {
                return dg_get_playing();
            }
            set
            {
                dg_set_playing(value);
            }
        }
    }
}
