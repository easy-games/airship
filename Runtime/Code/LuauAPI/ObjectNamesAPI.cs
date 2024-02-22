using System;
using UnityEditor;

[LuauAPI]
public class ObjectNamesAPI : BaseLuaAPIClass {

    public override Type GetAPIType() {
        return typeof(ObjectNames);
    }
}