
using System;
using UnityEngine;


public abstract class BaseLuaAPIClass
{
    public abstract Type GetAPIType();
    public virtual int OverrideStaticMethod(IntPtr thread, string methodName,  int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) { return -1; }
    public virtual int OverrideMemberMethod(IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) { return -1; }
    
    public virtual Type[] GetDescendantTypes() { return null; }
};
