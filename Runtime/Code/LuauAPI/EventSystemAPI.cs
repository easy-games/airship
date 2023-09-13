using System;
using UnityEngine.EventSystems;

[LuauAPI]
public class EventSystemAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(EventSystem);
    }

    public override int OverrideMemberMethod(IntPtr thread, object targetObject, string methodName, int numParameters,
        int[] parameterDataPODTypes, IntPtr[] parameterDataPtrs, int[] paramaterDataSizes)
    {
        if (methodName == "ClearSelected")
        {
            EventSystem.current.SetSelectedGameObject(null);
            return 0;
        }
        
        return -1;
    }
}