using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class BlendShapeWeightSave
    {
        private GameObject rootObject;
        private Dictionary<string, SkinnedMeshRenderer> renderers;

        private class SaveData
        {
            public Dictionary<string, float> values;
        }

        private Dictionary<SkinnedMeshRenderer, SaveData> originalValues;
        private Dictionary<SkinnedMeshRenderer, SaveData> prefabValues;

        public BlendShapeWeightSave(GameObject gameObject)
        {
            rootObject = gameObject;

            renderers = new Dictionary<string, SkinnedMeshRenderer>();
            {
                originalValues = new Dictionary<SkinnedMeshRenderer, SaveData>();
                foreach (var renderer in rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0)
                        continue;
                    var save = new SaveData()
                    {
                        values = new Dictionary<string, float>(),
                    };
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        var name = renderer.sharedMesh.GetBlendShapeName(i);
                        if (!save.values.ContainsKey(name))
                            save.values.Add(name, renderer.GetBlendShapeWeight(i));
                    }
                    originalValues.Add(renderer, save);
                    var path = AnimationUtility.CalculateTransformPath(renderer.transform, rootObject.transform);
                    if (!renderers.ContainsKey(path))
                    {
                        renderers.Add(path, renderer);
                    }
                }
            }
        }
        public void CreateExtraValues()
        {
            {
                prefabValues = new Dictionary<SkinnedMeshRenderer, SaveData>();
#if UNITY_2018_2_OR_NEWER
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(rootObject) as GameObject;
#else
                var prefab = PrefabUtility.GetPrefabParent(rootObject) as GameObject;
#endif
                if (prefab != null)
                {
                    var go = GameObject.Instantiate<GameObject>(prefab);
                    AnimatorUtility.DeoptimizeTransformHierarchy(go);
                    go.hideFlags |= HideFlags.HideAndDontSave;
                    {
                        foreach (var renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            if (renderer == null || renderer.sharedMesh == null || renderer.sharedMesh.blendShapeCount == 0)
                                continue;
                            var save = new SaveData()
                            {
                                values = new Dictionary<string, float>(),
                            };
                            for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                            {
                                var name = renderer.sharedMesh.GetBlendShapeName(i);
                                if (!save.values.ContainsKey(name))
                                    save.values.Add(name, renderer.GetBlendShapeWeight(i));
                            }
                            var path = AnimationUtility.CalculateTransformPath(renderer.transform, prefab.transform);
                            SkinnedMeshRenderer originalRenderer;
                            if (renderers.TryGetValue(path, out originalRenderer))
                            {
                                prefabValues.Add(originalRenderer, save);
                            }
                        }
                    }
                    GameObject.DestroyImmediate(go);
                }
            }
        }

        public bool ResetDefaultWeight()
        {
            if (ResetPrefabWeight()) return true;
            if (ResetOriginalWeight()) return true;
            return false;
        }
        public float GetDefaultWeight(SkinnedMeshRenderer renderer, string name)
        {
            if(IsHavePrefabWeight(renderer, name))
                return GetPrefabWeight(renderer, name);
            else
                return GetOriginalWeight(renderer, name);
        }

        public bool IsEnableOriginalWeight()
        {
            return originalValues != null;
        }
        public bool ResetOriginalWeight()
        {
            foreach (var pair in originalValues)
            {
                if (pair.Key == null || pair.Key.sharedMesh == null) continue;
                var renderer = pair.Key;
                var mesh = renderer.sharedMesh;
                foreach (var valuePair in pair.Value.values)
                {
                    var index = mesh.GetBlendShapeIndex(valuePair.Key);
                    if (index < 0 || index >= mesh.blendShapeCount) continue;
                    if (renderer.GetBlendShapeWeight(index) != valuePair.Value)
                    {
                        renderer.SetBlendShapeWeight(index, valuePair.Value);
                    }
                }
            }
            return true;
        }
        public bool IsHaveOriginalWeight(SkinnedMeshRenderer renderer, string name)
        {
            if (!originalValues.ContainsKey(renderer)) return false;
            return originalValues[renderer].values.ContainsKey(name);
        }
        public float GetOriginalWeight(SkinnedMeshRenderer renderer, string name)
        {
            if (!originalValues.ContainsKey(renderer)) return 0f;
            float weight = 0f;
            if (!originalValues[renderer].values.TryGetValue(name, out weight)) return 0f;
            return weight;
        }
        public void ActionOriginalWeights(SkinnedMeshRenderer renderer, Action<string, float> action)
        {
            if (!originalValues.ContainsKey(renderer)) return;
            foreach (var pair in originalValues[renderer].values)
            {
                action(pair.Key, pair.Value);
            }
        }

        public bool IsEnablePrefabWeight()
        {
            return prefabValues != null;
        }
        public bool ResetPrefabWeight()
        {
            if (prefabValues == null) return false;
            foreach (var pair in prefabValues)
            {
                if (pair.Key == null || pair.Key.sharedMesh == null) continue;
                foreach (var valuePair in pair.Value.values)
                {
                    var index = pair.Key.sharedMesh.GetBlendShapeIndex(valuePair.Key);
                    if (index < 0 || index >= pair.Key.sharedMesh.blendShapeCount) continue;
                    if (pair.Key.GetBlendShapeWeight(index) != valuePair.Value)
                    {
                        pair.Key.SetBlendShapeWeight(index, valuePair.Value);
                    }
                }
            }
            return true;
        }
        public bool IsHavePrefabWeight(SkinnedMeshRenderer renderer, string name)
        {
            if (prefabValues == null || !prefabValues.ContainsKey(renderer)) return false;
            return prefabValues[renderer].values.ContainsKey(name);
        }
        public float GetPrefabWeight(SkinnedMeshRenderer renderer, string name)
        {
            if (prefabValues == null || !prefabValues.ContainsKey(renderer)) return 0f;
            float weight = 0f;
            if (!prefabValues[renderer].values.TryGetValue(name, out weight)) return 0f;
            return weight;
        }
    }
}
