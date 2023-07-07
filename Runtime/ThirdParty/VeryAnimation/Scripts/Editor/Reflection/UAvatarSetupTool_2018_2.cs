#if UNITY_2018_2_OR_NEWER
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace VeryAnimation
{
    public class UAvatarSetupTool_2018_2 : UAvatarSetupTool
    {
        public UAvatarSetupTool_2018_2()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var avatarSetupToolType = asmUnityEditor.GetType("UnityEditor.AvatarSetupTool");            
            Assert.IsNotNull(m_GetHumanBones = avatarSetupToolType.GetMethod("GetHumanBones", new Type[] { typeof(SerializedProperty), typeof(Dictionary<Transform, bool>) }));
        }

        public override bool SampleTPose(GameObject go)
        {
            try
            {
                var modelBones = m_GetModelBones.Invoke(null, new object[] { go.transform, false, null });
                if (modelBones == null)
                    return false;

                object humanBoneArray = null;
                {
                    var animator = go.GetComponent<Animator>();
                    if (animator == null || animator.avatar == null)
                        return false;
                    var assetPath = EditorCommon.GetAssetPath(animator.avatar);
                    var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                    if (importer != null)
                    {
                        var so = new SerializedObject(importer);
                        humanBoneArray = so.FindProperty("m_HumanDescription.m_Human");
                        if (humanBoneArray == null)
                            return false;
                    }
                    else
                    {
                        var so = new SerializedObject(animator.avatar);
                        humanBoneArray = so.FindProperty("m_HumanDescription.m_Human");
                        if (humanBoneArray == null)
                            return false;
                    }
                }

                var bones = m_GetHumanBones.Invoke(null, new object[] { humanBoneArray, modelBones });
                if (bones == null)
                    return false;

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
#endif