using System;
using UnityEngine;

[LuauAPI]
public class TransformAPI : BaseLuaAPIClass
{
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