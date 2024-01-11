
using System;
using System.Collections.Generic;
using UnityEngine;
using Luau;
using Object = UnityEngine.Object;

[LuauAPI]
public class GameObjectAPI : BaseLuaAPIClass
{
    private readonly List<int> componentIds = new();

    private static LuauAirshipComponent GetAirshipComponent(GameObject gameObject) {
        var airshipComponent = gameObject.GetComponent<LuauAirshipComponent>();
        if (airshipComponent == null) {
            // See if it just needs to be started first:
            var foundAny = false;
            foreach (var binding in gameObject.GetComponents<ScriptBinding>()) {
                foundAny = true;
                binding.InitEarly();
            }
                
            // Retry getting LuauAirshipComponent:
            if (foundAny) {
                airshipComponent = gameObject.GetComponent<LuauAirshipComponent>();
            }
        }

        return airshipComponent;
    }

    public override Type GetAPIType()
    {
        return typeof(UnityEngine.GameObject);
    }

    public override int OverrideStaticMethod(IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "Create")
        {
            string name = "lua_created_gameobject";
            if (numParameters >= 1)
            {
                name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            }
            var go = new GameObject(name);
            LuauCore.WritePropertyToThread(thread, go, typeof(GameObject));
            return 1;
        }
        if (methodName == "CreateAtPos")
        {
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
        if (methodName == "Destroy")
        {
            GameObject go = (GameObject)LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, thread);
            if (go)
            {
                Object.Destroy(go);
            }
            return 0;
        }
        if (methodName == "DestroyImmediate")
        {
            GameObject go = (GameObject)LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, thread);
            if (go)
            {
                Object.DestroyImmediate(go);
            }
            return 0;
        }
        return -1;
    }

    public override int OverrideMemberMethod(IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "GetComponent")
        {
            // Attempt to push Lua airship component first:
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var gameObject = (GameObject)targetObject;
            var airshipComponent = GetAirshipComponent(gameObject);
            if (airshipComponent == null) {
                return -1;
            }

            var unityInstanceId = airshipComponent.Id;
            foreach (var binding in gameObject.GetComponents<ScriptBinding>()) {
                binding.InitEarly();
                if (!binding.IsAirshipComponent) continue;
                
                var componentName = binding.GetAirshipComponentName();
                if (componentName != typeName) continue;

                var componentId = binding.GetAirshipComponentId();
                
                LuauPlugin.LuauPushAirshipComponent(thread, unityInstanceId, componentId);
                
                return 1;
            }
            
            // If Lua airship component is not found, return -1, which will default to the Unity GetComponent method:
            return -1;
        }

        if (methodName == "GetComponents")
        {
            // Attempt to push Lua airship components first:
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var gameObject = (GameObject)targetObject;
            var airshipComponent = GetAirshipComponent(gameObject);
            if (airshipComponent == null) {
                return -1;
            }
                
            var unityInstanceId = airshipComponent.Id;

            var hasAny = false;
            foreach (var binding in gameObject.GetComponents<ScriptBinding>())
            {
                if (!binding.IsAirshipComponent) continue;
                
                var componentName = binding.GetAirshipComponentName();
                if (componentName != typeName) continue;

                var componentId = binding.GetAirshipComponentId();
                
                // LuauPlugin.LuauPushAirshipComponent(thread, unityInstanceId, componentId);
                if (!hasAny)
                {
                    hasAny = true;
                    componentIds.Clear();
                }
                componentIds.Add(componentId);
            }

            if (hasAny)
            {
                LuauPlugin.LuauPushAirshipComponents(thread, unityInstanceId, componentIds.ToArray());
                return 1;
            }

            componentIds.Clear();
            LuauPlugin.LuauPushAirshipComponents(thread, unityInstanceId, componentIds.ToArray());
            return 1;

            // If Lua airship components are not found, return -1, which will default to the Unity GetComponents method:
            // return -1;
        }
        
        if (methodName == "GetComponentIfExists") {
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentIfExists takes a parameter");
                return 0;
            }
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

            Type objectType = LuauCore.Instance.GetTypeFromString(typeName);
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
            var gameObject = (GameObject)targetObject;

            // Airship Behaviours
            {
                var foundComponents = false;

                // Attempt to initialize any uninitialized bindings first:
                var scriptBindings = gameObject.GetComponentsInChildren<ScriptBinding>();
                foreach (var binding in scriptBindings) {
                    // GetAirshipComponent side-effect loads the components if found. No need for its return result here.
                    GetAirshipComponent(binding.gameObject);
                }
                
                var airshipComponents = gameObject.GetComponentsInChildren<LuauAirshipComponent>();
                
                var first = true;
                foreach (var airshipComponent in airshipComponents) {
                    var hasAny = false;
                    
                    foreach (var binding in airshipComponent.GetComponents<ScriptBinding>()) {
                        if (!binding.IsAirshipComponent) continue;

                        var componentName = binding.GetAirshipComponentName();
                        if (componentName != typeName) continue;

                        var componentId = binding.GetAirshipComponentId();

                        if (!hasAny) {
                            hasAny = true;
                            componentIds.Clear();
                        }
                        componentIds.Add(componentId);
                    }

                    if (hasAny) {
                        LuauPlugin.LuauPushAirshipComponents(thread, airshipComponent.Id, componentIds.ToArray(), !first);
                        componentIds.Clear();
                        first = false;
                        foundComponents = true;
                    }
                }
                if (foundComponents) {
                    return 1;
                }
            }

            Type objectType = LuauCore.Instance.GetTypeFromString(typeName);
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

        if (methodName == "AddComponent")
        {
             
            string param0 = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            if (param0 == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddComponent takes a parameter");
                return 0;
            }
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;

            Type objectType = LuauCore.Instance.GetTypeFromString(param0);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: AddComponent component type not found: " + param0 + " (add [LuaiAPI] to class for auto registration)");
                return 0;
            }
            object newObject = gameObject.AddComponent(objectType);
            LuauCore.WritePropertyToThread(thread, newObject, objectType);
            return 1;
        }

        if (methodName == "OnUpdate")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnUpdate parameter must be a function");
                return 0;
            }

           
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnUpdateHandle(thread, handle, gameObject);

            return 1;
        }
        if (methodName == "OnLateUpdate")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnLateUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnLateUpdate parameter must be a function");
                return 0;
            }

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnLateUpdateHandle(thread, handle, gameObject);
            return 1;
        }
        if (methodName == "OnFixedUpdate")
        {
            if (numParameters != 1)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnFixedUpdate takes 1 parameter");
                return 0;
            }
            if (parameterDataPODTypes[0] != (int)LuauCore.PODTYPE.POD_LUAFUNCTION)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: OnFixedUpdate parameter must be a function");
                return 0;
            }

            
            int handle = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            ThreadDataManager.SetOnFixedUpdateHandle(thread, handle, gameObject);
            return 1;
        }

        if (methodName == "ClearChildren")
        {
            UnityEngine.GameObject gameObject = (UnityEngine.GameObject)targetObject;
            foreach (Transform transform in gameObject.transform)
            {
                UnityEngine.GameObject.Destroy(transform.gameObject);
            }

            return 0;
        }

        return -1;
    }
}