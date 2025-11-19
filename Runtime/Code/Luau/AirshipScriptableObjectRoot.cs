using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Luau {
    public static class AirshipScriptableObjectRoot {
        private static int _idGen;
        
        private static readonly Dictionary<AirshipScriptableObject, int> ScriptableObjectToId = new();
        private static readonly Dictionary<int, AirshipScriptableObject> IdToScriptableObject = new();
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetOnLoad() {
#if AIRSHIP_PLAYER
            foreach (var (_, scriptableObject) in IdToScriptableObject) {
#if AIRSHIP_STAGING
                Debug.Log($"[SR] Destroy scriptable object {scriptableObject} with id {scriptableObject.instanceId}");
#endif
                Object.Destroy(scriptableObject);
            }
#endif
            
            _idGen = 0;
            ScriptableObjectToId.Clear();
            IdToScriptableObject.Clear();
            
#if AIRSHIP_STAGING
            Debug.Log($"[SR] Reset ScriptableObject context");
#endif
        }
        
        public static int GetIdFromScriptableObject(AirshipScriptableObject scriptableObject) {
            if (ScriptableObjectToId.TryGetValue(scriptableObject, out var id)) return id;

            id = ++_idGen;
            ScriptableObjectToId.Add(scriptableObject, id);
            IdToScriptableObject.Add(id, scriptableObject);

#if AIRSHIP_STAGING
            Debug.Log($"[SR] Create Id for scriptable object {scriptableObject} - assigning {id}");
#endif
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
                
#if AIRSHIP_STAGING
                Debug.Log($"[SR] Cleanup scriptable object {id}");
#endif
            }
        }
    }
    
    // Matches same enum order in AirshipComponent.h plugin file
    public enum AirshipScriptableObjectUpdateType {
        AirshipEnabled,
        AirshipDisabled,
        AirshipAwake,
        AirshipDestroy,
    }
}