using System;
using Tayx.Graphy;

[LuauAPI]
public class GraphyManagerAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(GraphyManager);
    }
}