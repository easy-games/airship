using System;
using UnityEngine;

[LuauAPI]
public class ClickableAPI : BaseLuaAPIClass
{ 
    public override Type GetAPIType()
    {
        return typeof(UnityEngine.UIElements.Clickable);

        
    }
   
}