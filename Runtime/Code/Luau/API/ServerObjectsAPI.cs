using System;
using FishNet.Managing.Client;
using FishNet.Managing.Server;


[LuauAPI]
public class ServerObjectsAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(ServerObjects);
    }
}