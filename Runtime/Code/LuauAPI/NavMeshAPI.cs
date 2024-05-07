using System;
using UnityEngine.AI;

[LuauAPI]
public class NavMeshAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NavMesh);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) {

        if (methodName == "SamplePosition") {
            var sourcePosition = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                parameterDataPtrs, paramaterDataSizes);

            var maxDistance = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, paramaterDataSizes);

            var areaMask = LuauCore.GetParameterAsInt(2, numParameters, parameterDataPODTypes,
                parameterDataPtrs, paramaterDataSizes);

            var result = NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, maxDistance, areaMask);
            LuauCore.WritePropertyToThread(thread, result, typeof(bool));
            LuauCore.WritePropertyToThread(thread, hit, typeof(NavMeshHit));
            return 2;
        }

        return -1;
    }
}