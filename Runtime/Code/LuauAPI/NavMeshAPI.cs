using System;
using UnityEngine.AI;

[LuauAPI]
public class NavMeshAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NavMesh);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) {

        if (methodName == "SamplePosition") {
            var sourcePosition = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            var maxDistance = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            var areaMask = LuauCore.GetParameterAsInt32(2, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            if (NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, maxDistance, areaMask)) {
                LuauCore.WritePropertyToThread(thread, hit, typeof(NavMeshHit));
                return 1;
            }

            LuauCore.WritePropertyToThread(thread, null, null);
            return 1;
        }
        if (methodName == "FindClosestEdge") {
            var sourcePosition = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            var areaMask = LuauCore.GetParameterAsInt32(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            if (NavMesh.FindClosestEdge(sourcePosition, out NavMeshHit hit, areaMask)) {
                LuauCore.WritePropertyToThread(thread, hit, typeof(NavMeshHit));
                return 1;
            }

            LuauCore.WritePropertyToThread(thread, null, null);
            return 1;
        }
        if (methodName == "Raycast") {
            var sourcePosition = LuauCore.GetParameterAsVector3(0, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            var targetPosition = LuauCore.GetParameterAsVector3(1, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            var areaMask = LuauCore.GetParameterAsInt32(2, numParameters, parameterDataPODTypes,
                parameterDataPtrs, parameterDataSizes);

            if (NavMesh.Raycast(sourcePosition, targetPosition, out NavMeshHit hit, areaMask)) {
                LuauCore.WritePropertyToThread(thread, hit, typeof(NavMeshHit));
                return 1;
            }

            LuauCore.WritePropertyToThread(thread, null, null);
            return 1;
        }

        return -1;
    }
}
