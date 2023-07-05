using System;

[LuauAPI]
public class ClientSceneListenerAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(ClientSceneListener);
    }
}