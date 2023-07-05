using System;

[LuauAPI]
public class TimeAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.Time);
    }
  

}