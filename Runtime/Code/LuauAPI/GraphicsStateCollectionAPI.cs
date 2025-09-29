using System;
using UnityEngine.Experimental.Rendering;

[LuauAPI]
public class GraphicsStateCollectionAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(GraphicsStateCollection);
    }
}