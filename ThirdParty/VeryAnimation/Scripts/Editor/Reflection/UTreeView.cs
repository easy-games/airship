using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UTreeView
    {
        private Func<object, object> dg_get_m_TreeView;

        private UTreeViewController uTreeViewController;

        private class UTreeViewController
        {
            private MethodInfo mi_OffsetSelection;

            public UTreeViewController()
            {
                var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
                var treeViewControllerType = asmUnityEditor.GetType("UnityEditor.IMGUI.Controls.TreeViewController");

                Assert.IsNotNull(mi_OffsetSelection = treeViewControllerType.GetMethod("OffsetSelection", BindingFlags.Instance | BindingFlags.Public));
            }

            public void OffsetSelection(object instance, int offset)
            {
                if (instance == null) return;
                mi_OffsetSelection.Invoke(instance, new object[] { offset });
            }
        }

        public UTreeView()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var treeViewType = asmUnityEditor.GetType("UnityEditor.IMGUI.Controls.TreeView");

            Assert.IsNotNull(dg_get_m_TreeView = EditorCommon.CreateGetFieldDelegate<object>(treeViewType.GetField("m_TreeView", BindingFlags.NonPublic | BindingFlags.Instance)));

            uTreeViewController = new UTreeViewController();
        }

        public void OffsetSelection(object instance, int offset)
        {
            if (instance == null) return;

            var treeViewController = dg_get_m_TreeView(instance);
            if (treeViewController == null) return;

            uTreeViewController.OffsetSelection(treeViewController, offset);
        }
    }
}
