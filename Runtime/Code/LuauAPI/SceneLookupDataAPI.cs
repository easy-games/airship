using System;

[LuauAPI]    
public class SceneLookupDataAPI  : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(FishNet.Managing.Scened.SceneLookupData);
    }     
}