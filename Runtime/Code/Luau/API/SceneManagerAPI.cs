using System;
using UnityEngine.SceneManagement;

[LuauAPI]
public class SceneManagerAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(SceneManager);
    }
}