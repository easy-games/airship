using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class TransformPoseSave
    {
        public GameObject rootObject { get; private set; }
        public Vector3 startPosition { get; private set; }
        public Quaternion startRotation { get; private set; }
        public Vector3 startScale { get; private set; }
        public Vector3 startLocalPosition { get; private set; }
        public Quaternion startLocalRotation { get; private set; }
        public Vector3 startLocalScale { get; private set; }
        public Vector3 originalPosition { get; private set; }
        public Quaternion originalRotation { get; private set; }
        public Vector3 originalScale { get; private set; }
        public Vector3 originalLocalPosition { get; private set; }
        public Quaternion originalLocalRotation { get; private set; }
        public Vector3 originalLocalScale { get; private set; }

        public Matrix4x4 startMatrix { get { return Matrix4x4.TRS(startPosition, startRotation, startScale); } }
        public Matrix4x4 originalMatrix { get { return Matrix4x4.TRS(originalPosition, originalRotation, originalScale); } }

        public class SaveData
        {
            public SaveData()
            {
            }
            public SaveData(Transform t)
            {
                Save(t);
            }
            public void Save(Transform t)
            {
                localPosition = t.localPosition;
                localRotation = t.localRotation;
                localScale = t.localScale;
                position = t.position;
                rotation = t.rotation;
                scale = t.lossyScale;
            }
            public void LoadLocal(Transform t)
            {
                t.localPosition = localPosition;
                t.localRotation = localRotation;
                t.localScale = localScale;
            }
            public void LoadWorld(Transform t)
            {
                t.SetPositionAndRotation(position, rotation);
            }
            public Matrix4x4 localMatrix { get { return Matrix4x4.TRS(localPosition, localRotation, localScale); } }
            public Matrix4x4 matrix { get { return Matrix4x4.TRS(position, rotation, scale); } }

            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        private Dictionary<Transform, SaveData> originalTransforms;
        private Dictionary<Transform, SaveData> bindTransforms;
        private Dictionary<Transform, SaveData> tposeTransforms;
        private Dictionary<Transform, SaveData> prefabTransforms;
        private Dictionary<Transform, SaveData> humanDescriptionTransforms;

        public TransformPoseSave(GameObject gameObject)
        {
            rootObject = gameObject;
            startPosition = originalPosition = gameObject.transform.position;
            startRotation = originalRotation = gameObject.transform.rotation;
            startScale = originalScale = gameObject.transform.lossyScale;
            startLocalPosition = originalLocalPosition = gameObject.transform.localPosition;
            startLocalRotation = originalLocalRotation = gameObject.transform.localRotation;
            startLocalScale = originalLocalScale = gameObject.transform.localScale;
            #region originalTransforms
            {
                originalTransforms = new Dictionary<Transform, SaveData>();
                Action<Transform, Transform> SaveTransform = null;
                SaveTransform = (t, root) =>
                {
                    if (!originalTransforms.ContainsKey(t))
                    {
                        var saveTransform = new SaveData(t);
                        originalTransforms.Add(t, saveTransform);
                    }
                    for (int i = 0; i < t.childCount; i++)
                        SaveTransform(t.GetChild(i), root);
                };
                SaveTransform(gameObject.transform, gameObject.transform);
            }
            #endregion
        }
        public void CreateExtraTransforms()
        {
            #region saveTransforms
            {
                var bindPathTransforms = new Dictionary<string, SaveData>();
                var tposePathTransforms = new Dictionary<string, SaveData>();
                var prefabPathTransforms = new Dictionary<string, SaveData>();
                var humanDescriptionPathTransforms = new Dictionary<string, SaveData>();
                var defaultPathTransforms = new Dictionary<string, SaveData>();
                {
#if UNITY_2018_2_OR_NEWER
                    var uAvatarSetupTool = new UAvatarSetupTool_2018_2();
#else
                    var uAvatarSetupTool = new UAvatarSetupTool();
#endif
                    Action<Dictionary<string, SaveData>, Transform, Transform, bool> SaveTransform = null;
                    SaveTransform = (transforms, t, root, scaleOverwrite) =>
                    {
                        var path = AnimationUtility.CalculateTransformPath(t, root);
                        if (!transforms.ContainsKey(path))
                        {
                            var saveTransform = new SaveData(t);
                            transforms.Add(path, saveTransform);
                        }
                        else if (scaleOverwrite)
                        {
                            transforms[path].localScale = t.localScale;
                            transforms[path].scale = t.lossyScale;
                        }
                        for (int i = 0; i < t.childCount; i++)
                            SaveTransform(transforms, t.GetChild(i), root, scaleOverwrite);
                    };
                    {
                        List<GameObject> goList = new List<GameObject>();
                        Action<GameObject> AddList = null;
                        AddList = (obj) =>
                        {
                            goList.Add(obj);
                            for (int i = 0; i < obj.transform.childCount; i++)
                            {
                                AddList(obj.transform.GetChild(i).gameObject);
                            }
                        };
                        Action<GameObject> GetBindPose = (go) =>
                        {
                            if (go.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                                return;

                            var goTmp = GameObject.Instantiate<GameObject>(go);
                            goTmp.hideFlags |= HideFlags.HideAndDontSave;
                            goTmp.transform.SetParent(null);
                            goTmp.transform.localPosition = Vector3.zero;
                            goTmp.transform.localRotation = Quaternion.identity;
                            goTmp.transform.localScale = Vector3.one;
                            AddList(goTmp);
                            if (uAvatarSetupTool.SampleBindPose(goTmp))
                            {
                                var rootT = goTmp.transform;
                                #region Root
                                rootT.localPosition = rootObject.transform.localPosition;
                                rootT.localRotation = rootObject.transform.localRotation;
                                rootT.localScale = rootObject.transform.localScale;
                                #endregion
                                SaveTransform(defaultPathTransforms, rootT, rootT, false);
                                SaveTransform(bindPathTransforms, rootT, rootT, false);
                            }
                            GameObject.DestroyImmediate(goTmp);
                        };
                        Action<GameObject> GetTPoseHumanDescriptionPose = (go) =>
                        {
                            var animator = go.GetComponent<Animator>();
                            if (animator == null || !animator.isHuman || animator.avatar == null)
                                return;

                            var goTmp = GameObject.Instantiate<GameObject>(go);
                            goTmp.hideFlags |= HideFlags.HideAndDontSave;
                            goTmp.transform.SetParent(null);
                            goTmp.transform.localPosition = Vector3.zero;
                            goTmp.transform.localRotation = Quaternion.identity;
                            goTmp.transform.localScale = Vector3.one;
                            AddList(goTmp);
                            if (uAvatarSetupTool.SampleBindPose(goTmp) &&   //Reset
                                uAvatarSetupTool.SampleTPose(goTmp))
                            {
                                var rootT = goTmp.transform;
                                #region Root
                                rootT.localPosition = rootObject.transform.localPosition;
                                rootT.localRotation = rootObject.transform.localRotation;
                                rootT.localScale = rootObject.transform.localScale;
                                #endregion
                                SaveTransform(defaultPathTransforms, rootT, rootT, false);
                                SaveTransform(tposePathTransforms, rootT, rootT, false);
                            }
                            if (uAvatarSetupTool.SampleBindPose(goTmp))   //Reset
                            {
                                HumanDescription? humanDescription = null;
#if UNITY_2019_1_OR_NEWER
                                humanDescription = animator.avatar.humanDescription;
#endif
                                if (!humanDescription.HasValue)
                                {
                                    var modelImporter = AssetImporter.GetAtPath(EditorCommon.GetAssetPath(animator.avatar)) as ModelImporter;
                                    if (modelImporter != null)
                                        humanDescription = modelImporter.humanDescription;
                                }
                                if (humanDescription.HasValue)
                                {
                                    var hd = humanDescription.Value;
                                    var transforms = goTmp.GetComponentsInChildren<Transform>(true);
                                    for (int i = 0; i < hd.skeleton.Length; i++)
                                    {
                                        var t = Array.Find(transforms, x => x.name == hd.skeleton[i].name);
                                        if (t == null)
                                            continue;
                                        t.localPosition = hd.skeleton[i].position;
                                        t.localRotation = hd.skeleton[i].rotation;
                                        t.localScale = hd.skeleton[i].scale;
                                    }
                                    var rootT = goTmp.transform;
                                    #region Root
                                    rootT.localPosition = rootObject.transform.localPosition;
                                    rootT.localRotation = rootObject.transform.localRotation;
                                    rootT.localScale = rootObject.transform.localScale;
                                    #endregion
                                    SaveTransform(defaultPathTransforms, rootT, rootT, false);
                                    SaveTransform(humanDescriptionPathTransforms, rootT, rootT, false);
                                }
                            }
                            GameObject.DestroyImmediate(goTmp);
                        };
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
                            AddList(go);
                            #region BindPose
                            GetBindPose(go);
                            #endregion
                            #region TPose
                            GetTPoseHumanDescriptionPose(go);
                            #endregion
                            #region PrefabPose
                            {  //Root
                                go.transform.localPosition = rootObject.transform.localPosition;
                                go.transform.localRotation = rootObject.transform.localRotation;
                                go.transform.localScale = rootObject.transform.localScale;
                            }
                            SaveTransform(defaultPathTransforms, go.transform, go.transform, true);
                            SaveTransform(prefabPathTransforms, go.transform, go.transform, false);
                            #endregion
                            GameObject.DestroyImmediate(go);
                        }
                        else
                        {
                            #region BindPose
                            GetBindPose(rootObject);
                            #endregion
                            #region TPose
                            GetTPoseHumanDescriptionPose(rootObject);
                            #endregion
                        }
                        foreach (var go in goList)
                        {
                            if (go != null)
                                GameObject.DestroyImmediate(go);
                        }
                    }
                    //GameObjectPose
                    SaveTransform(defaultPathTransforms, rootObject.transform, rootObject.transform, false);
                }
                bindTransforms = Paths2Transforms(bindPathTransforms, rootObject.transform);
                tposeTransforms = Paths2Transforms(tposePathTransforms, rootObject.transform);
                prefabTransforms = Paths2Transforms(prefabPathTransforms, rootObject.transform);
                humanDescriptionTransforms = Paths2Transforms(humanDescriptionPathTransforms, rootObject.transform);
            }
            #endregion
        }

        public void ChangeStartTransform()
        {
            var transform = rootObject.transform;
            startPosition = transform.position;
            startRotation = transform.rotation;
            startScale = transform.lossyScale;
            startLocalPosition = transform.localPosition;
            startLocalRotation = transform.localRotation;
            startLocalScale = transform.localScale;
            ChangeTransform(transform);
        }
        public void ChangeTransform(Transform transform)
        {
            Action<Dictionary<Transform, SaveData>, Transform> SetTransform = (list, t) =>
            {
                if (list == null)
                    return;
                SaveData save;
                if (!list.TryGetValue(t, out save))
                    return;
                save.Save(t);
            };
            SetTransform(originalTransforms, transform);
        }
        public void ChangeTransformReference(GameObject gameObject)
        {
            var paths = new List<string>(originalTransforms.Count);
            var transforms = new List<Transform>(originalTransforms.Count);
            foreach (var pair in originalTransforms)
            {
                paths.Add(AnimationUtility.CalculateTransformPath(pair.Key, rootObject.transform));
                transforms.Add(pair.Key);
            }

            Action<Transform, Transform> SaveTransform = null;
            SaveTransform = (t, root) =>
            {
                var path = AnimationUtility.CalculateTransformPath(t, root);
                var index = paths.IndexOf(path);
                if (index >= 0)
                {
                    Action<Dictionary<Transform, SaveData>, Transform, Transform> ChangeTransform = (list, oldT, newT) =>
                    {
                        if (list != null && list.Count > 0)
                        {
                            SaveData saveData;
                            if (list.TryGetValue(oldT, out saveData))
                            {
                                list.Remove(oldT);
                                list.Add(newT, saveData);
                            }
                        }
                    };
                    ChangeTransform(originalTransforms, transforms[index], t);
                    ChangeTransform(bindTransforms, transforms[index], t);
                    ChangeTransform(tposeTransforms, transforms[index], t);
                    ChangeTransform(prefabTransforms, transforms[index], t);
                    ChangeTransform(humanDescriptionTransforms, transforms[index], t);
                }
                for (int i = 0; i < t.childCount; i++)
                    SaveTransform(t.GetChild(i), root);
            };
            SaveTransform(gameObject.transform, gameObject.transform);
            rootObject = gameObject;
        }

        public bool IsRootStartTransform()
        {
            if (rootObject != null)
            {
                var t = rootObject.transform;
                if (t.position == startPosition &&
                    t.rotation == startRotation)
                {
                    return true;
                }
            }
            return false;
        }
        public void ResetRootStartTransform()
        {
            if (rootObject != null)
            {
                rootObject.transform.SetPositionAndRotation(startPosition, startRotation);
            }
        }
        public void ResetRootOriginalTransform()
        {
            if (rootObject != null)
            {
                rootObject.transform.SetPositionAndRotation(originalPosition, originalRotation);
            }
        }

        public bool ResetDefaultTransform()
        {
            if (ResetBindTransform()) return true;
            if (ResetPrefabTransform()) return true;
            if (ResetOriginalTransform()) return true;
            return false;
        }

        public bool IsEnableOriginalTransform()
        {
            return (originalTransforms != null && originalTransforms.Count > 0);
        }
        public bool ResetOriginalTransform()
        {
            if (IsEnableOriginalTransform())
            {
                foreach (var trans in originalTransforms)
                {
                    if (trans.Key != null)
                        trans.Value.LoadLocal(trans.Key);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public SaveData GetOriginalTransform(Transform t)
        {
            if (originalTransforms != null)
            {
                SaveData data;
                if (originalTransforms.TryGetValue(t, out data))
                {
                    return data;
                }
            }
            return null;
        }

        public bool IsEnableBindTransform()
        {
            return (bindTransforms != null && bindTransforms.Count > 0);
        }
        public bool ResetBindTransform()
        {
            if (IsEnableBindTransform())
            {
                foreach (var trans in bindTransforms)
                {
                    if (trans.Key != null)
                        trans.Value.LoadLocal(trans.Key);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public SaveData GetBindTransform(Transform t)
        {
            if (bindTransforms != null)
            {
                SaveData data;
                if (bindTransforms.TryGetValue(t, out data))
                {
                    return data;
                }
            }
            return null;
        }
        public bool IsEnableTPoseTransform()
        {
            return (tposeTransforms != null && tposeTransforms.Count > 0);
        }
        public bool ResetTPoseTransform()
        {
            if (IsEnableTPoseTransform())
            {
                foreach (var trans in tposeTransforms)
                {
                    if (trans.Key != null)
                        trans.Value.LoadLocal(trans.Key);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public SaveData GetTPoseTransform(Transform t)
        {
            if (tposeTransforms != null)
            {
                SaveData data;
                if (tposeTransforms.TryGetValue(t, out data))
                {
                    return data;
                }
            }
            return null;
        }

        public bool IsEnablePrefabTransform()
        {
            return (prefabTransforms != null && prefabTransforms.Count > 0);
        }
        public bool ResetPrefabTransform()
        {
            if (IsEnablePrefabTransform())
            {
                foreach (var trans in prefabTransforms)
                {
                    if (trans.Key != null)
                        trans.Value.LoadLocal(trans.Key);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public SaveData GetPrefabTransform(Transform t)
        {
            if (prefabTransforms != null)
            {
                SaveData data;
                if (prefabTransforms.TryGetValue(t, out data))
                {
                    return data;
                }
            }
            return null;
        }

        public bool IsEnableHumanDescriptionTransforms()
        {
            return (humanDescriptionTransforms != null && humanDescriptionTransforms.Count > 0);
        }
        public bool ResetHumanDescriptionTransforms()
        {
            if (IsEnableHumanDescriptionTransforms())
            {
                foreach (var trans in humanDescriptionTransforms)
                {
                    if (trans.Key != null)
                        trans.Value.LoadLocal(trans.Key);
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public SaveData GetHumanDescriptionTransforms(Transform t)
        {
            if (humanDescriptionTransforms != null)
            {
                SaveData data;
                if (humanDescriptionTransforms.TryGetValue(t, out data))
                {
                    return data;
                }
            }
            return null;
        }

        private Dictionary<Transform, SaveData> Paths2Transforms(Dictionary<string, SaveData> src, Transform transform)
        {
            var dst = new Dictionary<Transform, SaveData>(src.Count);
            Action<Transform, Transform> SaveTransform = null;
            SaveTransform = (t, root) =>
            {
                var path = AnimationUtility.CalculateTransformPath(t, root);
                if (src.ContainsKey(path))
                    dst.Add(t, src[path]);
                for (int i = 0; i < t.childCount; i++)
                    SaveTransform(t.GetChild(i), root);
            };
            SaveTransform(transform, transform);
            return dst;
        }
    }
}
