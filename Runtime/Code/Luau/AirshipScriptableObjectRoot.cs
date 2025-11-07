using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Luau {
    public static class AirshipScriptableObjectRoot {
        private static int _idGen;
        
        private static readonly Dictionary<AirshipScriptableObject, int> ScriptableObjectToId = new();
        private static readonly Dictionary<int, AirshipScriptableObject> IdToScriptableObject = new();
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            _idGen = 0;
            ScriptableObjectToId.Clear();
            IdToScriptableObject.Clear();
        }
        
        public static int GetIdFromScriptableObject(AirshipScriptableObject scriptableObject) {
            if (ScriptableObjectToId.TryGetValue(scriptableObject, out var id)) return id;

            id = ++_idGen;
            ScriptableObjectToId.Add(scriptableObject, id);
            IdToScriptableObject.Add(id, scriptableObject);

            return id;
        }

        [CanBeNull]
        public static AirshipScriptableObject GetScriptableObjectFromId(int id) {
            if (IdToScriptableObject.TryGetValue(id, out var scriptableObject)) {
                return scriptableObject;
            }

            return null;
        }

        public static void CleanIdOnDestroy(AirshipScriptableObject scriptableObject) {
            if (ScriptableObjectToId.TryGetValue(scriptableObject, out var id)) {
                IdToScriptableObject.Remove(id);
                ScriptableObjectToId.Remove(scriptableObject);
            }
        }
    }
}