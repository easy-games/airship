using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class USceneView
    {
        protected PropertyInfo pi_viewIsLockedToObject;
#if !UNITY_2019_1_OR_NEWER
        protected FieldInfo fi_onPreSceneGUIDelegate;
#endif
        public USceneView()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var sceneViewType = asmUnityEditor.GetType("UnityEditor.SceneView");
            Assert.IsNotNull(pi_viewIsLockedToObject = sceneViewType.GetProperty("viewIsLockedToObject", BindingFlags.NonPublic | BindingFlags.Instance));
#if !UNITY_2019_1_OR_NEWER
            Assert.IsNotNull(fi_onPreSceneGUIDelegate = sceneViewType.GetField("onPreSceneGUIDelegate", BindingFlags.NonPublic | BindingFlags.Static));
#endif
        }

#if !UNITY_2019_1_OR_NEWER
        public void SetOnPreSceneGUIDelegate(SceneView.OnSceneFunc del)
        {
            fi_onPreSceneGUIDelegate.SetValue(null, del);
        }
        public SceneView.OnSceneFunc GetOnPreSceneGUIDelegate()
        {
            return (SceneView.OnSceneFunc)fi_onPreSceneGUIDelegate.GetValue(null);
        }
#endif

        public void SetViewIsLockedToObject(SceneView instance, bool flag)
        {
            pi_viewIsLockedToObject.SetValue(instance, flag, null);
        }

        public bool Frame(SceneView instance, Bounds bounds, bool instant = true)
        {
            return instance.Frame(bounds, instant);
        }
    }
}
