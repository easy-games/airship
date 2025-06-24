using System;
using Unity.Mathematics;
using UnityEngine.Splines;

[LuauAPI]
public class SplineUtilityAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(SplineUtility);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        
        if (methodName == "EvaluatePosition") {
            var spline = (ISpline) LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes, thread);

            var f = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            var result = SplineUtility.EvaluatePosition(spline, f);
            LuauCore.WritePropertyToThread(thread, result, typeof(float3));
            return 1;
        }
        
        if (methodName == "EvaluateTangent") {
            var spline = (ISpline) LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes, thread);

            var f = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            var result = SplineUtility.EvaluateTangent(spline, f);
            LuauCore.WritePropertyToThread(thread, result, typeof(float3));
            return 1;
        }
        
        if (methodName == "EvaluateUpVector") {
            var spline = (ISpline) LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes, thread);

            var f = LuauCore.GetParameterAsFloat(1, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);

            var result = SplineUtility.EvaluateUpVector(spline, f);
            LuauCore.WritePropertyToThread(thread, result, typeof(float3));
            return 1;
        }

        return -1;
    }
}