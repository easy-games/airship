using System;
using Luau;

//Do we need this one?!
[LuauAPI]
public class ObjectAPI : BaseLuaAPIClass
{
 
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Object);
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