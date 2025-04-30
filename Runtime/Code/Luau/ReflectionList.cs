using System;
using System.Collections.Generic;
using System.Reflection;
using ElRaccoone.Tweens;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using LightType = UnityEngine.LightType;
using UnityEngine.Tilemaps;
using UnityEngine.VFX;
using Toggle = UnityEngine.UI.Toggle;

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
            [typeof(Vector2)] = LuauContextAll,
            [typeof(Vector3)] = LuauContextAll,
            [typeof(Vector4)] = LuauContextAll,
            [typeof(Color)] = LuauContextAll,
            [typeof(string)] = LuauContextAll,
            // Unity
            [typeof(UnityEngine.Object)] = LuauContextAll,
            [typeof(GameObject)] = LuauContextAll,
            [typeof(Transform)] = LuauContextAll,
            [typeof(RectTransform)] = LuauContextAll,
            [typeof(Component)] = LuauContextAll,
            [typeof(Camera)] = LuauContextAll,
            [typeof(Debug)] = LuauContextAll,
            [typeof(LayerMask)] = LuauContextAll,
            [typeof(Scene)] = LuauContextAll,
            [typeof(SceneManager)] = LuauContext.Protected,
            [typeof(UnityEngine.Profiling.Profiler)] = LuauContextAll,
            [typeof(ShaderWarmup)] = LuauContextAll,
            [typeof(ShaderVariantCollection)] = LuauContextAll,

            // Navmesh
            [typeof(NavMesh)] = LuauContextAll,
            [typeof(NavMeshAgent)] = LuauContextAll,
            [typeof(NavMeshBuilder)] = LuauContextAll,
            [typeof(NavMeshHit)] = LuauContextAll,
            [typeof(NavMeshObstacle)] = LuauContextAll,
            [typeof(NavMeshPath)] = LuauContextAll,
            [typeof(NavMeshLinkData)] = LuauContextAll,
            [typeof(NavMeshLinkInstance)] = LuauContextAll,
            [typeof(OffMeshLinkData)] = LuauContextAll,
            [typeof(OffMeshLinkType)] = LuauContextAll,
            [typeof(NavMeshQueryFilter)] = LuauContextAll,

            // Mirror
            [typeof(NetworkServer)] = LuauContextAll,
            [typeof(NetworkClient)] = LuauContextAll,
            [typeof(NetworkTransform)] = LuauContextAll,
            [typeof(NetworkTransformReliable)] = LuauContextAll,
            [typeof(NetworkTransformUnreliable)] = LuauContextAll,
            [typeof(NetworkIdentity)] = LuauContextAll,
            [typeof(NetworkAnimator)] = LuauContextAll,
            [typeof(NetworkConnectionToClient)] = LuauContextAll,
            [typeof(NetworkConnectionToServer)] = LuauContextAll,
            [typeof(NetworkConnection)] = LuauContextAll,
            [typeof(NetworkTime)] = LuauContextAll,
            [typeof(LagCompensator)] = LuauContextAll,
            [typeof(PredictedRigidbody)] = LuauContextAll,
            [typeof(PredictedState)] = LuauContextAll,
            [typeof(PredictedSyncData)] = LuauContextAll,
            [typeof(PredictedSyncDataReadWrite)] = LuauContextAll,

            // Physics
            [typeof(CharacterController)] = LuauContextAll,
            [typeof(Physics)] = LuauContextAll,
            [typeof(Physics2D)] = LuauContextAll,
            [typeof(Rigidbody)] = LuauContextAll,
            [typeof(Rigidbody2D)] = LuauContextAll,
            [typeof(ContactPoint)] = LuauContextAll,
            [typeof(ContactPoint2D)] = LuauContextAll,
            [typeof(BoxCollider)] = LuauContextAll,
            [typeof(BoxCollider2D)] = LuauContextAll,
            [typeof(CapsuleCollider)] = LuauContextAll,
            [typeof(CapsuleCollider2D)] = LuauContextAll,
            [typeof(Collider)] = LuauContextAll,
            [typeof(Collider2D)] = LuauContextAll,
            [typeof(WheelCollider)] = LuauContextAll,
            [typeof(SphereCollider)] = LuauContextAll,
            [typeof(CircleCollider2D)] = LuauContextAll,
            [typeof(PolygonCollider2D)] = LuauContextAll,
            [typeof(EdgeCollider2D)] = LuauContextAll,
            [typeof(TilemapCollider2D)] = LuauContextAll,
            [typeof(CustomCollider2D)] = LuauContextAll,
            [typeof(MeshCollider)] = LuauContextAll,
            [typeof(RaycastHit)] = LuauContextAll,
            [typeof(RaycastHit[])] = LuauContextAll,
            [typeof(ConstantForce)] = LuauContextAll,
            [typeof(ConstantForce2D)] = LuauContextAll,
            [typeof(FixedJoint)] = LuauContextAll,
            [typeof(CharacterJoint)] = LuauContextAll,
            [typeof(Bounds)] = LuauContextAll,
            [typeof(TerrainCollider)] = LuauContextAll,
            [typeof(Terrain)] = LuauContextAll,
            [typeof(TreeInstance)] = LuauContextAll,
            // UI
            [typeof(Canvas)] = LuauContextAll,
            [typeof(CanvasGroup)] = LuauContextAll,
            [typeof(CanvasScaler)] = LuauContextAll,
            [typeof(EventSystem)] = LuauContextAll,
            [typeof(RawImage)] = LuauContextAll,
            [typeof(UnityEngine.UI.Image)] = LuauContextAll,
            [typeof(UnityEngine.UIElements.Image)] = LuauContextAll,
            [typeof(UnityEngine.UIElements.Button)] = LuauContextAll,
            [typeof(HorizontalLayoutGroup)] = LuauContextAll,
            [typeof(VerticalLayoutGroup)] = LuauContextAll,
            [typeof(Grid)] = LuauContextAll,
            [typeof(GridLayoutGroup)] = LuauContextAll,
            [typeof(LayoutRebuilder)] = LuauContextAll,
            [typeof(UnityEngine.UI.Button)] = LuauContextAll,
            [typeof(Dropdown)] = LuauContextAll,
            [typeof(TMP_Dropdown)] = LuauContextAll,
            [typeof(TMP_InputField)] = LuauContextAll,
            [typeof(TMP_Text)] = LuauContextAll,
            [typeof(TextMeshProUGUI)] = LuauContextAll,
            [typeof(UnityEngine.UI.Slider)] = LuauContextAll,
            [typeof(CanvasScaler)] = LuauContextAll,
            [typeof(Toggle)] = LuauContextAll,
            [typeof(Mask)] = LuauContextAll,
            [typeof(ContentSizeFitter)] = LuauContextAll,
            [typeof(AspectRatioFitter)] = LuauContextAll,
            [typeof(InputField)] = LuauContextAll,
            [typeof(Scrollbar)] = LuauContextAll,
            [typeof(ScrollRect)] = LuauContextAll,
            [typeof(Text)] = LuauContextAll,
            [typeof(RectMask2D)] = LuauContextAll,

            // Particles
            [typeof(ParticleSystem)] = LuauContextAll,
            [typeof(ParticleSystemRenderer)] = LuauContextAll,
            [typeof(TrailRenderer)] = LuauContextAll,
            // Lights
            [typeof(Light)] = LuauContextAll,
            [typeof(PointLight)] = LuauContextAll,
            [typeof(LightType)] = LuauContextAll,
            // Animations
            [typeof(Animator)] = LuauContextAll,
            [typeof(Animation)] = LuauContextAll,
            [typeof(AnimationCurve)] = LuauContextAll,
            [typeof(RuntimeAnimatorController)] = LuauContextAll,
            [typeof(AnimationClip)] = LuauContextAll,
            // Audio
            [typeof(AudioClip)] = LuauContextAll,
            [typeof(AudioListener)] = LuauContextAll,
            [typeof(AudioRolloffMode)] = LuauContextAll,
            [typeof(AudioSource)] = LuauContextAll,
            [typeof(AudioResource)] = LuauContextAll,
            [typeof(AudioMixerGroup)] = LuauContextAll,
            [typeof(AudioMixerSnapshot)] = LuauContextAll,
            //Rendering
            [typeof(Graphics)] = LuauContextAll,
            [typeof(Material)] = LuauContextAll,
            [typeof(Texture2D)] = LuauContextAll,
            [typeof(RenderTexture)] = LuauContextAll,
            [typeof(Renderer)] = LuauContextAll,
            [typeof(LineRenderer)] = LuauContextAll,
            [typeof(MeshRenderer)] = LuauContextAll,
            [typeof(SkinnedMeshRenderer)] = LuauContextAll,
            [typeof(MeshFilter)] = LuauContextAll,
            [typeof(Sprite)] = LuauContextAll,
            [typeof(DecalProjector)] = LuauContextAll,

            // Rigging
            [typeof(TwoBoneIKConstraint)] = LuauContextAll,
            [typeof(MultiAimConstraint)] = LuauContextAll,
            // Misc
            [typeof(EventTrigger)] = LuauContextAll,
            [typeof(SpriteRenderer)] = LuauContextAll,
            [typeof(ParticleSystem.MinMaxGradient)] = LuauContextAll,
            [typeof(Input)] = LuauContextAll,
            // Tween
            [typeof(NativeTween)] = LuauContextAll,
            // Visual Effects
            [typeof(VisualEffect)] = LuauContextAll,
            [typeof(Volume)] = LuauContextAll,
            [typeof(VolumeProfile)] = LuauContextAll,
            [typeof(DepthOfField)] = LuauContextAll,
            [typeof(GraphicRaycaster)] = LuauContextAll,
        };
        
        // Add types (as strings) here that should be allowed.
        // NOTE: If it is our own code, use the LuauAPI attribute instead.
        private static readonly Dictionary<string, LuauContext> AllowedTypeStrings = new() {
            // [""] = LuauContext.Protected,
            ["ElRaccoone.Tweens.NativeTween+LocalScaleDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+GraphicAlphaDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+PositionDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+RotationDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+AnchoredPositionYDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+AnchoredPositionXDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+AnchoredPositionDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+SizeDeltaDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+LocalPositionDriver"] = LuauContextAll,
            ["ElRaccoone.Tweens.NativeTween+LocalRotationDriver"] = LuauContextAll,
            ["ActiveAccessory[]"] = LuauContextAll,
            ["TubeRendererCS"] = LuauContextAll,
            ["AudioMixerSnapshotController"] = LuauContextAll,
            ["AudioMixerController"] = LuauContextAll,
        };

        public static Dictionary<Type, LuauContext> allowedTypesInternal;
        private static Dictionary<MethodInfo, LuauContext> _allowedMethodInfos;
        
        private static Dictionary<string, Type> _stringToTypeCache;
        private static Dictionary<Assembly, List<string>> _assemblyNamespaces;

        /// <summary>
        /// Add a type to the reflection list with the given Luau context mask.
        /// If the type already exists, we union the contexts.
        /// </summary>
        public static void AddToReflectionList(Type t, LuauContext contextMask) {
            if (allowedTypesInternal.TryGetValue(t, out var existingContext)) {
                contextMask |= existingContext;
            }
            allowedTypesInternal[t] = contextMask;
        }

        public static void AddToMethodList(MethodInfo info, LuauContext contextMask) {
            _allowedMethodInfos.TryAdd(info, contextMask);
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


            var allowed = allowedTypesInternal.TryGetValue(t, out var mask) && (mask & context) != 0;
            if (!allowed) {
                if (t != null && !string.IsNullOrEmpty(t.Namespace) && t.Namespace.Contains("ElRaccoone")) {
                    return true;
                }
            }

            return allowed;
        }

        public static bool IsMethodAllowed(Type classType, MethodInfo methodInfo, LuauContext context) {
            if (!IsReflectionListEnabled) return true;

            // Protected context has access to all
            if ((context & LuauContext.Protected) != 0) {
                return true;
            }

            if (_allowedMethodInfos.TryGetValue(methodInfo, out var methodMask)) {
                return (methodMask & context) != 0;
            }

            return IsAllowed(classType, context);
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

        public static Type AttemptGetTypeFromString(string typeStr) {
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
            allowedTypesInternal = new Dictionary<Type, LuauContext>(AllowedTypes);
            _stringToTypeCache = new Dictionary<string, Type>();
            _allowedMethodInfos = new Dictionary<MethodInfo, LuauContext>();

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
                allowedTypesInternal.TryAdd(t, context);
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
