using System;
using Luau;
using UnityEngine;

[LuauAPI]
public class CharacterControllerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(CharacterController);
    }

    public override int OverrideMemberMethod(IntPtr thread, LuauSecurityContext securityContext, object targetObject, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {

        if (methodName == "Raycast" && numParameters == 2) {
            Ray ray = LuauCore.GetParameterAsRay(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);
            float distance = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);

            CharacterController target = (CharacterController)targetObject;
            if (target.Raycast(ray, out RaycastHit hitInfo, distance)) {
                LuauCore.WritePropertyToThread(thread, hitInfo, typeof(RaycastHit));
            } else {
                LuauCore.WritePropertyToThread(thread, null, null);
            }
            return 1;
        }

        return base.OverrideMemberMethod(thread, securityContext, targetObject, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, paramaterDataSizes);
    }
}