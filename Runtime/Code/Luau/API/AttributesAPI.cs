using System;
using UnityEngine;

[LuauAPI]
public class AttributesAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(EasyAttributes);
    }
}