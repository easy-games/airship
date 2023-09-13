using System;
using UnityEngine.UI;

[LuauAPI]
public class ImageAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Image);
    }
}