using System;
using UnityEngine;

[LuauAPI]
public class CollisionAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Collision);
    }
}