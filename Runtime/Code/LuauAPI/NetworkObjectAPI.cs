using System;
using FishNet.Object;
using UnityEngine;

[LuauAPI]
public class NetworkObjectAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NetworkObject);
    }
}