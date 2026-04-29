using System;
using UnityEngine;

[LuauAPI]
public class Collision2DAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Collision2D);
    }
}