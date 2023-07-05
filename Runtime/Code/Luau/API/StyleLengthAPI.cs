using System;
 
[LuauAPI]
public class StyleLengthAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.UIElements.StyleLength); 
    }
}
