using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class AnimationClipValueSave
    {
        public GameObject rootObject { get; private set; }

        public AnimationClip clip { get; private set; }

        private EditorCurveBinding[] bindings;
        private float?[] floatValues;

        private EditorCurveBinding[] refBindings;
        private UnityEngine.Object[] refValues;

        public AnimationClipValueSave(GameObject gameObject, AnimationClip clip)
        {
            this.rootObject = gameObject;
            this.clip = clip;

            bindings = AnimationUtility.GetCurveBindings(clip);
            floatValues = new float?[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                float floatValue;
                if (AnimationUtility.GetFloatValue(rootObject, bindings[i], out floatValue))
                {
                    floatValues[i] = floatValue;
                }
            }

            refBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            refValues = new UnityEngine.Object[refBindings.Length];
            for (int i = 0; i < refBindings.Length; i++)
            {
                UnityEngine.Object refValue;
                if (AnimationUtility.GetObjectReferenceValue(rootObject, refBindings[i], out refValue))
                {
                    refValues[i] = refValue;
                }
            }
        }

        public void ResetValue()
        {
            if (rootObject == null)
                return;

            if (bindings != null)
            {
                for (int i = 0; i < bindings.Length; i++)
                {
                    if (!floatValues[i].HasValue)
                        continue;

                    var t = rootObject.transform.Find(bindings[i].path);
                    if (t == null)
                        continue;

                    Component comp = null;
                    try
                    {
                        comp = t.GetComponent(bindings[i].type);
                    }
                    catch
                    {
                        continue;
                    }
                    if (comp == null)
                        continue;

                    var so = new SerializedObject(comp);
                    var sp = so.FindProperty(bindings[i].propertyName);
                    if (sp == null)
                        continue;

                    var type = AnimationUtility.GetEditorCurveValueType(rootObject, bindings[i]);
                    if (type == typeof(float))
                    {
                        sp.floatValue = floatValues[i].Value;
                    }
                    else if (type == typeof(int))
                    {
                        sp.intValue = (int)floatValues[i].Value;
                    }
                    else if (type == typeof(bool))
                    {
                        sp.boolValue = floatValues[i].Value != 0f;
                    }
                    else
                    {
                        Assert.IsTrue(false);
                        continue;
                    }

                    so.ApplyModifiedProperties();
                }
            }

            if (refBindings != null)
            {
                for (int i = 0; i < refBindings.Length; i++)
                {
                    var t = rootObject.transform.Find(refBindings[i].path);
                    if (t == null)
                        continue;

                    Component comp = null;
                    try
                    {
                        comp = t.GetComponent(refBindings[i].type);
                    }
                    catch
                    {
                        continue;
                    }
                    if (comp == null)
                        continue;

                    var so = new SerializedObject(comp);
                    var sp = so.FindProperty(refBindings[i].propertyName);
                    if (sp == null)
                        continue;

                    sp.objectReferenceValue = refValues[i];

                    so.ApplyModifiedProperties();
                }
            }
        }
    }
}
