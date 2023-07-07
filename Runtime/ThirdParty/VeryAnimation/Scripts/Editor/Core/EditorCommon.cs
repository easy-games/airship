using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class EditorCommon
    {
        public class ArrowMesh
        {
            public Mesh mesh { get; private set; }
            public Material material { get; private set; }

            public ArrowMesh()
            {
                #region Mesh
                mesh = new Mesh();
                mesh.hideFlags |= HideFlags.DontSave;
                Vector3[] lines = new Vector3[]
                {
                    new Vector3(0, 0, 0),

                    new Vector3(0, 0.1f, 0.1f),
                    new Vector3(0.09f, -0.05f, 0.1f),
                    new Vector3(-0.09f, -0.05f, 0.1f),

                    new Vector3(0, 0, 1),
                };
                int[] indices = new int[]
                {
                    0, 1,
                    0, 2,
                    0, 3,

                    1, 2,
                    2, 3,
                    3, 1,

                    4, 1,
                    4, 2,
                    4, 3,
                };
                mesh.vertices = lines;
                mesh.SetIndices(indices, MeshTopology.Lines, 0);
                mesh.RecalculateBounds();
                #endregion

                material = new Material(Shader.Find("Very Animation/VertexColor-Transparent"));
                material.hideFlags |= HideFlags.DontSave;
            }
            ~ArrowMesh()
            {
                EditorApplication.delayCall += () =>
                {
                    Mesh.DestroyImmediate(mesh);
                    Material.DestroyImmediate(material);
                };
            }
        }

        public static Texture2D CreateColorTexture(Color color)
        {
            Texture2D tex = new Texture2D(4, 4);
            tex.hideFlags |= HideFlags.DontSave;
            for (int x = 0; x < tex.width; x++)
            {
                for (int y = 0; y < tex.height; y++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return tex;
        }

        public static void SaveInsideAssetsFolderDisplayDialog()
        {
            EditorUtility.DisplayDialog(Language.GetText(Language.Help.DisplayDialogSaveInsideAssetsFolder),
                                        Language.GetTooltip(Language.Help.DisplayDialogSaveInsideAssetsFolder), "ok");
        }

        public static Texture2D LoadTexture2DAssetAtPath(string path)
        {
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (result == null)
            {
                var fileName = Path.GetFileName(path);
                var guids = AssetDatabase.FindAssets("t:Texture2D");
                for (int i = 0; i < guids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (Path.GetFileName(assetPath) == fileName)
                    {
                        if (assetPath.IndexOf("VeryAnimation") >= 0)
                        {
                            result = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        public static List<GameObject> GetHierarchyGameObject(GameObject go)
        {
            List<GameObject> list = new List<GameObject>();
            {
                Action<GameObject> AddList = null;
                AddList = (t) =>
                {
                    list.Add(t);
                    for (int i = 0; i < t.transform.childCount; i++)
                    {
                        AddList(t.transform.GetChild(i).gameObject);
                    }
                };
                AddList(go);
            }
            return list;
        }
        public static List<Transform> GetHierarchyTransform(Transform root)
        {
            List<Transform> list = new List<Transform>();
            {
                Action<Transform> AddList = null;
                AddList = (t) =>
                {
                    list.Add(t);
                    for (int i = 0; i < t.transform.childCount; i++)
                    {
                        AddList(t.transform.GetChild(i));
                    }
                };
                AddList(root);
            }
            return list;
        }

        public static bool IsAncestorObject(GameObject obj, GameObject ancestorObject)
        {
            if (obj == null || ancestorObject == null)
                return false;
            var t = obj.transform;
            var ancestorT = ancestorObject.transform;
            while (t != null)
            {
                if (t == ancestorT)
                    return true;
                t = t.parent;
            }
            return false;
        }

        public static bool Ray_Triangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 resultP)
        {
            var e1 = v1 - v0;
            var e2 = v2 - v0;

            resultP = Vector3.zero;

            var pvec = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, pvec);

            Vector3 qvec;
            float u, v;
            if (det > Mathf.Epsilon)
            {
                var tvec = ray.origin - v0;
                u = Vector3.Dot(tvec, pvec);
                if (u < 0.0f || u > det) return false;

                qvec = Vector3.Cross(tvec, e1);

                v = Vector3.Dot(ray.direction, qvec);
                if (v < 0.0 || u + v > det) return false;
            }
            else
            {
                return false;
            }

            var inv_det = 1.0f / det;

            var t = Vector3.Dot(e2, qvec);
            t *= inv_det;
            u *= inv_det;
            v *= inv_det;

            resultP = ray.origin + ray.direction * t;

            return true;
        }

        public static bool IsInsideTriangle(Vector3 pos, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 normal)
        {
            float result = 0f;
            {
                var vec0 = v0 - pos;
                var vec1 = v1 - pos;
                var vec2 = v2 - pos;
                {
                    var angle = Vector3.Angle(vec0, vec1);
                    if (Vector3.Dot(Vector3.Cross(vec0, vec1), normal) < 0f)
                        angle *= -1f;
                    result += angle;
                }
                {
                    var angle = Vector3.Angle(vec1, vec2);
                    if (Vector3.Dot(Vector3.Cross(vec1, vec2), normal) < 0f)
                        angle *= -1f;
                    result += angle;
                }
                {
                    var angle = Vector3.Angle(vec2, vec0);
                    if (Vector3.Dot(Vector3.Cross(vec2, vec0), normal) < 0f)
                        angle *= -1f;
                    result += angle;
                }
                result = Mathf.Abs(result);
            }
            return Mathf.Abs(result - 360f) < 0.001f;
        }

        public static void GetTRS(Matrix4x4 mat, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = mat.GetColumn(3);
            rotation = Quaternion.LookRotation(mat.GetColumn(2), mat.GetColumn(1));
            scale = new Vector3(mat.GetColumn(0).magnitude, mat.GetColumn(1).magnitude, mat.GetColumn(2).magnitude);
        }

        public static UnityEditor.Animations.AnimatorController GetAnimatorController(Animator animator)
        {
            UnityEditor.Animations.AnimatorController ac = null;
            if (animator != null)
            {
                if (animator.runtimeAnimatorController is AnimatorOverrideController)
                {
                    var owc = animator.runtimeAnimatorController as AnimatorOverrideController;
                    ac = owc.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                }
                else
                {
                    ac = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                }
            }
            return ac;
        }

        public static bool ArrayContains<T>(T[] array, T value)
        {
            return ArrayIndexOf(array, value) >= 0;
        }
        public static int ArrayIndexOf<T>(T[] array, T value)
        {
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] != null)
                    {
                        if (array[i].Equals(value))
                            return i;
                    }
                    else if (value == null)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public static Func<object, T> CreateGetFieldDelegate<T>(FieldInfo fi)
        {
            if (fi == null)
                return null;

            string methodName = fi.ReflectedType.FullName + ".get_" + fi.Name;
            DynamicMethod dynamicMethod = new DynamicMethod(methodName, typeof(T), new Type[] { typeof(object) }, true);
            ILGenerator gen = dynamicMethod.GetILGenerator();
            if (fi.IsStatic)
            {
                gen.Emit(OpCodes.Ldsfld, fi);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, fi);
            }
            gen.Emit(OpCodes.Ret);
            return (Func<object, T>)dynamicMethod.CreateDelegate(typeof(Func<object, T>));
        }
        public static Action<object, T> CreateSetFieldDelegate<T>(FieldInfo fi)
        {
            if (fi == null)
                return null;

            string methodName = fi.ReflectedType.FullName + ".set_" + fi.Name;
            DynamicMethod dynamicMethod = new DynamicMethod(methodName, null, new Type[] { typeof(object), typeof(T) }, true);
            ILGenerator gen = dynamicMethod.GetILGenerator();
            if (fi.IsStatic)
            {
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stsfld, fi);
            }
            else
            {
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldarg_1);
                gen.Emit(OpCodes.Stfld, fi);
            }
            gen.Emit(OpCodes.Ret);
            return (Action<object, T>)dynamicMethod.CreateDelegate(typeof(Action<object, T>));
        }

        public static void DisableOtherBehaviors(GameObject gameobject)
        {
            var behaviours = gameobject.GetComponentsInChildren<Behaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;
                if (behaviour is Animator ||
                    behaviour is Animation)
                    continue;
                behaviour.enabled = false;
            }
        }

        public static void ShowNotification(string message)
        {
            var scene = SceneView.lastActiveSceneView;
            if (scene == null) return;
            scene.ShowNotification(new GUIContent(message));
        }

        public static string GetAssetPath(UnityEngine.Object obj)
        {
            if (AssetDatabase.Contains(obj))
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                return Path.GetDirectoryName(assetPath);
            }
            else
            {
                return "Assets";
            }
        }
    }
}
