using System;
using Luau;

[LuauAPI]
public class InputAPI : BaseLuaAPIClass
{

    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Input); 
    }
    public override int OverrideStaticMethod(IntPtr thread, LuauSecurityContext securityContext, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        return -1;
    }

    public override int OverrideMemberMethod(IntPtr thread, LuauSecurityContext securityContext, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        return -1;
    }

}