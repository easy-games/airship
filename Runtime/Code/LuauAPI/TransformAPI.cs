using System;
using System.Collections.Generic;
using Luau;
using UnityEngine;

[LuauAPI]
public class TransformAPI : BaseLuaAPIClass
{
    private readonly List<int> componentIds = new();

    private static LuauAirshipComponent GetAirshipComponent(Transform transform) {
        var airshipComponent = transform.GetComponent<LuauAirshipComponent>();
        if (airshipComponent == null) {
            // See if it just needs to be started first:
            var foundAny = false;
            foreach (var binding in transform.GetComponents<ScriptBinding>()) {
                foundAny = true;
                binding.InitEarly();
            }
                
            // Retry getting LuauAirshipComponent:
            if (foundAny) {
                airshipComponent = transform.GetComponent<LuauAirshipComponent>();
            }
        }

        return airshipComponent;
    }

    public override Type GetAPIType()
    {
        return typeof(Transform);
    }

    public override int OverrideMemberMethod(
        IntPtr thread,
        object targetObject,
        string methodName,
        int numParameters,
        int[] parameterDataPODTypes,
        IntPtr[] parameterDataPtrs,
        int[] paramaterDataSizes) 
    { 

        if (methodName == "GetComponent")
        {
            // Attempt to push Lua airship component first:
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var t = (Transform)targetObject;
            var airshipComponent = GetAirshipComponent(t);
            if (airshipComponent == null) {
                return -1;
            }

            var unityInstanceId = airshipComponent.Id;
            foreach (var binding in t.GetComponents<ScriptBinding>()) {
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
        
        if (methodName == "GetComponentInChildren") {
            // Attempt to push Lua airship component first:
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes, out var exists);

            var gameObject = (GameObject)targetObject;

            // Attempt to initialize any uninitialized bindings first:
            var scriptBindings = gameObject.GetComponentsInChildren<ScriptBinding>();
            foreach (var binding in scriptBindings) {
                // GetAirshipComponent side-effect loads the components if found. No need for its return result here.
                GetAirshipComponent(binding.transform);
            }
                
            var airshipComponent = gameObject.GetComponentInChildren<LuauAirshipComponent>(includeInactive);

            var unityInstanceId = airshipComponent.Id;
            foreach (var binding in airshipComponent.GetComponents<ScriptBinding>()) {
                binding.InitEarly();
                if (!binding.IsAirshipComponent) continue;
                
                var componentName = binding.GetAirshipComponentName();
                if (componentName != typeName) continue;

                var componentId = binding.GetAirshipComponentId();
                
                LuauPlugin.LuauPushAirshipComponent(thread, unityInstanceId, componentId);
                
                return 1;
            }
            
            // If Lua airship component is not found, return -1, which will default to the Unity GetComponentInChildren method:
            return -1;
        }

        if (methodName == "GetComponents") {
            // Attempt to push Lua airship components first:
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var t = (Transform)targetObject;
            var airshipComponent = GetAirshipComponent(t);
            if (airshipComponent != null) {
                var unityInstanceId = airshipComponent.Id;

                var hasAny = false;
                foreach (var binding in t.GetComponents<ScriptBinding>())
                {
                    if (!binding.IsAirshipComponent) continue;

                    var componentName = binding.GetAirshipComponentName();
                    if (componentName != typeName) continue;

                    var componentId = binding.GetAirshipComponentId();

                    // LuauPlugin.LuauPushAirshipComponent(thread, unityInstanceId, componentId);
                    if (!hasAny)
                    {
                        hasAny = true;
                        this.componentIds.Clear();
                    }
                    this.componentIds.Add(componentId);
                }

                if (hasAny)
                {
                    LuauPlugin.LuauPushAirshipComponents(thread, unityInstanceId, this.componentIds.ToArray());
                    return 1;
                }
            }

            /*
             * Done searching for AirshipBehaviours. Now we look for Unity Components
             */
            var componentType = LuauCore.Instance.GetTypeFromString(typeName);
            if (componentType == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: Unknown type \"" + typeName + "\". If this is a C# type please report it. There is a chance we forgot to add to allow list.");
                this.componentIds.Clear();
                return 0;
            }
            var unityComponents = t.GetComponents(componentType);
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
            Transform gameObject = (Transform)targetObject;

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
            Transform t = (Transform)targetObject;
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInChildren takes a string parameter.");
                return 0;
            }

            // Airship Behaviours
            {
                var foundComponents = false;
                
                // Attempt to initialize any uninitialized bindings first:
                var scriptBindings = t.GetComponentsInChildren<ScriptBinding>();
                foreach (var binding in scriptBindings) {
                    // GetAirshipComponent side-effect loads the components if found. No need for its return result here.
                    GetAirshipComponent(binding.transform);
                }
                
                var airshipComponents = t.GetComponentsInChildren<LuauAirshipComponent>();
                
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
            var results = t.GetComponentsInChildren(objectType);
            LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
            return 1;
        }

        if (methodName == "ClampRotationY" && numParameters == 2) {
            float targetY = LuauCore.GetParameterAsFloat(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);
            float maxAngle = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);

            //Clamp the rotation so the spine doesn't appear broken
            Transform t = (Transform)targetObject;
            var localEulerAngles = t.localEulerAngles;
            localEulerAngles = new Vector3(localEulerAngles.x, MathUtil.ClampAngle(targetY, -maxAngle, maxAngle), localEulerAngles.z);
            t.localEulerAngles = localEulerAngles;

            return 0;
        }
        
        return -1; }
}