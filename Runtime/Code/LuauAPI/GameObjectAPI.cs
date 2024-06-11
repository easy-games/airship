
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
            return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
        }

        if (methodName == "GetComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var componentTypeResult =
                AirshipBehaviourHelper.GetTypeFromTypeName(typeName, context, thread, out var componentType);
            if (componentTypeResult != 1) return componentTypeResult;
            
            var gameObject = (GameObject)targetObject;
            var unityComponents = gameObject.GetComponents(componentType);
            LuauCore.WritePropertyToThread(thread, unityComponents, unityComponents.GetType());
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