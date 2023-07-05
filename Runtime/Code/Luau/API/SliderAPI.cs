using System;
using UnityEngine.UI;

[LuauAPI]
public class SliderAPI : BaseLuaAPIClass
{
    public override Type GetAPIType()
    {
        return typeof(Slider);
    }
}