using System;
using Luau;

[LuauAPI]
public class InputAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(UnityEngine.Input); 
    }
    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        return -1;
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        return -1;
    }
}
