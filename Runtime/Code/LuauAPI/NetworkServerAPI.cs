using System;
using Mirror;

[LuauAPI]
public class NetworkServerAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NetworkServer);
    }
}