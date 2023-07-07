using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UEditorWindow
    {
        protected Func<bool> dg_get_hasFocus;
        private Func<int> dg_GetNumTabs;

        private FieldInfo fi_m_Parent;

        public class UDockArea
        {
            private Type dockAreaType;

            private PropertyInfo pi_selected;
            private MethodInfo mi_AddTab;

            public UDockArea()
            {
                var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
                dockAreaType = asmUnityEditor.GetType("UnityEditor.DockArea");

                Assert.IsNotNull(pi_selected = dockAreaType.GetProperty("selected", BindingFlags.Public | BindingFlags.Instance));
#if UNITY_2018_3_OR_NEWER
                Assert.IsNotNull(mi_AddTab = dockAreaType.GetMethod("AddTab", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(EditorWindow), typeof(bool) }, null));
#else
                Assert.IsNotNull(mi_AddTab = dockAreaType.GetMethod("AddTab", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(EditorWindow) }, null));
#endif
            }

            public int GetSelected(UnityEngine.Object dockArea)
            {
                if (dockArea == null) return -1;
                return (int)pi_selected.GetValue(dockArea, null);
            }
            public void SetSelected(UnityEngine.Object dockArea, int selected)
            {
                if (dockArea == null) return;
                pi_selected.SetValue(dockArea, selected, null);
            }

            public void AddTab(UnityEngine.Object dockArea, EditorWindow pane)
            {
                if (dockArea == null) return;
#if UNITY_2018_3_OR_NEWER
                mi_AddTab.Invoke(dockArea, new object[] { pane, true });
#else
                mi_AddTab.Invoke(dockArea, new object[] { pane });
#endif
            }
        }

        private UDockArea uDockArea;

        public UEditorWindow()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var editorWindowType = asmUnityEditor.GetType("UnityEditor.EditorWindow");

            Assert.IsNotNull(fi_m_Parent = editorWindowType.GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance));

            uDockArea = new UDockArea();
        }

        public virtual bool HasFocus(EditorWindow w)
        {
            if (w == null) return false;
            if (dg_get_hasFocus == null || dg_get_hasFocus.Target != (object)w)
                dg_get_hasFocus = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), w, w.GetType().GetProperty("hasFocus", BindingFlags.NonPublic | BindingFlags.Instance).GetGetMethod(true));
            return dg_get_hasFocus();
        }

        public int GetNumTabs(EditorWindow w)
        {
            if (w == null) return 0;
            if (dg_GetNumTabs == null || dg_GetNumTabs.Target != (object)w)
                dg_GetNumTabs = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), w, w.GetType().GetMethod("GetNumTabs", BindingFlags.NonPublic | BindingFlags.Instance));
            return dg_GetNumTabs();
        }

        public bool IsDockBrother(EditorWindow w1, EditorWindow w2)
        {
            return fi_m_Parent.GetValue(w1) == fi_m_Parent.GetValue(w2);
        }

        public int GetSelectedTab(EditorWindow w)
        {
            return uDockArea.GetSelected(fi_m_Parent.GetValue(w) as UnityEngine.Object);
        }
        public void SetSelectedTab(EditorWindow w, int selected)
        {
            uDockArea.SetSelected(fi_m_Parent.GetValue(w) as UnityEngine.Object, selected);
        }
        public void AddTab(EditorWindow w, EditorWindow pane)
        {
            uDockArea.AddTab(fi_m_Parent.GetValue(w) as UnityEngine.Object, pane);
        }
    }
}
