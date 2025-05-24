using System;
using System.Collections.Generic;
using System.Linq;
using Luau;
using UnityEngine.EventSystems;

[LuauAPI]
public class EventSystemAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(EventSystem);
    }

    public override int OverrideMemberMethod(LuauContext context, IntPtr thread, object targetObject, string methodName, int numParameters,
        ArraySegment<int> parameterDataPODTypes, ArraySegment<IntPtr> parameterDataPtrs, ArraySegment<int> parameterDataSizes) {
        var target = (EventSystem)targetObject;
        if (methodName == "ClearSelected") {
            target.SetSelectedGameObject(null);
            return 0;
        }

        if (methodName == "RaycastAll") {
            var pointerData = (PointerEventData) LuauCore.GetParameterAsObject(0, numParameters, parameterDataPODTypes, parameterDataPtrs,
                parameterDataSizes, thread);
            
            var results = new List<RaycastResult>();
            target.RaycastAll(pointerData, results);

            var resultsArray = results.ToArray();
            LuauCore.WritePropertyToThread(thread, resultsArray, resultsArray.GetType());
            return 1;
        }
        
        return -1;
    }
}