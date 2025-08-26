using System;
using System.Collections.Generic;

public partial class LuauCore {
    private static Dictionary<ulong, Func<LuauContext, object, IntPtr, IntPtr, IntPtr, IntPtr, int, int>> _callbackMap = new Dictionary<ulong, Func<LuauContext, object, IntPtr, IntPtr, IntPtr, IntPtr, int, int>>();

    public static bool CallMethodDirectly(ulong methodNameHash, LuauContext context, object objectReference, IntPtr thread, IntPtr firstParameterData, IntPtr firstParameterSize, IntPtr firstParameterType, int numParameters, out int methodReturn) {
        if (_callbackMap.TryGetValue(methodNameHash, out var action)) {
            methodReturn = action.Invoke(context, objectReference, thread, firstParameterData, firstParameterSize, firstParameterType, numParameters);
            return true;
        }
        methodReturn = 0; // Won't be used
        return false;
    }

    public static void RegisterDirectCallback(ulong hash, Func<LuauContext, object, IntPtr, IntPtr, IntPtr, IntPtr, int, int> callback) {
        _callbackMap[hash] = callback;
    }
}