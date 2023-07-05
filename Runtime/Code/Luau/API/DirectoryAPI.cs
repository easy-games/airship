using System;
using System.IO;
using UnityEngine;

[LuauAPI]
public class DirectoryAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(Directory);
    }
}