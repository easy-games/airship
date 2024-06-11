using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Luau;
using UnityEngine;

public static class AirshipBehaviourHelper {
    private static readonly List<int> ComponentIds = new();
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnReset() {
        ComponentIds.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PushNil(IntPtr thread) {
        LuauCore.WritePropertyToThread(thread, null, null);
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PushEmptyTable(IntPtr thread) {
        LuauPlugin.LuauPushTableToThread(thread);
        return 1;
    }
    
    private static AirshipBehaviourRoot GetAirshipBehaviourRoot(GameObject gameObject) {
        var airshipComponent = gameObject.GetComponent<AirshipBehaviourRoot>();
        if (airshipComponent == null) {
            // See if it just needs to be started first:
            var foundAny = false;
            foreach (var binding in gameObject.GetComponents<ScriptBinding>()) {
                foundAny = true;
                binding.InitEarly();
            }
            
            // Retry getting AirshipBehaviourRoot:
            if (foundAny) {
                airshipComponent = gameObject.GetComponent<AirshipBehaviourRoot>();
            }
        }

        return airshipComponent;
    }

    public static int GetAirshipComponent(LuauContext context, IntPtr thread, GameObject gameObject, string typeName) {
        var airshipComponent = GetAirshipBehaviourRoot(gameObject);
        if (airshipComponent == null) {
            return PushNil(thread);
        }

        var unityInstanceId = airshipComponent.Id;
        foreach (var binding in gameObject.GetComponents<ScriptBinding>()) {
            binding.InitEarly();
            if (!binding.IsAirshipComponent) continue;

            var componentName = binding.GetAirshipComponentName();
            if (componentName != typeName) continue;

            var componentId = binding.GetAirshipComponentId();

            LuauPlugin.LuauPushAirshipComponent(context, thread, unityInstanceId, componentId);
            return 1;
        }

        return PushNil(thread);
    }

    public static int GetAirshipComponents(LuauContext context, IntPtr thread, GameObject gameObject, string typeName) {
        var airshipComponent = GetAirshipBehaviourRoot(gameObject);
        if (airshipComponent != null) {
            var unityInstanceId = airshipComponent.Id;

            var hasAny = false;
            foreach (var binding in gameObject.GetComponents<ScriptBinding>()) {
                binding.InitEarly();
                if (!binding.IsAirshipComponent) continue;

                var componentName = binding.GetAirshipComponentName();
                if (componentName != typeName) continue;

                var componentId = binding.GetAirshipComponentId();

                if (!hasAny) {
                    hasAny = true;
                    ComponentIds.Clear();
                }
                ComponentIds.Add(componentId);
            }

            if (hasAny) {
                LuauPlugin.LuauPushAirshipComponents(context, thread, unityInstanceId, ComponentIds.ToArray());
                return 1;
            }
        }
        
        return PushEmptyTable(thread);
    }

    public static int GetAirshipComponentInChildren(LuauContext context, IntPtr thread, GameObject gameObject, string typeName, bool includeInactive) {
        // Attempt to initialize any uninitialized bindings first:
        var scriptBindings = gameObject.GetComponentsInChildren<ScriptBinding>();
        foreach (var binding in scriptBindings) {
            // Side effect loads the components if found. No need for its return result here.
            GetAirshipBehaviourRoot(binding.gameObject);
        }
        
        var airshipComponents = gameObject.GetComponentsInChildren<AirshipBehaviourRoot>(includeInactive);

        foreach (var airshipComponent in airshipComponents) {
            var unityInstanceId = airshipComponent.Id;
            foreach (var binding in airshipComponent.GetComponents<ScriptBinding>()) {
                binding.InitEarly();
                if (!binding.IsAirshipComponent) continue;

                var componentName = binding.GetAirshipComponentName();
                if (componentName != typeName) continue;

                var componentId = binding.GetAirshipComponentId();

                LuauPlugin.LuauPushAirshipComponent(context, thread, unityInstanceId, componentId);
                return 1;
            }
        }

        return PushNil(thread);
    }

    public static int GetAirshipComponentsInChildren(LuauContext context, IntPtr thread, GameObject gameObject, string typeName, bool includeInactive) {
        var foundComponents = false;

        // Attempt to initialize any uninitialized bindings first:
        var scriptBindings = gameObject.GetComponentsInChildren<ScriptBinding>();
        foreach (var binding in scriptBindings) {
            // Side effect loads the components if found. No need for its return result here.
            GetAirshipBehaviourRoot(binding.gameObject);
        }
        
        var airshipComponents = gameObject.GetComponentsInChildren<AirshipBehaviourRoot>(includeInactive);
        
        var first = true;
        foreach (var airshipComponent in airshipComponents) {
            var hasAny = false;
            
            foreach (var binding in airshipComponent.GetComponents<ScriptBinding>()) {
                binding.InitEarly();
                if (!binding.IsAirshipComponent) continue;

                var componentName = binding.GetAirshipComponentName();
                if (componentName != typeName) continue;

                var componentId = binding.GetAirshipComponentId();

                if (!hasAny) {
                    hasAny = true;
                    ComponentIds.Clear();
                }
                ComponentIds.Add(componentId);
            }

            if (hasAny) {
                LuauPlugin.LuauPushAirshipComponents(context, thread, airshipComponent.Id, ComponentIds.ToArray(), !first);
                ComponentIds.Clear();
                first = false;
                foundComponents = true;
            }
        }
        
        if (foundComponents) {
            return 1;
        }

        return PushEmptyTable(thread);
    }

    public static int AddAirshipComponent(LuauContext context, IntPtr thread, GameObject gameObject, string componentName) {
        if (componentName == null) {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: AddAirshipComponent takes a parameter");
            return 0;
        }

        var buildInfo = AirshipBuildInfo.Instance;
        if (!buildInfo.HasAirshipBehaviourClass(componentName)) {
            ThreadDataManager.Error(thread);
            Debug.LogError($"Error: AddAirshipComponent - Airship component \"{componentName}\" not found");
            return 0;
        }
        
        var binding = gameObject.AddComponent<ScriptBinding>();
        var path = buildInfo.GetScriptPath(componentName);
        binding.SetScriptFromPath($"Assets/{path}", context, true);
        
        var airshipComponent = GetAirshipBehaviourRoot(gameObject);
        if (airshipComponent == null) {
            ThreadDataManager.Error(thread);
            Debug.LogError($"Error: AddAirshipComponent - Failed to add \"{componentName}\"");
            return 0;
        }
        
        var componentId = binding.GetAirshipComponentId();
        var unityInstanceId = airshipComponent.Id;
        LuauPlugin.LuauPushAirshipComponent(context, thread, unityInstanceId, componentId);

        return 1;
    }

    public static int BypassIfTypeStringIsAllowed(string typeName, LuauContext context, IntPtr thread) {
        if (ReflectionList.IsAllowedFromString(typeName, context)) return -1;
        
        ThreadDataManager.Error(thread);
        Debug.LogError($"[Airship] Access denied. Component type \"{typeName}\" not allowed from {context} context");
        return 0;
    }
}
