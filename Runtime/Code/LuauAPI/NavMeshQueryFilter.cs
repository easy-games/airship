using System;
using Code.Luau.LuauAssembly;
using UnityEngine.AI;

[LuauAPI]
public class NavMeshQueryFilterAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(NavMeshQueryFilter);
    }
}