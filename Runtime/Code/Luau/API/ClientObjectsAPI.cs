using System;
using FishNet.Managing.Client;


[LuauAPI]
public class ClientObjectsAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(ClientObjects);
    }
}