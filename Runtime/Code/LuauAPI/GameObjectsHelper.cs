using System;
using Luau;
using UnityEngine;
using Object = System.Object;

public static class GameObjectsHelper {
    public static int GetComponent(LuauContext context, IntPtr thread, Object target, string typeName) {
        return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
    }
    
    public static int GetComponentInChildren(LuauContext context, IntPtr thread,  Object targetObject, string typeName, bool includeInactive) {
        var componentTypeResult =
            AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
        if (componentTypeResult != 1) return componentTypeResult;
            
        var gameObject = (GameObject)targetObject;
        var unityChildComponent = gameObject.GetComponentInChildren(componentType, includeInactive);
        LuauCore.WritePropertyToThread(thread, unityChildComponent, unityChildComponent != null ? unityChildComponent.GetType() : null);
        return 1;
    }
    
    public static int GetComponentInParent(LuauContext context, IntPtr thread,  Object targetObject, string typeName, bool includeInactive) {
        var componentTypeResult =
            AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
        if (componentTypeResult != 1) return componentTypeResult;
            
        var gameObject = (GameObject)targetObject;
        var unityParentComponent = gameObject.GetComponentInParent(componentType, includeInactive);

        if (unityParentComponent != null) {
            LuauCore.WritePropertyToThread(thread, unityParentComponent, unityParentComponent.GetType());
        }
        else {
            LuauCore.WritePropertyToThread(thread, null, null);
        }

        return 1;
    }
    
    public static int GetComponents(LuauContext context, IntPtr thread, Object targetObject, string typeName) {
        var componentTypeResult =
            AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
        if (componentTypeResult != 1) return componentTypeResult;
            
        var gameObject = (GameObject)targetObject;
        var unityComponents = gameObject.GetComponents(componentType);
        LuauCore.WritePropertyToThread(thread, unityComponents, unityComponents?.GetType());
        return 1;
    }

    public static int GetComponentsInChildren(LuauContext context, IntPtr thread, Object targetObject, string typeName, bool includeInactive) {
        if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;
            
        var gameObject = (GameObject)targetObject;
            
        Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
        if (objectType == null) {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: GetComponentsInChildren component type not found: " + typeName + " (consider registering it?)");
            return 0;
        }
            
        var results = gameObject.GetComponentsInChildren(objectType, includeInactive);
        LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
        return 1;
    }
    
    public static int GetComponentsInParent(LuauContext context, IntPtr thread, Object targetObject, string typeName, bool includeInactive) {
        if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;
            
        var gameObject = (GameObject)targetObject;
            
        Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
        if (objectType == null)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: GetComponentsInParent component type not found: " + typeName + " (consider registering it?)");
            return 0;
        }

        var results = gameObject.GetComponentsInParent(objectType, includeInactive);
        LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
        return 1;
    }

    public static int AddComponent(LuauContext context, IntPtr thread, Object targetObject, string typeName) {
        if (typeName == null)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: AddComponent takes a parameter");
            return 0;
        }
        
        if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;
            
        UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

        Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
        if (objectType == null)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: AddComponent component type not found: " + typeName + " (add [LuauAPI] to class for auto registration)");
            return 0;
        }
        object newObject = gameObject.AddComponent(objectType);
        LuauCore.WritePropertyToThread(thread, newObject, objectType);
        return 1;
    }
    
    public static int GetComponentIfExists(LuauContext context, IntPtr thread, Object targetObject, string typeName) {
        if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;
            
        UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

        Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
        if (objectType == null)
        {
            ThreadDataManager.Error(thread);
            Debug.LogError("Error: GetComponentIfExists component type not found: \"" + typeName + "\". Has it been registered in LuauCoreSystemNamespaces.cs?");
            return 0;
        }
        var newObject = gameObject.GetComponent(objectType);
        if (newObject != null)
        {
            LuauCore.WritePropertyToThread(thread, newObject, objectType);
            return 1;
        }

        LuauCore.WritePropertyToThread(thread, null, null);
        return 1;
    }
    
    public static bool HandleGameObjectMethods(LuauContext context, IntPtr thread, Object targetObject,
        string methodName, int numParameters, Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs,
        Span<int> parameterDataSizes, out int numReturnValues) {
        if (targetObject is Transform transform) targetObject = transform.gameObject; // ensure we're using GameObject
        
        if (methodName == "GetComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            numReturnValues = GetComponent(context, thread, targetObject, typeName);
            return true;
        } else if (methodName == "GetComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) {
                numReturnValues = -1;
                return false;
            }
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes, out _);

            numReturnValues = GetComponentInChildren(context, thread, targetObject, typeName, includeInactive);
            return true;
        } else if (methodName == "GetComponentInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) {
                numReturnValues = -1;
                return false;
            }

            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes, out _);
        }

        numReturnValues = -1;
        return false;
    }
}