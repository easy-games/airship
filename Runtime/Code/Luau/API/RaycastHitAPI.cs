using System;

[LuauAPI]
public class RaycastHitAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
     
        return typeof(UnityEngine.RaycastHit);
    }
  

}