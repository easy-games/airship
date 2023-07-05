using System;
using UnityEngine;

[LuauAPI]
public class ApplicationAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Application);
    }
}