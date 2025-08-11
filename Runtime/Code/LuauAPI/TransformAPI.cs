using System;
using System.Collections.Generic;
using System.Linq;
using Luau;
using UnityEngine;

[LuauAPI]
public class TransformAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Transform);
    }

    public override int OverrideMemberMethod(
        LuauContext context, 
        IntPtr thread,
        object targetObject,
        string methodName,
        int numParameters,
        ArraySegment<int> parameterDataPODTypes,
        ArraySegment<IntPtr> parameterDataPtrs,
        ArraySegment<int> parameterDataSizes) 
    {
        if (methodName == "GetAirshipComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            return AirshipBehaviourHelper.GetAirshipComponent(context, thread, ((Transform)targetObject).gameObject, typeName);
        }

        if (methodName == "GetAirshipComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            return AirshipBehaviourHelper.GetAirshipComponents(context, thread, ((Transform)targetObject).gameObject, typeName);
        }

        if (methodName == "GetAirshipComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, out var exists);
            
            return AirshipBehaviourHelper.GetAirshipComponentInChildren(context, thread, ((Transform)targetObject).gameObject, typeName, includeInactive);
        }

        if (methodName == "GetAirshipComponentsInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return 0;
            
            var includeInactive = LuauCore.GetParameterAsBool(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes, out var exists);

            return AirshipBehaviourHelper.GetAirshipComponentsInChildren(context, thread, ((Transform)targetObject).gameObject, typeName, includeInactive);
        }

        if (methodName == "AddAirshipComponent") {
            var componentName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

            return AirshipBehaviourHelper.AddAirshipComponent(context, thread, ((Transform)targetObject).gameObject, componentName);
        }
        
        if (methodName == "GetComponent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
        }

        if (methodName == "GetComponentInChildren") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
        }

        if (methodName == "GetComponentInParent") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            return AirshipBehaviourHelper.BypassIfTypeStringIsAllowed(typeName, context, thread);
        }

        if (methodName == "GetComponents") {
            var typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (string.IsNullOrEmpty(typeName)) return -1;

            var t = (Transform)targetObject;
            
            var componentType = LuauCore.CoreInstance.GetTypeFromString(typeName);
            if (componentType == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: Unknown type \"" + typeName + "\". If this is a C# type please report it. There is a chance we forgot to add to allow list.");
                return 0;
            }
            var unityComponents = t.GetComponents(componentType);
            LuauCore.WritePropertyToThread(thread, unityComponents, unityComponents.GetType());
            return 1;
        }

        if (methodName == "GetComponentIfExists") {
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentIfExists takes a parameter");
                return 0;
            }
            Transform gameObject = (Transform)targetObject;

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
            Transform t = (Transform)targetObject;
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInChildren takes a string parameter.");
                return 0;
            }
            
            Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
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
        
        if (methodName == "GetComponentsInParent") {
            Transform t = (Transform)targetObject;
            string typeName = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
            if (typeName == null) {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInParent takes a string parameter.");
                return 0;
            }
            
            Type objectType = LuauCore.CoreInstance.GetTypeFromString(typeName);
            if (objectType == null)
            {
                ThreadDataManager.Error(thread);
                Debug.LogError("Error: GetComponentsInParent component type not found: " + typeName + " (consider registering it?)");
                return 0;
            }
            var results = t.GetComponentsInParent(objectType);
            LuauCore.WritePropertyToThread(thread, results, typeof(Component[]));
            return 1;
        }
        
        if (methodName == "RotateRelativeTo") {
            switch (numParameters) {
                case 2: {
                    var axis = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    var relativeTo = (Space)LuauCore.GetParameterAsInt(1, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);

                    var t = (Transform)targetObject;
                    t.Rotate(axis, relativeTo);
                    return 0;
                }
                case 3: {
                    var axis = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    var angle = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    var relativeTo = (Space)LuauCore.GetParameterAsInt(2, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);

                    var t = (Transform)targetObject;
                    t.Rotate(axis, angle, relativeTo);
                    return 0;
                }
                case 4: {
                    var axisX = LuauCore.GetParameterAsFloat(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    var axisY = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    var axisZ = LuauCore.GetParameterAsFloat(2, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    var relativeTo = (Space)LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);

                    var t = (Transform)targetObject;
                    t.Rotate(axisX, axisY, axisZ, relativeTo);
                    return 0;
                }
            }
        }

        if (methodName == "ClampRotationY" && numParameters == 2) {
            float targetY = LuauCore.GetParameterAsFloat(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            float maxAngle = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            //Clamp the rotation so the spine doesn't appear broken
            Transform t = (Transform)targetObject;
            var localEulerAngles = t.localEulerAngles;
            localEulerAngles = new Vector3(localEulerAngles.x, MathUtil.ClampAngle(targetY, -maxAngle, maxAngle), localEulerAngles.z);
            t.localEulerAngles = localEulerAngles;

            return 0;
        }
        
        if(methodName == "GetDescendants") {
            Transform transform = (Transform)targetObject;
            var children = new List<Transform>();
            GetAllChildren(ref children, transform);
            LuauCore.WritePropertyToThread(thread, children.ToArray<Transform>(), typeof(Transform[]));
            return 1;            
        }
        
        return -1;
    }

    private void GetAllChildren(ref List<Transform> children, Transform transform){
        children.Add(transform);
        foreach(Transform child in transform){
            GetAllChildren(ref children, child);
        }
    }
}
