using System;
using Luau;
using UnityEngine;

[LuauAPI]
public class PhysicsAPI : BaseLuaAPIClass
{
    
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Physics);
    }
    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName,  int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {
        if (methodName is "Raycast" or "EasyRaycast") {
            //ray.origin, ray.direction, 1000, -1
            if (numParameters >= 3 && numParameters <= 5) {
                Vector3 start = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                Vector3 dir = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                float distance = LuauCore.GetParameterAsFloat(2, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

                bool hit;
                RaycastHit hitInfo;

                if (numParameters == 3) {
                    // 3 params (no mask)
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance);
                } else if (numParameters == 4) {
                    // 4 params
                    int layerMask = LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance, layerMask);
                } else {
                    // 5 params
                    int layerMask = LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                    QueryTriggerInteraction queryTriggerInteraction = (QueryTriggerInteraction) LuauCore.GetParameterAsInt(4, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        paramaterDataSizes);
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance, layerMask, queryTriggerInteraction);
                }

                if (hit == true) {
                    // Debug.Log($"C# response: {true}, {hitInfo.point}, {hitInfo.normal}, {hitInfo.collider}");
                    LuauCore.WritePropertyToThread(thread, true, typeof(bool));
                    LuauCore.WritePropertyToThread(thread, hitInfo.point, typeof(Vector3));
                    LuauCore.WritePropertyToThread(thread, hitInfo.normal, typeof(Vector3));
                    LuauCore.WritePropertyToThread(thread, hitInfo.collider, typeof(UnityEngine.Object));
                    return 4;
                } else {
                    LuauCore.WritePropertyToThread(thread, false, typeof(bool));
                    return 1;
                }
            }
        }

        if (methodName == "BoxCast") {
            Vector3 center = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);
            Vector3 halfExtents = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);
            Vector3 direction = LuauCore.GetParameterAsVector3(2, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);

            Quaternion orientation;
            if (numParameters >= 4) {
                orientation = LuauCore.GetParameterAsQuaternion(3, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            } else {
                orientation = Quaternion.identity;
            }

            float maxDistance;
            if (numParameters >= 5) {
                maxDistance = LuauCore.GetParameterAsFloat(4, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            } else {
                maxDistance = Mathf.Infinity;
            }

            int layerMask;
            if (numParameters >= 6) {
                layerMask = LuauCore.GetParameterAsInt(5, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            } else {
                layerMask = Physics.DefaultRaycastLayers;
            }

            QueryTriggerInteraction queryTriggerInteraction;
            if (numParameters >= 7) {
                queryTriggerInteraction = (QueryTriggerInteraction) LuauCore.GetParameterAsInt(6, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    paramaterDataSizes);
            } else {
                queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;
            }

            RaycastHit hitInfo;
            var hit = Physics.BoxCast(center, halfExtents, direction, out hitInfo, orientation, maxDistance, layerMask,
                queryTriggerInteraction);

            if (hit) {
                LuauCore.WritePropertyToThread(thread, true, typeof(bool));
                LuauCore.WritePropertyToThread(thread, hitInfo.point, typeof(Vector3));
                LuauCore.WritePropertyToThread(thread, hitInfo.normal, typeof(Vector3));
                LuauCore.WritePropertyToThread(thread, hitInfo.collider, typeof(UnityEngine.Object));
                return 4;
            } else {
                LuauCore.WritePropertyToThread(thread, false, typeof(bool));
                return 1;
            }
        }
        if (methodName is "RaycastLegacy") {
            //ray.origin, ray.direction, 1000, -1

            if (numParameters == 3 || numParameters == 4)
            {
                Vector3 start = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                Vector3 dir = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                float distance = LuauCore.GetParameterAsFloat(2, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

                bool hit;
                RaycastHit hitInfo;

                if (numParameters == 3) {
                    // 3 params (no mask)
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance);
                } else {
                    // 4 params (mask)
                    int layerMask = LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance, layerMask);
                }

                if (hit) {
                    LuauCore.WritePropertyToThread(thread, hitInfo, typeof(UnityEngine.Object));
                    return 1;
                }

                return 0;
            }
        }

        if (methodName == "InvertMask")
        {
            if (numParameters == 1)
            {
                int layerMask = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
                LuauCore.WritePropertyToThread(thread, ~layerMask, typeof(int));
                return 1;
            }
        }
 
        return -1;
    }

}