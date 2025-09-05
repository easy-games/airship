using System;
using Code.Luau.LuauAssembly.Protection;
using Luau;
using UnityEngine;

[LuauAPI]
public class ObjectAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(UnityEngine.Object);
    }
    
    public override int OverrideStaticMethod(LuauContext context, IntPtr thread, string methodName, int numParameters, Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) {
        if (methodName == "DontDestroyOnLoad" && LuauProtection.CurrentContext == LuauContext.Game) {
            Debug.LogError("[Airship] Access denied to method DontDestroyOnLoad(). Instead, load a new offline scene to store persistent GameObjects.");
            ThreadDataManager.Error(thread);
            return 0;
        }
        return -1;
    }
    
    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters, Span<int> parameterDataPODTypes, Span<IntPtr> parameterDataPtrs, Span<int> parameterDataSizes) {
        return -1;
    }
}
