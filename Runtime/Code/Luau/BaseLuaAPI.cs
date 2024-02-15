
using System;
using Luau;
using UnityEngine;


public abstract class BaseLuaAPIClass
{
    public abstract Type GetAPIType();
    public virtual int OverrideStaticMethod(IntPtr thread, LuauSecurityContext securityContext, string methodName,  int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) { return -1; }
    public virtual int OverrideMemberMethod(IntPtr thread, LuauSecurityContext securityContext, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) { return -1; }
    
    public virtual Type[] GetDescendantTypes() { return null; }
};
