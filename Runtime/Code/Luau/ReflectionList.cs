﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using FishNet;
using FishNet.Component.ColliderRollback;
using FishNet.Managing.Timing;
using FishNet.Object;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using LightType = UnityEngine.LightType;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Luau {
    public static class ReflectionList {
        private const bool IsReflectionListEnabled = true;
        
        private const LuauContext LuauContextAll = LuauContext.Game | LuauContext.Protected;

        // Add types here that should be allowed.
        // NOTE: If it is our own code, use the LuauAPI attribute instead.
        private static readonly Dictionary<Type, LuauContext> AllowedTypes = new() {
            [typeof(SceneManager)] = LuauContext.Protected,
            [typeof(Scene)] = LuauContextAll,
            [typeof(Vector2)] = LuauContextAll,
            [typeof(Vector3)] = LuauContextAll,
            [typeof(Vector4)] = LuauContextAll,
            [typeof(Color)] = LuauContextAll,
            [typeof(UnityEngine.Object)] = LuauContextAll,
            [typeof(Transform)] = LuauContextAll,
            [typeof(RectTransform)] = LuauContextAll,
            [typeof(Sprite)] = LuauContextAll,
            [typeof(CanvasGroup)] = LuauContextAll,
            [typeof(BoxCollider)] = LuauContextAll,
            [typeof(BoxCollider2D)] = LuauContextAll,
            [typeof(CapsuleCollider)] = LuauContextAll,
            [typeof(CapsuleCollider2D)] = LuauContextAll,
            [typeof(Collider)] = LuauContextAll,
            [typeof(Collider2D)] = LuauContextAll,
            [typeof(SphereCollider)] = LuauContextAll,
            [typeof(UnityEngine.UI.HorizontalLayoutGroup)] = LuauContextAll,
            [typeof(UnityEngine.UI.LayoutRebuilder)] = LuauContextAll,
            [typeof(UnityEngine.UI.Image)] = LuauContextAll,
            [typeof(UnityEngine.UI.Button)] = LuauContextAll,
            [typeof(UnityEngine.UI.Dropdown)] = LuauContextAll,
            [typeof(UnityEngine.UI.InputField)] = LuauContextAll,
            [typeof(UnityEngine.UI.Scrollbar)] = LuauContextAll,
            [typeof(UnityEngine.UI.Text)] = LuauContextAll,
            [typeof(UnityEngine.UI.ScrollRect)] = LuauContextAll,
            [typeof(UnityEngine.UI.VerticalLayoutGroup)] = LuauContextAll,
            [typeof(UnityEngine.UI.RawImage)] = LuauContextAll,
            [typeof(UnityEngine.Profiling.Profiler)] = LuauContextAll,
            [typeof(AudioSource)] = LuauContextAll,
            [typeof(Physics)] = LuauContextAll,
            [typeof(GameObject)] = LuauContextAll,
            [typeof(string)] = LuauContextAll,
            [typeof(Rigidbody)] = LuauContextAll,
            [typeof(Rigidbody2D)] = LuauContextAll,
            [typeof(Animator)] = LuauContextAll,
            [typeof(AnimancerComponent)] = LuauContextAll,
            [typeof(Debug)] = LuauContextAll,
            [typeof(ClipState)] = LuauContextAll,
            [typeof(TimeManager)] = LuauContextAll,
            [typeof(Canvas)] = LuauContextAll,
            [typeof(Camera)] = LuauContextAll,
            [typeof(InstanceFinder)] = LuauContextAll,
            [typeof(Component)] = LuauContextAll,
            [typeof(NetworkObject)] = LuauContextAll,
            [typeof(EventSystem)] = LuauContextAll,
            [typeof(Material)] = LuauContextAll,
            [typeof(RollbackManager)] = LuauContextAll,
            [typeof(AudioClip)] = LuauContextAll,
            [typeof(AudioListener)] = LuauContextAll,
            [typeof(AudioRolloffMode)] = LuauContextAll,
            [typeof(Physics2D)] = LuauContextAll,
            [typeof(LayerMask)] = LuauContextAll,
            [typeof(CanvasScaler)] = LuauContextAll,
            [typeof(UnityEngine.UIElements.Image)] = LuauContextAll,
            [typeof(UnityEngine.UIElements.Button)] = LuauContextAll,
            [typeof(ParticleSystem)] = LuauContextAll,
            [typeof(ParticleSystemRenderer)] = LuauContextAll,
            [typeof(Light)] = LuauContextAll,
            [typeof(PointLight)] = LuauContextAll,
            [typeof(LightType)] = LuauContextAll,
        };
        
        // Add types (as strings) here that should be allowed.
        // NOTE: If it is our own code, use the LuauAPI attribute instead.
        private static readonly Dictionary<string, LuauContext> AllowedTypeStrings = new() {
            // [""] = LuauContext.Protected,
            ["ElRaccoone.Tweens.LocalScaleTween+Driver"] = LuauContextAll,
            ["ElRaccoone.Tweens.GraphicAlphaTween+Driver"] = LuauContextAll,
            ["ElRaccoone.Tweens.PositionTween+Driver"] = LuauContextAll,
        };

        private static Dictionary<Type, LuauContext> _allowedTypesInternal;
        private static Dictionary<string, Type> _stringToTypeCache;
        private static Dictionary<Assembly, List<string>> _assemblyNamespaces;

        /// <summary>
        /// Add a type to the reflection list with the given Luau context mask.
        /// </summary>
        public static void AddToReflectionList(Type t, LuauContext contextMask) {
            _allowedTypesInternal.Add(t, contextMask);
        }

        /// <summary>
        /// Checks if the given type exists and is allowed for reflection given the Luau context.
        /// </summary>
        public static bool IsAllowed(Type t, LuauContext context) {
            if (!IsReflectionListEnabled) return true;

            bool isDict = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            if (isDict) {
                return true;
            }

            // Protected context has access to all
            if ((context & LuauContext.Protected) != 0) {
                return true;
            }
            
            if (t.IsArray) {
                t = t.GetElementType();
            }
            return _allowedTypesInternal.TryGetValue(t, out var mask) && (mask & context) != 0;
        }

        public static bool IsAllowedFromString(string typeStr, LuauContext context) {
            if (_stringToTypeCache.TryGetValue(typeStr, out var t)) {
                return IsAllowed(t, context);
            }
            
            var typeFromStr = AttemptGetTypeFromString(typeStr);
            if (typeFromStr != null) {
                _stringToTypeCache[typeStr] = typeFromStr;
                return IsAllowed(typeFromStr, context);
            }

            return false;
        }

        internal static Type AttemptGetTypeFromString(string typeStr) {
            if (string.IsNullOrEmpty(typeStr)) return null;
            
            var t = Type.GetType(typeStr);
            if (t != null) {
                return t;
            }
            
            foreach (var (assembly, namespaces) in _assemblyNamespaces) {
                var type = assembly.GetType(typeStr);
                if (type != null) {
                    return type;
                }
                
                foreach (var ns in namespaces) {
                    type = assembly.GetType(ns + "." + typeStr);
                    if (type != null) {
                        return type;
                    }
                }
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            _allowedTypesInternal = new Dictionary<Type, LuauContext>(AllowedTypes);
            _stringToTypeCache = new Dictionary<string, Type>();

            // Collect all namespaces per assembly:
            _assemblyNamespaces = new Dictionary<Assembly, List<string>>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var namespaces = new List<string>();
                var nsSet = new HashSet<string>();
                foreach (var t in assembly.GetTypes()) {
                    var ns = t.Namespace;
                    if (string.IsNullOrEmpty(ns)) continue;
                    if (nsSet.Add(ns)) {
                        namespaces.Add(ns);
                    }
                }
                _assemblyNamespaces[assembly] = namespaces;
            }
            
            foreach (var (typeStr, context) in AllowedTypeStrings) {
                var t = AttemptGetTypeFromString(typeStr);
                if (t == null) {
                    Debug.LogError($"Failed to find type \"{typeStr}\"");
                    continue;
                }
                _allowedTypesInternal.TryAdd(t, context);
            }
        }
    }
    
#if UNITY_EDITOR
    public class ReflectionListEditor : EditorWindow {
        [MenuItem("Airship/Misc/Type Validator")]
        public static void ShowWindow() {
            EditorWindow window = GetWindow<ReflectionListEditor>();
            window.titleContent = new GUIContent("Type Validator");
        }

        public void CreateGUI() {
            var textField = new TextField("Type");
            var fullNameTextField = new TextField("Full Name");
            var isValidLabel = new Label();

            textField.RegisterValueChangedCallback((typeStr) => {
                var t = ReflectionList.AttemptGetTypeFromString(typeStr.newValue);
                if (t == null) {
                    isValidLabel.text = "Invalid type";
                    fullNameTextField.value = "";
                } else {
                    isValidLabel.text = "Valid";
                    fullNameTextField.value = t.FullName;
                }
                isValidLabel.visible = true;
            });

            fullNameTextField.isReadOnly = true;
            isValidLabel.visible = false;
            
            rootVisualElement.Add(textField);
            rootVisualElement.Add(fullNameTextField);
            rootVisualElement.Add(isValidLabel);
        }
    }
#endif
}
