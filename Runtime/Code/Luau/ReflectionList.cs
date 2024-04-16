using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Luau {
    public static class ReflectionList {
        private const LuauContext LuauContextAll = LuauContext.Game | LuauContext.Protected;

        private static readonly Dictionary<Type, LuauContext> AllowedTypes = new() {
            [typeof(SceneManager)] = LuauContextAll,
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
            [typeof(UnityEngine.UI.HorizontalLayoutGroup)] = LuauContextAll,
            [typeof(UnityEngine.UI.LayoutRebuilder)] = LuauContextAll,
            [typeof(UnityEngine.UI.Image)] = LuauContextAll,
            [typeof(UnityEngine.UI.Button)] = LuauContextAll,
            [typeof(UnityEngine.UI.Dropdown)] = LuauContextAll,
            [typeof(UnityEngine.UI.InputField)] = LuauContextAll,
            [typeof(UnityEngine.UI.Scrollbar)] = LuauContextAll,
            [typeof(UnityEngine.UI.Text)] = LuauContextAll,
            [typeof(UnityEngine.Profiling.Profiler)] = LuauContextAll,
            [typeof(AudioSource)] = LuauContextAll,
            [typeof(Physics)] = LuauContextAll,
            [typeof(GameObject)] = LuauContextAll,
            [typeof(string)] = LuauContextAll,
            
        };

        private static Dictionary<Type, LuauContext> _allowedTypesInternal;

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
            if (t.IsArray) {
                t = t.GetElementType();
            }
            return _allowedTypesInternal.TryGetValue(t, out var mask) && (mask & context) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            _allowedTypesInternal = new Dictionary<Type, LuauContext>(AllowedTypes);
        }
    }
}
