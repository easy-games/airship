using System;
using Mirror;

[LuauAPI]
public class NetworkClientAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NetworkClient);
    }
}