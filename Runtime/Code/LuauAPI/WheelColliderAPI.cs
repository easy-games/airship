using System;
using UnityEngine;

[LuauAPI]
public class WheelColliderAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(WheelCollider);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        
        if (methodName == "GetGroundHit") {
            var target = (WheelCollider)targetObject;
            if (target.GetGroundHit(out var hit)) {
                LuauCore.WritePropertyToThread(thread, hit, hit.GetType());
                return 1;
            }

            return 0;
        }
        
        if (methodName == "GetWorldPose") {
            var target = (WheelCollider)targetObject;
            target.GetWorldPose(out var pos, out var quat);
            LuauCore.WritePropertyToThread(thread, pos, pos.GetType());
            LuauCore.WritePropertyToThread(thread, quat, quat.GetType());
            return 2;
        }
        
        return -1;
    }
}