
using System;
using UnityEngine;

[LuauAPI]
public class AnimatorOverrideControllerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(AnimatorOverrideController);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {

        if (methodName == "SetClip") {
            var name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes);
            var clip = LuauCore.GetParameterAsObject(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                paramaterDataSizes, thread);
            ((AnimatorOverrideController)targetObject)[name] = (AnimationClip)clip;
        }

        return -1;
    }
}