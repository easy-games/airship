using System;
using UnityEngine;

[LuauAPI]
public class PhysicsAPI : BaseLuaAPIClass
{
    
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Physics);
    }
    public override int OverrideStaticMethod(IntPtr thread, string methodName,  int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "EasyRaycast")
        {
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

                if (hit == true)
                {
                    // Debug.Log($"C# response: {true}, {hitInfo.point}, {hitInfo.normal}, {hitInfo.collider}");
                    LuauCore.WritePropertyToThread(thread, true, typeof(bool));
                    LuauCore.WritePropertyToThread(thread, hitInfo.point, typeof(Vector3));
                    LuauCore.WritePropertyToThread(thread, hitInfo.normal, typeof(Vector3));
                    LuauCore.WritePropertyToThread(thread, hitInfo.collider, typeof(UnityEngine.Object));
                    return 4;
                }
                else
                {
                    LuauCore.WritePropertyToThread(thread, false, typeof(bool));
                    return 1;
                }
 
            }
        }

        if (methodName == "InvertMask")
        {
            if (numParameters == 1)
            {
                int layerMask = LuauCore.GetParameterAsInt(1, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);

                LuauCore.WritePropertyToThread(thread, ~layerMask, typeof(int));
                return 1;
            }
        }
 
        return -1;
    }

}