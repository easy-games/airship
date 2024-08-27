
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
        //if (Application.isEditor) return -1;
        
        var tag = gameObject.tag;
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        
        if (gameConfig && gameConfig.TryGetUserTag(tag, out var userTag)) {
            // push user tag
            LuauCore.WritePropertyToThread(thread, userTag, typeof(string));
            return 1;
        }

        return -1;
    }

    private static int SetTagProperty(LuauContext context, IntPtr thread, GameObject gameObject, string value) {
        //if (Application.isEditor) return -1;
        
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (gameConfig) {
            var tagList = UnityEditorInternal.InternalEditorUtility.tags;
            Debug.Log($"Unity tags: [ {string.Join(", ", tagList)} ]; user tags: [ {string.Join(", ", gameConfig.gameTags)} ]");

            if (gameConfig.TryGetRuntimeTag(value, out var runtimeTag)) {
                Debug.Log($"Translate Tag '{value}' -> '{runtimeTag}'");
                value = runtimeTag;
                gameObject.tag = value;
                return 0;
            }
        }

        return -1;
    }

    public override int OverrideMemberGetter(LuauContext context, IntPtr thread, object targetObject, string getterName) {
        if (getterName == "tag") {
            return GetTagProperty(context, thread, (GameObject)targetObject);
        }
        
        return -1;
    }

    public override int OverrideMemberSetter(LuauContext context, IntPtr thread, object targetObject, string setterName, LuauCore.PODTYPE dataType, IntPtr dataPtr,
        int dataPtrSize) {
        
        if (setterName == "tag") {
            var gameObject = (GameObject)targetObject;
            var value = LuauCore.GetPropertyAsString(dataType, dataPtr);
            return SetTagProperty(context, thread, gameObject, value);
        }
        
        return -1;
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {
        if (methodName == "Create") {
            string name = "lua_created_gameobject";
            if (numParameters >= 1) {
                name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            }
            var go = new GameObject(name);
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        if (methodName == "CreateAtPos") {
            string name = "lua_created_gameobject";

            var pos = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);

            if (numParameters >= 2)
            {
                name = LuauCore.GetParameterAsString(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            }
            var go = new GameObject(name);
            go.transform.position = pos;
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        if (methodName == "DestroyImmediate") {
            GameObject go = (GameObject)LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, thread);
            if (go)
            {
                Object.DestroyImmediate(go);
            }
            return 0;
        }

        if (methodName == "FindObjectsByType") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: FindObjectsByType takes a string parameter");
                return 0;
            }

            var sortMode = FindObjectsSortMode.None;
            var findObjectsInactive = FindObjectsInactive.Exclude;
            var useFindObjectsInactive = false;
            if (numParameters == 2) {
                sortMode = (FindObjectsSortMode)LuauCore.GetParameterAsInt(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            } else if (numParameters == 3) {
                useFindObjectsInactive = true;
                findObjectsInactive = (FindObjectsInactive)LuauCore.GetParameterAsInt(2, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
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

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {
        if (methodName == "GetAirshipComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            return AirshipBehaviourHelper.GetAirshipComponent(context, thread, (GameObject)targetObject, typeName);
        }

        if (methodName == "GetAirshipComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            return AirshipBehaviourHelper.GetAirshipComponents(context, thread, (GameObject)targetObject, typeName);
        }

        if (methodName == "GetAirshipComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, out var exists);
            
            return AirshipBehaviourHelper.GetAirshipComponentInChildren(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }
        
        if (methodName == "GetAirshipComponentInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, out var exists);
            
            return AirshipBehaviourHelper.GetAirshipComponentInParent(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }
        
        if (methodName == "GetAirshipComponentsInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, out var exists);
            
            return AirshipBehaviourHelper.GetAirshipComponentsInParent(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }

        if (methodName == "GetAirshipComponentsInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, out var exists);

            return AirshipBehaviourHelper.GetAirshipComponentsInChildren(context, thread, (GameObject)targetObject, typeName, includeInactive);
        }

        if (methodName == "AddAirshipComponent") {
            var componentName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            return AirshipBehaviourHelper.AddAirshipComponent(context, thread, (GameObject)targetObject, componentName);
        }

        if (methodName == "GetComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
        }

        if (methodName == "GetComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var componentTypeResult =
                AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
            if (componentTypeResult != 1) return componentTypeResult;
            
            var gameObject = (GameObject)targetObject;
            var unityChildComponent = gameObject.GetComponentInChildren(componentType);
            LuauCore.WritePropertyToThread(thread, unityChildComponent, unityChildComponent.GetType());
            return 1;
        }

        if (methodName == "GetComponentInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var componentTypeResult =
                AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
            if (componentTypeResult != 1) return componentTypeResult;
            
            var gameObject = (GameObject)targetObject;
            var unityParentComponent = gameObject.GetComponentInParent(componentType);

            if (unityParentComponent != null) {
                LuauCore.WritePropertyToThread(thread, unityParentComponent, unityParentComponent.GetType());
            }
            else {
                LuauCore.WritePropertyToThread(thread, null, null);
            }

            return 1;
        }

        if (methodName == "GetComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
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
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
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
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
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
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
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
            string param0 = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

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

           
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

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

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            
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

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

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

        return -1;
    }
}