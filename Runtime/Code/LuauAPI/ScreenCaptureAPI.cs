using System;

[LuauAPI]
public class ScreenCaptureAPI : BaseLuaAPIClass
{

    public override Type GetAPIType()
    {
        return typeof(UnityEngine.ScreenCapture);
    }
    public override int OverrideStaticMethod(IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        return -1;
    }

    public override int OverrideMemberMethod(IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        return -1;
    }

}