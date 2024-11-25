using System;
using Luau;

[LuauAPI]
public class ScreenCaptureAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(UnityEngine.ScreenCapture);
    }
    
    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        return -1;
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        return -1;
    }
}
