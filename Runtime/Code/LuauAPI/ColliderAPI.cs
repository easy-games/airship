using System;
using Luau;
using UnityEngine;

[LuauAPI]
public class ColliderAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Collider);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {

        if (methodName == "Raycast" && numParameters == 2) {
            Ray ray = LuauCore.GetParameterAsRay(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            float distance = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            Collider target = (Collider)targetObject;
            if (target.Raycast(ray, out RaycastHit hitInfo, distance)) {
                LuauCore.WritePropertyToThread(thread, hitInfo, typeof(RaycastHit));
            } else {
                LuauCore.WritePropertyToThread(thread, null, null);
            }
            return 1;
        }

        return base.OverrideMemberMethod(context, thread, targetObject, methodName, numParameters, parameterDataPODTypes, parameterDataPtrs, parameterDataSizes);
    }

    public override Type[] GetDescendantTypes() {
        return new Type[] {
            typeof(CapsuleCollider),
            typeof(BoxCollider),
            typeof(SphereCollider),
            typeof(MeshCollider),
        };
    }
}