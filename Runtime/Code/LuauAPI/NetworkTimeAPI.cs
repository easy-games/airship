using System;
using Mirror;

[LuauAPI]
public class NetworkTimeAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NetworkTime);
    }
}