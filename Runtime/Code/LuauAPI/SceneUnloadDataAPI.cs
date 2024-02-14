using System;

[LuauAPI]    
public class SceneUnloadDataAPI  : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(FishNet.Managing.Scened.SceneUnloadData);
    }     
}