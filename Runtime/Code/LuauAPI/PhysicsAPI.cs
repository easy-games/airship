using System;
using Luau;
using Mirror;
using UnityEngine;

[LuauAPI]
public class PhysicsAPI : BaseLuaAPIClass
{
    
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Physics);
    }

    private int WriteRaycastResultToThread(IntPtr thread, bool success, RaycastHit hitInfo) {
        if (success) {
            LuauCore.WritePropertyToThread(thread, true, typeof(bool));
            LuauCore.WritePropertyToThread(thread, hitInfo.point, typeof(Vector3));
            LuauCore.WritePropertyToThread(thread, hitInfo.normal, typeof(Vector3));
            LuauCore.WritePropertyToThread(thread, hitInfo.collider, typeof(UnityEngine.Object));
            return 4;
        }
        else {
            LuauCore.WritePropertyToThread(thread, false, typeof(bool));
            return 1;
        }
    }
    
    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName,  int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        if (methodName is "Raycast" or "EasyRaycast") {
            //ray.origin, ray.direction, 1000, -1
            if (numParameters >= 3 && numParameters <= 5) {
                Vector3 start = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                Vector3 dir = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                float distance = LuauCore.GetParameterAsFloat(2, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

                bool hit;
                RaycastHit hitInfo;

                if (numParameters == 3) {
                    // 3 params (no mask)
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance);
                } else if (numParameters == 4) {
                    // 4 params
                    int layerMask = LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance, layerMask);
                } else {
                    // 5 params
                    int layerMask = LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                    QueryTriggerInteraction queryTriggerInteraction = (QueryTriggerInteraction) LuauCore.GetParameterAsInt(4, numParameters, parameterDataPODTypes, parameterDataPtrs,
                        parameterDataSizes);
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance, layerMask, queryTriggerInteraction);
                }
                
                return WriteRaycastResultToThread(thread, hit, hitInfo);
            }
        }

        if (methodName is "SphereCast") {
            switch (numParameters) {
                case 3: {
                    // origin, radius, direction
                    var origin = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var radius = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var direction = LuauCore.GetParameterAsVector3(2, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);

                    var hit = Physics.SphereCast(origin, radius, direction, out var hitInfo);
                    return WriteRaycastResultToThread(thread, hit, hitInfo);
                }
                case 4: {
                    // origin, radius, direction, maxDistance
                    var origin = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var radius = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var direction = LuauCore.GetParameterAsVector3(2, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var maxDistance = LuauCore.GetParameterAsFloat(3, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);

                    var hit = Physics.SphereCast(origin, radius, direction, out var hitInfo, maxDistance);
                    return WriteRaycastResultToThread(thread, hit, hitInfo);
                }
                case 5: {
                    // origin, radius, direction, maxDistance, layerMask
                    var origin = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var radius = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var direction = LuauCore.GetParameterAsVector3(2, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var maxDistance = LuauCore.GetParameterAsFloat(3, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);
                    var layerMask = LuauCore.GetParameterAsInt(4, numParameters, parameterDataPODTypes,
                        parameterDataPtrs, parameterDataSizes);

                    var hit = Physics.SphereCast(origin, radius, direction, out var hitInfo, maxDistance, layerMask);
                    return WriteRaycastResultToThread(thread, hit, hitInfo);
                    break;
                }
            }
        }
        
        if (methodName == "BoxCast") {
            Vector3 center = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            Vector3 halfExtents = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            Vector3 direction = LuauCore.GetParameterAsVector3(2, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            Quaternion orientation;
            if (numParameters >= 4) {
                orientation = LuauCore.GetParameterAsQuaternion(3, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
            } else {
                orientation = Quaternion.identity;
            }

            float maxDistance;
            if (numParameters >= 5) {
                maxDistance = LuauCore.GetParameterAsFloat(4, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
            } else {
                maxDistance = Mathf.Infinity;
            }

            int layerMask;
            if (numParameters >= 6) {
                layerMask = LuauCore.GetParameterAsInt(5, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
            } else {
                layerMask = Physics.DefaultRaycastLayers;
            }

            QueryTriggerInteraction queryTriggerInteraction;
            if (numParameters >= 7) {
                queryTriggerInteraction = (QueryTriggerInteraction) LuauCore.GetParameterAsInt(6, numParameters, parameterDataPODTypes, parameterDataPtrs,
                    parameterDataSizes);
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

            if (numParameters == 3 || numParameters == 4) {
                Vector3 start = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                Vector3 dir = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                float distance = LuauCore.GetParameterAsFloat(2, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);

                bool hit;
                RaycastHit hitInfo;

                if (numParameters == 3) {
                    // 3 params (no mask)
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance);
                } else {
                    // 4 params (mask)
                    int layerMask = LuauCore.GetParameterAsInt(3, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                    hit = Physics.Raycast(new Ray(start, dir), out hitInfo, distance, layerMask);
                }

                if (hit) {
                    LuauCore.WritePropertyToThread(thread, hitInfo, typeof(UnityEngine.Object));
                    return 1;
                }

                return 0;
            }
        }

        if (methodName == "InvertMask") {
            if (numParameters == 1) {
                int layerMask = LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
                LuauCore.WritePropertyToThread(thread, ~layerMask, typeof(int));
                return 1;
            }
        }
 
        return -1;
    }

}
