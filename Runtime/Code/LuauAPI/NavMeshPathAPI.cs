using System;
using UnityEngine.AI;

[LuauAPI]
public class NavMeshPathAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NavMeshPath);
    }
}