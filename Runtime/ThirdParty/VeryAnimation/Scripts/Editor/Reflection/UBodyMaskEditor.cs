using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace VeryAnimation
{
    public class UBodyMaskEditor
    {
        private Func<object, Color[]> dg_get_m_MaskBodyPartPicker;

        public UBodyMaskEditor()
        {
            var asmUnityEditor = Assembly.LoadFrom(InternalEditorUtility.GetEditorAssemblyPath());
            var bodyMaskEditorType = asmUnityEditor.GetType("UnityEditor.BodyMaskEditor");
            {
                var fi_m_MaskBodyPartPicker = bodyMaskEditorType.GetField("m_MaskBodyPartPicker", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(dg_get_m_MaskBodyPartPicker = EditorCommon.CreateGetFieldDelegate<Color[]>(fi_m_MaskBodyPartPicker));
            }
        }

        public Color[] GetMaskBodyPartPicker()
        {
            return dg_get_m_MaskBodyPartPicker(null);
        }
    }
}
