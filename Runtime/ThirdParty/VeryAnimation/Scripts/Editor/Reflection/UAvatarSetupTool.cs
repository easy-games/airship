using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace VeryAnimation
{
    public class UAvatarSetupTool
    {
        protected MethodInfo m_SampleBindPose;
        protected MethodInfo m_GetModelBones;
        protected MethodInfo m_GetHumanBones;
        protected MethodInfo m_MakePoseValid;

        public UAvatarSetupTool()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var avatarSetupToolType = asmUnityEditor.GetType("UnityEditor.AvatarSetupTool");
            Assert.IsNotNull(m_SampleBindPose = avatarSetupToolType.GetMethod("SampleBindPose", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(m_GetModelBones = avatarSetupToolType.GetMethod("GetModelBones", BindingFlags.Public | BindingFlags.Static));
            m_GetHumanBones = avatarSetupToolType.GetMethod("GetHumanBones", new Type[] { typeof(SerializedObject), typeof(Dictionary<Transform, bool>) });
            Assert.IsNotNull(m_MakePoseValid = avatarSetupToolType.GetMethod("MakePoseValid", BindingFlags.Public | BindingFlags.Static));
        }

        public bool SampleBindPose(GameObject go)
        {
            foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer == null || renderer.sharedMesh == null || renderer.bones == null)
                    return false;
                else if (renderer.bones.Length != renderer.sharedMesh.bindposes.Length)
                {
                    Debug.LogErrorFormat(Language.GetText(Language.Help.LogSampleBindPoseBoneLengthError), renderer.name, renderer.sharedMesh.name);
                    return false;
                }
                {
                    var index = ArrayUtility.IndexOf(renderer.bones, null);
                    if (index >= 0)
                    {
                        Debug.LogErrorFormat(Language.GetText(Language.Help.LogSampleBindPoseBoneNullError), renderer.name, index);
                        return false;
                    }
                }
            }
            try
            {
                m_SampleBindPose.Invoke(null, new object[] { go });
            }
            catch
            {
                Debug.LogError(Language.GetText(Language.Help.LogSampleBindPoseUnknownError));
                return false;
            }
            return true;
        }
        public virtual bool SampleTPose(GameObject go)
        {
            try
            {
                var modelBones = m_GetModelBones.Invoke(null, new object[] { go.transform, false, null });
                if (modelBones == null)
                    return false;

                object bones = null;
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator == null || animator.avatar == null)
                        return false;
                    var assetPath = EditorCommon.GetAssetPath(animator.avatar);
                    var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (importer == null)
                        return false;
                    var so = new SerializedObject(importer);
                    bones = m_GetHumanBones.Invoke(null, new object[] { so, modelBones });
                    if (bones == null)
                        return false;
                }

                m_MakePoseValid.Invoke(null, new object[] { bones });
            }
            catch
            {
                Debug.LogError(Language.GetText(Language.Help.LogSampleTPoseUnknownError));
                return false;
            }
            return true;
        }
    }
}
