
using System;
using System.Collections.Generic;
using UnityEngine;
using Luau;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

[LuauAPI]
public class GameObjectAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(UnityEngine.GameObject);
    }

    private static int GetTagProperty(LuauContext context, IntPtr thread, GameObject gameObject) {
        var runtimeTag = gameObject.tag;
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (!gameConfig || !gameConfig.TryGetUserTag(runtimeTag, out var userTag)) return -1;
        
        Debug.Log($"Translating runtime tag '{runtimeTag}' to user tag '{userTag}'");
        LuauCore.WritePropertyToThread(thread, userTag, typeof(string));
        return 1;

    }

    private static int SetTagProperty(LuauContext context, IntPtr thread, GameObject gameObject, string value) {
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (!gameConfig || !gameConfig.TryGetRuntimeTag(value, out var runtimeTag)) return -1;
        
        Debug.Log($"Translating user tag '{value}' to runtime tag '{runtimeTag}'");
        gameObject.tag = runtimeTag;
        return 0;

    }

    private static int FindWithTag(LuauContext context, IntPtr thread, string tag) {
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (gameConfig && gameConfig.TryGetRuntimeTag(tag, out var runtimeTag)) {
            var go = GameObject.FindWithTag(runtimeTag);
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }

        return -1;
    }

    private static int CompareTag(LuauContext context, IntPtr thread, GameObject gameObject, string tag) {
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (gameConfig && gameConfig.TryGetRuntimeTag(tag, out var runtimeTag)) {
            LuauCore.WritePropertyToThread(thread, gameObject.CompareTag(runtimeTag), typeof(bool));
            return 1;
        }
        
        return -1;
    }
    
    private static int FindGameObjectsWithTag(LuauContext context, IntPtr thread, string tag) {
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (gameConfig && gameConfig.TryGetRuntimeTag(tag, out var runtimeTag)) {
            var go = GameObject.FindGameObjectsWithTag(runtimeTag);
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject[]));
            return 1;
        }

        return -1;
    }

    public override int OverrideMemberGetter(LuauContext context, IntPtr thread, object targetObject, string getterName) {
#if AIRSHIP_PLAYER
        if (getterName == "tag") {
            return GetTagProperty(context, thread, (GameObject)targetObject);
        }
#endif
        
        return -1;
    }

    public override int OverrideMemberSetter(LuauContext context, IntPtr thread, object targetObject, string setterName, LuauCore.PODTYPE dataType, IntPtr dataPtr,
        int dataPtrSize) {
#if AIRSHIP_PLAYER
        if (setterName == "tag") {
            var gameObject = (GameObject)targetObject;
            var value = LuauCore.GetPropertyAsString(dataType, dataPtr);
            return SetTagProperty(context, thread, gameObject, value);
        }
#endif
        
        return -1;
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (methodName == "Create") {
            string name = "lua_created_gameobject";
            if (numParameters >= 1) {
                name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
            }
            var go = new GameObject(name);
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        if (methodName == "CreateAtPos") {
            string name = "lua_created_gameobject";

            var pos = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            if (numParameters >= 2)
            {
                name = LuauCore.GetParameterAsString(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
            }
            var go = new GameObject(name);
            go.transform.position = pos;
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        if (methodName == "DestroyImmediate") {
            GameObject go = (GameObject)LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, thread);
            if (go)
            {
                Object.DestroyImmediate(go);
            }
            return 0;
        }

        // Runtime tags
#if AIRSHIP_PLAYER
        if (methodName == "FindWithTag" || methodName == "FindGameObjectWithTag") {
            var tag = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            var result = FindWithTag(context, thread, tag);
            if (result != -1) return result;
        }

        if (methodName == "FindGameObjectsWithTag") {
            var tag = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            var result = FindGameObjectsWithTag(context, thread, tag);
            if (result != -1) return result;
        }
#endif
        
        if (methodName == "FindObjectsByType") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: FindObjectsByType takes a string parameter");
                return 0;
            }

            var sortMode = FindObjectsSortMode.None;
            var findObjectsInactive = FindObjectsInactive.Exclude;
            var useFindObjectsInactive = false;
            if (numParameters == 2) {
                sortMode = (FindObjectsSortMode)LuauCore.GetParameterAsInt(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            } else if (numParameters == 3) {
                useFindObjectsInactive = true;
                findObjectsInactive = (FindObjectsInactive)LuauCore.GetParameterAsInt(2, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            } else {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: FindObjectsByType expected 1 or 2 parameters");
                return 0;
            }

            if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;

            var objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
            Object[] objects;
            if (useFindObjectsInactive) {
                objects = Object.FindObjectsByType(objectType, findObjectsInactive, sortMode);
            } else {
                objects = Object.FindObjectsByType(objectType, sortMode);
            }
            LuauCore.WritePropertyToThread(thread, objects, typeof(Object[]));
            return 1;
        }

        return -1;
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
#if AIRSHIP_PLAYER
        if (methodName == "CompareTag") {
            var tag = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            var compareTagResult = CompareTag(context, thread, (GameObject)targetObject, tag);
            if (compareTagResult != -1) return compareTagResult;
        }
#endif
        
        if (methodName == "GetAirshipComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            return AirshipBehaviourHelper.GetAirshipComponent(context, thread, (GameObject)targetObject, typeName);
        }

        if (methodName == "GetAirshipComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            return AirshipBehaviourHelper.GetAirshipComponents(context, thread, (GameObject)targetObject, typeName);
        }

        if (methodName == "GetAirshipComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, out var _);
            
            return AirshipBehaviourHelper.GetAirshipComponentInChildren(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }
        
        if (methodName == "GetAirshipComponentInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, out var exists);
            
            return AirshipBehaviourHelper.GetAirshipComponentInParent(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }
        
        if (methodName == "GetAirshipComponentsInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, out var exists);
            
            return AirshipBehaviourHelper.GetAirshipComponentsInParent(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }

        if (methodName == "GetAirshipComponentsInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, out var exists);

            return AirshipBehaviourHelper.GetAirshipComponentsInChildren(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }

        if (methodName == "AddAirshipComponent") {
            var componentName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

            return AirshipBehaviourHelper.AddAirshipComponent(context, thread, (GameObject)targetObject, componentName);
        }

        if (methodName == "GetComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
        }

        if (methodName == "GetComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes, out _);
            
            var componentTypeResult =
                AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
            if (componentTypeResult != 1) return componentTypeResult;
            
            var gameObject = (GameObject)targetObject;
            var unityChildComponent = gameObject.GetComponentInChildren(componentType, includeInactive);
            LuauCore.WritePropertyToThread(thread, unityChildComponent, unityChildComponent != null ? unityChildComponent.GetType() : null);
            return 1;
        }

        if (methodName == "GetComponentInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes, out _);

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

        if (methodName == "GetComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var componentTypeResult =
                AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
            if (componentTypeResult != 1) return componentTypeResult;
            
            var gameObject = (GameObject)targetObject;
            var unityComponents = gameObject.GetComponents(componentType);
            LuauCore.WritePropertyToThread(thread, unityComponents, unityComponents?.GetType());
            return 1;
        }
        
        if (methodName == "GetComponentIfExists") {
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentIfExists takes a parameter");
                return 0;
            }
            
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
        
        if (methodName == "GetComponentsInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInChildren takes a string parameter.");
                return 0;
            }
            
            if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;
            
            var gameObject = (GameObject)targetObject;
            
            Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInChildren component type not found: " + typeName + " (consider registering it?)");
                return 0;
            }
            var results = gameObject.GetComponentsInChildren(objectType);
            LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
            return 1;
        }
        
        if (methodName == "GetComponentsInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInParent takes a string parameter.");
                return 0;
            }
            
            if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread) == 0) return 0;
            
            var gameObject = (GameObject)targetObject;
            
            Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInParent component type not found: " + typeName + " (consider registering it?)");
                return 0;
            }
            var results = gameObject.GetComponentsInParent(objectType);
            LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
            return 1;
        }

        if (methodName == "AddComponent") {
            string param0 = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

            if (param0 == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddComponent takes a parameter");
                return 0;
            }
            
            if (AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(param0, context, thread) == 0) return 0;
            
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

            Type objectType = LuauCore.CoreInstance.GetTypeFromString(param0);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddComponent component type not found: " + param0 + " (add [LuauAPI] to class for auto registration)");
                return 0;
            }
            object newObject = gameObject.AddComponent(objectType);
            LuauCore.WritePropertyToThread(thread, newObject, objectType);
            return 1;
        }

        if (methodName == "OnUpdate") {
            if (numParameters != 1) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnUpdate parameter must be a function");
                return 0;
            }

           
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnUpdateHandle(context, thread, handle, gameObject);

            return 1;
        }
        
        if (methodName == "OnLateUpdate") {
            if (numParameters != 1) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnLateUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnLateUpdate parameter must be a function");
                return 0;
            }

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnLateUpdateHandle(context, thread, handle, gameObject);
            return 1;
        }
        
        if (methodName == "OnFixedUpdate") {
            if (numParameters != 1) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnFixedUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnFixedUpdate parameter must be a function");
                return 0;
            }

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnFixedUpdateHandle(context, thread, handle, gameObject);
            return 1;
        }

        if (methodName == "ClearChildren") {
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            foreach (Transform transform in gameObject.transform) {
                UnityEngine.GameObject.Destroy(transform.gameObject);
            }

            return 0;
        }

        if (methodName == "SetLayerRecursive") {
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            int layer = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            SetLayerRecursive(gameObject.transform, layer, ~0);
            return 0;
        }
        if (methodName == "ReplaceLayerRecursive") {
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            int layer = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            int replaceMask = LuauCore.GetParameterAsInt(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            SetLayerRecursive(gameObject.transform, layer, replaceMask);
            return 0;
        }

        return -1;
    }

    private void SetLayerRecursive(Transform transform, int layer, int replaceMask) {
        if ((replaceMask & (1 << transform.gameObject.layer)) != 0) {
            transform.gameObject.layer = layer;
        }

        foreach (Transform child in transform) {
            SetLayerRecursive(child, layer, replaceMask);
        }
    }
}