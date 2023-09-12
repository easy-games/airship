using System;

[LuauAPI]
public class ResourcesAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Resources);
    }
 
}