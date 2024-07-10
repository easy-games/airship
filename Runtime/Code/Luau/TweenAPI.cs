using System;
using ElRaccoone.Tweens;

[LuauAPI]
public class TweenAPI : BaseLuaAPIClass {
    public override Type GetAPIType()
    {
        return typeof(NativeTween);
    }
}