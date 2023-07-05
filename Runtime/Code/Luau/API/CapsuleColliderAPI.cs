using System;
using UnityEngine;

[LuauAPI]
public class CapsuleColliderAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(CapsuleCollider);
    }
}