using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UAnimationClipEditor
    {
        public Editor instance { get; private set; }

        private MethodInfo mi_ClipRangeGUI;
        private MethodInfo mi_InitClipTime;
        private FieldInfo fi_m_AvatarPreview;

        public UAnimationClipEditor(AnimationClip clip, UAvatarPreview avatarPreview)
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var animationClipEditorType = asmUnityEditor.GetType("UnityEditor.AnimationClipEditor");
            Assert.IsNotNull(mi_ClipRangeGUI = animationClipEditorType.GetMethod("ClipRangeGUI"));
            mi_InitClipTime = animationClipEditorType.GetMethod("InitClipTime");    //Unity2020.2
            Assert.IsNotNull(fi_m_AvatarPreview = animationClipEditorType.GetField("m_AvatarPreview", BindingFlags.NonPublic | BindingFlags.Instance));

            instance = Editor.CreateEditor(clip, animationClipEditorType);
            fi_m_AvatarPreview.SetValue(instance, avatarPreview.instance);
            if (mi_InitClipTime != null)
                mi_InitClipTime.Invoke(instance, null);
        }
        ~UAnimationClipEditor()
        {
            if (instance != null)
            {
                EditorApplication.delayCall += () =>
                {
                    Editor.DestroyImmediate(instance);
                };
            }
        }
        public void Release()
        {
            if (instance == null) return;
            fi_m_AvatarPreview.SetValue(instance, null);
            Editor.DestroyImmediate(instance);
            instance = null;
        }

        public void ClipRangeGUI(ref float startFrame, ref float stopFrame, out bool changedStart, out bool changedStop, bool showAdditivePoseFrame, ref float additivePoseframe, out bool changedAdditivePoseframe)
        {
            changedStart = false;
            changedStop = false;
            changedAdditivePoseframe = false;
            var objects = new object[] { startFrame, stopFrame, changedStart, changedStop, showAdditivePoseFrame, additivePoseframe, changedAdditivePoseframe };
            mi_ClipRangeGUI.Invoke(instance, objects);
            startFrame = (float)objects[0];
            stopFrame = (float)objects[1];
            changedStart = (bool)objects[2];
            changedStop = (bool)objects[3];
            additivePoseframe = (float)objects[5];
            changedAdditivePoseframe = (bool)objects[6];
        }
    }
}
