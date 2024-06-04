using System;
using Luau;
using UnityEngine;

//Do we need this one?!
[LuauAPI]
public class ObjectAPI : BaseLuaAPIClass
{
 
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Object);
    }
    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "DontDestroyOnLoad" && LuauCore.CurrentContext == LuauContext.Game) {
            Debug.LogError("[Airship] Access denied to method DontDestroyOnLoad(). Instead, load a new offline scene to store persistent GameObjects.");
            ThreadDataManager.Error(thread);
            return 0;
        }
        return -1;
    }
    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        return -1;
    }

}