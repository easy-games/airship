using System;
using Code.Player.Human.Net;
using FishNet.Managing.Timing;

[LuauAPI]
public class MoveInputDataAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(MoveInputData);
    }
}