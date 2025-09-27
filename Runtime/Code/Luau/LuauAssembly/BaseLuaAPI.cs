using System;
using Code.Luau.LuauAssembly;
using JetBrains.Annotations;

public abstract class BaseLuaAPIClass {
    public abstract Type GetAPIType();
    public virtual int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName,  int numParameters, Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) { return -1; }
    public virtual int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters, Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) { return -1; }

    public virtual int OverrideMemberGetter(LuauContext context, IntPtr thread, object targetObject, string getterName) {
        return -1;
    }

    public virtual int OverrideMemberSetter(LuauContext context, IntPtr thread, object targetObject, string setterName, PODTYPE dataType, IntPtr dataPtr, int dataPtrSize) {
        return -1;
    }
    
    public virtual Type[] GetDescendantTypes() { return null; }
}
