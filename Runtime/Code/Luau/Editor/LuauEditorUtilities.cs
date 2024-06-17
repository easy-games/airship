using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Reflection;
using System.Text;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using HandlebarsDotNet.Compiler;
using Luau;
using UnityEditor;
using UnityEngine;

namespace Code.Luau.Editor {
#if AIRSHIP_INTERNAL && UNITY_EDITOR
    public class LuauEditorUtilities : UnityEditor.Editor {
        [MenuItem("Airship/Luau/Reveal Indiscernible Overloads", true)]
        public static bool CanCheckForIndiscernibleOverloads() {
            return Application.isPlaying;
        }
        
        [MenuItem("Airship/Luau/Reveal Indiscernible Overloads")]
        public static void CheckForIndiscernibleOverloads() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                foreach (var type in assembly.GetTypes()) {
                    if (!ReflectionList.IsAllowed(type, LuauContext.Game)) continue;
                    var typeName = type.FullName;
                    
                    // Check methods
                    Dictionary<string, List<Type>> foundMethods = new();
                    var methodInfos = type.GetMethods();
                    foreach (var info in methodInfos) {
                        if (info.IsVirtual) continue;
                        if (CheckCallInfoForCollisions(typeName, foundMethods, info.GetParameters(), info.Name)) {
                            break;
                        }
                    }
                    
                    // Check constructors
                    Dictionary<string, List<Type>> foundConstructors = new();
                    var constructorInfos = type.GetConstructors();
                    foreach (var info in constructorInfos) {
                        if (CheckCallInfoForCollisions(typeName, foundConstructors, info.GetParameters(), info.Name)) {
                            break;
                        }
                    }
                }
            }
        }

        private static bool CheckCallInfoForCollisions(string typeName, Dictionary<string, List<Type>> foundParameterSets, ParameterInfo[] parameters, string name) {
            foreach (var param in parameters) {
                if (param.ParameterType == null) return false;
            }
            // Grab parameters in a string (so we can quickly check matching pod parameter lists)
            GetPodParameters(parameters, out var podTypeString, out var podObjects);
            if (podTypeString == "") return false;
            var fullKey = name + " " + podTypeString;
                        
            // Check for match
            if (foundParameterSets.ContainsKey(fullKey)) {
                var seenMethods = foundParameterSets[fullKey];
                if (podObjects.Count == seenMethods.Count) {
                    var match = true;
                    for (var i = 0; i < podObjects.Count; i++) {
                        if (podObjects[i] != seenMethods[i]) {
                            match = false;
                            break;
                        }
                    }

                    if (match) {
                        Debug.Log("-------");
                        Debug.Log("Overload: " + typeName + "." + fullKey);
                        if (podObjects.Count > 0) {
                            Debug.Log("With object types:");
                            foreach (var obj in podObjects) {
                                Debug.Log(" - " + obj.FullName);
                            }
                        }
                        return true;
                    }
                }
            }
                        
            // Add to match dictionary
            foundParameterSets[fullKey] = podObjects;
            return false;
        }
        
        private static void GetPodParameters(ParameterInfo[] parameters, out string podTypeString, out List<Type> podObjects) {
            podObjects = new List<Type>();
            int[] podTypes = new int[parameters.Length];
            for (int paramIndex = 0; paramIndex < parameters.Length; paramIndex++) {
                Type sourceParamType = parameters[paramIndex].ParameterType;
                if (parameters[paramIndex].IsOut == true || parameters[paramIndex].IsIn == true) {
                    sourceParamType = sourceParamType.GetElementType();
                }

                var paramPodType = LuauCore.GetParamPodType(sourceParamType);
                podTypes[paramIndex] = (int) paramPodType;
                if (paramPodType == LuauCore.PODTYPE.POD_OBJECT) {
                    podObjects.Add(sourceParamType);
                }
            }
            podTypeString = String.Join(" ", podTypes);
        }

        
        
    }
#endif
}