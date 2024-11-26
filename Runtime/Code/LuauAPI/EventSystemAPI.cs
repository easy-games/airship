using System;
using Luau;
using UnityEngine.EventSystems;

[LuauAPI]
public class EventSystemAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(EventSystem);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes)
    {
        if (methodName == "ClearSelected")
        {
            EventSystem.current.SetSelectedGameObject(null);
            return 0;
        }
        
        return -1;
    }
}