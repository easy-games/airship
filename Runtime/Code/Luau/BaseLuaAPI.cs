
using System;
using Luau;
using UnityEngine;


public abstract class BaseLuaAPIClass
{


    public abstract Type GetAPIType();
    public virtual int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName,  int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) { return -1; }
    public virtual int OverrideMemberMethod(LuauContext context, IntPtr thread, System.Object targetObject, string methodName, int numParameters, int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes) { return -1; }

    public virtual int OverrideMemberGetter(LuauContext context, IntPtr thread, System.Object targetObject,
        string getterName) {
        return -1;
    }

    public virtual int OverrideMemberSetter(LuauContext context, IntPtr thread, System.Object targetObject, string setterName,
        LuauCore.PODTYPE dataType, IntPtr dataPtr, int dataPtrSize) {
        return -1;
    }
    
    public virtual Type[] GetDescendantTypes() { return null; }
};
