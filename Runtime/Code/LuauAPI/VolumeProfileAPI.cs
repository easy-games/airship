using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[LuauAPI]
public class VolumeProfileAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(VolumeProfile);
    }

    public override int OverrideMemberMethod(
        LuauContext context,
        IntPtr thread,
        object targetObject,
        string methodName,
        int numParameters,
        int[] parameterDataPODTypes,
        IntPtr[] parameterDataPtrs,
        int[] paramaterDataSizes) {
        if (methodName == "GetDepthOfField") {
            var target = (VolumeProfile)targetObject;
            if (target.TryGet<DepthOfField>(out var dof)) {
                LuauCore.WritePropertyToThread(thread, dof, typeof(DepthOfField));
                return 1;
            }

            return 0;
        }

        return -1;
    }
}