using System;
using UnityEngine;

[LuauAPI(ContextOverrideList = new [] {"OpenURL"}, ContextOverrideMask = (int) LuauContext.Protected)]
public class ApplicationAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Application);
    }
}