using System;

[LuauAPI]
public class RectAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Rect);
    }
}