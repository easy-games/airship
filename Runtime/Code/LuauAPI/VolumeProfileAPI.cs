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
        ArraySegment<int> parameterDataPODTypes,
        ArraySegment<IntPtr> parameterDataPtrs,
        ArraySegment<int> parameterDataSizes) {
        var target = (VolumeProfile)targetObject;

        if (methodName == "GetDepthOfField") {
            if (target.TryGet<DepthOfField>(out var dof)) {
                LuauCore.WritePropertyToThread(thread, dof, typeof(DepthOfField));
                return 1;
            }

            return 0;
        }

        if (methodName == "GetVolumeComponents") {
            var results = target.components.ToArray();
            LuauCore.WritePropertyToThread(thread, results, typeof(VolumeComponent[]));
            return 1;
        }

        return -1;
    }
}
