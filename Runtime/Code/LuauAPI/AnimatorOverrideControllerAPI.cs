
using System;
using UnityEngine;

[LuauAPI]
public class AnimatorOverrideControllerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(AnimatorOverrideController);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {

        if (methodName == "SetClip") {
            var name = LuauCore.GetParameterAsString(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            var clip = LuauCore.GetParameterAsObject(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes, thread);
            ((AnimatorOverrideController)targetObject)[name] = (AnimationClip)clip;
            return 0;
        }

        return -1;
    }
}