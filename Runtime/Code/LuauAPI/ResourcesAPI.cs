using System;

[LuauAPI(LuauContext.Protected)]
public class ResourcesAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Resources);
    }
 
}