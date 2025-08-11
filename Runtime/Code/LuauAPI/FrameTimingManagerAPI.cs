using System;
using UnityEngine;

[LuauAPI]
public class FrameTimingManagerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(UnityEngine.FrameTimingManager);
    }

    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs,
        ArraySegment<int> parameterDataSizes) {
        if (methodName is "GetLatestTimings") {
            if (numParameters != 1) throw new ArgumentException("GetLatestTimings expects 1 parameter.");

            var numTimings = (uint) LuauCore.GetParameterAsInt(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes);
            
            // Arbitrary limit to avoid huge allocations / requests
            if (numTimings > 1000) throw new ArgumentException("Requested too many frames (>1000) from GetLatestTimings.");
            
            // This function will alloc -- we could look to in the future support a non-alloc setup
            var timings = new FrameTiming[numTimings];
            var fetchedResults = FrameTimingManager.GetLatestTimings(numTimings, timings);
            LuauCore.WriteArrayToThread(thread, timings, typeof(FrameTiming), (int) fetchedResults);
            return 1;
        }
        return -1;
    }
}