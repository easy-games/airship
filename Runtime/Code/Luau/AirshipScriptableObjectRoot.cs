using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace Luau {
    public static class AirshipScriptableObjectRoot {
        private static int _idGen;
        
        private static readonly Dictionary<AirshipScriptableObject, int> ScriptableObjectToId = new();
        private static readonly Dictionary<int, AirshipScriptableObject> IdToScriptableObject = new();

        public static void DebugCommand() {
            Debug.Log($"=== Scriptable Objects ({IdToScriptableObject.Count}) ===");
            foreach (var (id, obj) in IdToScriptableObject) {
                if (obj == null) {
                    Debug.Log($"\tid: {id}\t**NULL REFERENCE**");
                } else {
                    if (obj.script == null) {
                        Debug.Log($"\tid: {id}\tname: '{obj.name}'\t\tscript: **NULL SCRIPT**");
                    } else {
                        Debug.Log($"\tid: {id}\tname: '{obj.name}'\t\tscript: '{obj.script.m_path}'");
                    }
                }
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetOnLoad() {
#if AIRSHIP_PLAYER
            var scriptableObjects = IdToScriptableObject.ToArray();
            foreach (var (_, scriptableObject) in scriptableObjects) {
                scriptableObject.Destroy();
            }
#endif
            
            _idGen = 0;
            ScriptableObjectToId.Clear();
            IdToScriptableObject.Clear();
        }
        
        public static int GetIdFromScriptableObject(AirshipScriptableObject scriptableObject) {
            if (ScriptableObjectToId.TryGetValue(scriptableObject, out var id)) {
                return id;
            }

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

        public static bool ContainsScriptableObject(AirshipScriptableObject scriptableObject) {
            return ScriptableObjectToId.TryGetValue(scriptableObject, out _);
        }

        public static void CleanIdOnDestroy(AirshipScriptableObject scriptableObject) {
            if (ScriptableObjectToId.TryGetValue(scriptableObject, out var id)) {
                IdToScriptableObject.Remove(id);
                ScriptableObjectToId.Remove(scriptableObject);
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