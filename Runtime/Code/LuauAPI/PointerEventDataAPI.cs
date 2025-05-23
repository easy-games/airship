using System;
using UnityEngine.EventSystems;

[LuauAPI]
public class PointerEventDataAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(PointerEventData);
    }
}