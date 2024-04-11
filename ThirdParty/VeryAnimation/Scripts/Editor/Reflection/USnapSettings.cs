using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class USnapSettings
    {
#if !UNITY_2019_3_OR_NEWER
        private Func<Vector3> dg_get_move;
        private Func<float> dg_get_scale;
        private Func<float> dg_get_rotation;
#endif

        public USnapSettings()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
#if !UNITY_2019_3_OR_NEWER
            var snapSettingsType = asmUnityEditor.GetType("UnityEditor.SnapSettings");
            Assert.IsNotNull(dg_get_move = (Func<Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3>), null, snapSettingsType.GetProperty("move", BindingFlags.Public | BindingFlags.Static).GetGetMethod()));
            Assert.IsNotNull(dg_get_scale = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), null, snapSettingsType.GetProperty("scale", BindingFlags.Public | BindingFlags.Static).GetGetMethod()));
            Assert.IsNotNull(dg_get_rotation = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), null, snapSettingsType.GetProperty("rotation", BindingFlags.Public | BindingFlags.Static).GetGetMethod()));
#endif
        }

#if !UNITY_2019_3_OR_NEWER
        public Vector3 move { get { return dg_get_move(); } }
        public float scale { get { return dg_get_scale(); } }
        public float rotation { get { return dg_get_rotation(); }  }
#else
        public Vector3 move { get { return EditorSnapSettings.move; } }
        public float scale { get { return EditorSnapSettings.scale; } }
        public float rotation { get { return EditorSnapSettings.rotate; } }
#endif
    }
}
