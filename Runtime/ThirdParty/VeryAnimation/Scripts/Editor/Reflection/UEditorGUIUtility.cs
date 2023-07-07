using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UEditorGUIUtility
    {
        private Func<string, Texture2D> m_LoadIcon;
        private Func<MessageType, Texture2D> m_GetHelpIcon;
        private Func<object, int> m_get_s_LastControlID;

        public UEditorGUIUtility()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var editorGUIUtilityType = asmUnityEditor.GetType("UnityEditor.EditorGUIUtility");

            Assert.IsNotNull(m_LoadIcon = (Func<string, Texture2D>)Delegate.CreateDelegate(typeof(Func<string, Texture2D>), null, editorGUIUtilityType.GetMethod("LoadIcon", BindingFlags.Static | BindingFlags.NonPublic)));
            Assert.IsNotNull(m_GetHelpIcon = (Func<MessageType, Texture2D>)Delegate.CreateDelegate(typeof(Func<MessageType, Texture2D>), null, editorGUIUtilityType.GetMethod("GetHelpIcon", BindingFlags.Static | BindingFlags.NonPublic)));
            Assert.IsNotNull(m_get_s_LastControlID = EditorCommon.CreateGetFieldDelegate<int>(editorGUIUtilityType.GetField("s_LastControlID", BindingFlags.NonPublic | BindingFlags.Static)));
        }

        public Texture2D LoadIcon(string name)
        {
            return m_LoadIcon(name);
        }

        public Texture2D GetHelpIcon(MessageType type)
        {
            return m_GetHelpIcon(type);
        }

        public int GetLastControlID()
        {
            return m_get_s_LastControlID(null);
        }
    }
}
