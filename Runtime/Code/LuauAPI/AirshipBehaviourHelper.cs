using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Luau;
using UnityEngine;
using UnityEngine.Profiling;

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
    
    private static int GetAirshipBehaviourRootId(GameObject gameObject) {
        if (!AirshipBehaviourRootV2.HasId(gameObject)) {
            // See if it just needs to be started first:
            foreach (var component in gameObject.GetComponents<AirshipComponent>()) {
                Profiler.BeginSample("InitComponent");
                component.Init();
                Profiler.EndSample();
            }
        }

        if (!AirshipBehaviourRootV2.HasId(gameObject)) {
            return -1;
        }

        return AirshipBehaviourRootV2.GetId(gameObject);
    }

    private static bool IsTypeOrInheritingType(AirshipComponent airshipComponent, string typeName, string targetTypeScriptPath) {
        Profiler.BeginSample("IsTypeOrInheritingType");
        var componentName = airshipComponent.GetAirshipComponentName();
        
        if (componentName == typeName) {
            Profiler.EndSample();
            return true;
        }

        var buildInfo = AirshipBuildInfo.Instance;
        if (!buildInfo) {
            Profiler.EndSample();
            return false;
        }

        if (!airshipComponent.script) {
            Debug.LogWarning($"Airship Component is missing script at path: {airshipComponent.scriptPath}.");
            Profiler.EndSample();
            return false;
        }

        // Check inheritance if possible
        var result = targetTypeScriptPath != null && buildInfo.Inherits(airshipComponent.script, targetTypeScriptPath);
        Profiler.EndSample();
        return result;
    }
    
    public static int GetAirshipComponent(LuauContext context, IntPtr thread, GameObject gameObject, string typeName) {
        Profiler.BeginSample("GetBehaviorRootId");
        var unityInstanceId = GetAirshipBehaviourRootId(gameObject);
        Profiler.EndSample();
        if (unityInstanceId == -1) {
            return PushNil(thread);
        }

        var buildInfo = AirshipBuildInfo.Instance;
        var targetTypeScriptPath = buildInfo ? buildInfo.GetScriptPathByTypeName(typeName) : null;

        foreach (var airshipComponent in gameObject.GetComponents<AirshipComponent>()) {
            Profiler.BeginSample("InitComponent");
            airshipComponent.Init();
            Profiler.EndSample();
            if (!IsTypeOrInheritingType(airshipComponent, typeName, targetTypeScriptPath)) continue;

            var componentId = airshipComponent.GetAirshipComponentId();

            LuauPlugin.LuauPushAirshipComponent(context, thread, unityInstanceId, componentId);
            return 1;
        }

        return PushNil(thread);
    }
    
    public static int GetAirshipComponents(LuauContext context, IntPtr thread, GameObject gameObject, string typeName) {
        var unityInstanceId = GetAirshipBehaviourRootId(gameObject);
        if (unityInstanceId != -1) {

            var buildInfo = AirshipBuildInfo.Instance;
            var targetTypeScriptPath = buildInfo ? buildInfo.GetScriptPathByTypeName(typeName) : null;
            
            var hasAny = false;
            foreach (var airshipComponent in gameObject.GetComponents<AirshipComponent>()) {
                airshipComponent.Init();
                if (!IsTypeOrInheritingType(airshipComponent, typeName, targetTypeScriptPath)) continue;

                var componentId = airshipComponent.GetAirshipComponentId();

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
        foreach (var airshipComponent in gameObject.GetComponentsInChildren<AirshipComponent>()) {
            // Side effect loads the components if found. No need for its return result here.
            GetAirshipBehaviourRootId(airshipComponent.gameObject);
        }
        
        var airshipComponents = gameObject.GetComponentsInChildren<AirshipComponent>(includeInactive);
        var buildInfo = AirshipBuildInfo.Instance;
        var targetTypeScriptPath = buildInfo ? buildInfo.GetScriptPathByTypeName(typeName) : null;
        
        foreach (var airshipComponent in airshipComponents) {
            airshipComponent.Init();
            if (!IsTypeOrInheritingType(airshipComponent, typeName, targetTypeScriptPath)) continue;

            var componentId = airshipComponent.GetAirshipComponentId();

            LuauPlugin.LuauPushAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(airshipComponent), componentId);
            return 1;
        }

        return PushNil(thread);
    }

    public static int GetAirshipComponentInParent(LuauContext context, IntPtr thread, GameObject gameObject,
        string typeName, bool includeInactive) {
        
        foreach (var airshipComponent in gameObject.GetComponentsInParent<AirshipComponent>()) {
            // Side effect loads the components if found. No need for its return result here.
            GetAirshipBehaviourRootId(airshipComponent.gameObject);
        }
        
        var airshipComponents = gameObject.GetComponentsInParent<AirshipComponent>(includeInactive);
        var buildInfo = AirshipBuildInfo.Instance;
        var targetTypeScriptPath = buildInfo ? buildInfo.GetScriptPathByTypeName(typeName) : null;
        
        foreach (var airshipComponent in airshipComponents) {
            airshipComponent.Init();
            if (!IsTypeOrInheritingType(airshipComponent, typeName, targetTypeScriptPath)) continue;

            var componentId = airshipComponent.GetAirshipComponentId();

            LuauPlugin.LuauPushAirshipComponent(context, thread, AirshipBehaviourRootV2.GetId(airshipComponent), componentId);
            return 1;
        }

        return PushNil(thread);
    }

    public static int GetAirshipComponentsInChildren(LuauContext context, IntPtr thread, GameObject gameObject, string typeName, bool includeInactive) {
        // Attempt to initialize any uninitialized bindings first:
        foreach (var airshipComponent in gameObject.GetComponentsInChildren<AirshipComponent>()) {
            // Side effect loads the components if found. No need for its return result here.
            GetAirshipBehaviourRootId(airshipComponent.gameObject);
        }
        
        var airshipComponents = gameObject.GetComponentsInChildren<AirshipComponent>(includeInactive);
        var buildInfo = AirshipBuildInfo.Instance;
        var targetTypeScriptPath = buildInfo ? buildInfo.GetScriptPathByTypeName(typeName) : null;

        var componentIdsByUnityInstanceIds = new Dictionary<int, List<int>>();

        foreach (var airshipComponent in airshipComponents) {
            airshipComponent.Init();
            if (!IsTypeOrInheritingType(airshipComponent, typeName, targetTypeScriptPath)) continue;

            var componentId = airshipComponent.GetAirshipComponentId();
            var unityInstanceId = AirshipBehaviourRootV2.GetId(airshipComponent);
            if (!componentIdsByUnityInstanceIds.ContainsKey(unityInstanceId)) {
                componentIdsByUnityInstanceIds.Add(unityInstanceId, new List<int>());
            }
            componentIdsByUnityInstanceIds[unityInstanceId].Add(componentId);
        }

        if (componentIdsByUnityInstanceIds.Count > 0) {
            var first = true;
            foreach (var (unityInstanceId, componentIds) in componentIdsByUnityInstanceIds) {
                LuauPlugin.LuauPushAirshipComponents(context, thread, unityInstanceId, componentIds.ToArray(), !first);
                first = false;
            }

            return 1;
        }

        return PushEmptyTable(thread);
    }
    
    public static int GetAirshipComponentsInParent(LuauContext context, IntPtr thread, GameObject gameObject, string typeName, bool includeInactive) {
        var foundComponents = false;

        // Attempt to initialize any uninitialized bindings first:
        foreach (var airshipComponent in gameObject.GetComponentsInParent<AirshipComponent>()) {
            // Side effect loads the components if found. No need for its return result here.
            GetAirshipBehaviourRootId(airshipComponent.gameObject);
        }
        
        var airshipComponents = gameObject.GetComponentsInParent<AirshipComponent>(includeInactive);
        var buildInfo = AirshipBuildInfo.Instance;
        var targetTypeScriptPath = buildInfo ? buildInfo.GetScriptPathByTypeName(typeName) : null;
        
        var componentIdsByUnityInstanceIds = new Dictionary<int, List<int>>();

        foreach (var airshipComponent in airshipComponents) {
            airshipComponent.Init();
            if (!IsTypeOrInheritingType(airshipComponent, typeName, targetTypeScriptPath)) continue;

            var componentId = airshipComponent.GetAirshipComponentId();
            var unityInstanceId = AirshipBehaviourRootV2.GetId(airshipComponent);
            if (!componentIdsByUnityInstanceIds.ContainsKey(unityInstanceId)) {
                componentIdsByUnityInstanceIds.Add(unityInstanceId, new List<int>());
            }
            componentIdsByUnityInstanceIds[unityInstanceId].Add(componentId);
        }

        if (componentIdsByUnityInstanceIds.Count > 0) {
            var first = true;
            foreach (var (unityInstanceId, componentIds) in componentIdsByUnityInstanceIds) {
                LuauPlugin.LuauPushAirshipComponents(context, thread, unityInstanceId, componentIds.ToArray(), !first);
                first = false;
            }

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
        if (buildInfo == null || !buildInfo.HasAirshipBehaviourClass(componentName)) {
            ThreadDataManager.Error(thread);
            Debug.LogError($"Error: AddAirshipComponent - Airship component \"{componentName}\" not found");
            return 0;
        }
        
        var path = buildInfo.GetScriptPath(componentName);
        AirshipComponent component;
        try {
            component = AirshipComponent.Create(gameObject, $"Assets/{path}", context);
        } catch (Exception e) {
            ThreadDataManager.Error(thread);
            Debug.LogException(e);
            return 0;
        }

        if (!AirshipBehaviourRootV2.HasId(gameObject)) {
            ThreadDataManager.Error(thread);
            Debug.LogError($"Error: AddAirshipComponent - Failed to add \"{componentName}\"");
            return 0;
        }
        
        var componentId = component.GetAirshipComponentId();
        var unityInstanceId = AirshipBehaviourRootV2.GetId(gameObject);
        LuauPlugin.LuauPushAirshipComponent(context, thread, unityInstanceId, componentId);

        return 1;
    }

    public static int BypassIfTypeStringIsAllowed(string typeName, LuauContext context, IntPtr thread) {
        if (ReflectionList.IsAllowedFromString(typeName, context)) return -1;
        
        ThreadDataManager.Error(thread);
        Debug.LogError($"[Airship] Access denied. Component type \"{typeName}\" not allowed from {context} context");
        return 0;
    }

    public static int GetTypeFromTypeName(string typeName, LuauContext context, IntPtr thread, out Type componentType) {
        if (BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) {
            componentType = null;
            return 0;
        }
        
        componentType = LuauCore.CoreInstance.GetTypeFromString(typeName);
        if (componentType == null) {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: Unknown type \"" + typeName + "\". If this is a C# type please report it. There is a chance we forgot to add to allow list.");
            return 0;
        }

        return 1;
    } 
}
