using System;
using UnityEngine.UIElements;

[LuauAPI]
static public class DisplayStyle 
{
    //Here is where I write a pithy comment about how dumb wrapping an enum in a generic is...
    public static StyleEnum<UnityEngine.UIElements.DisplayStyle> Flex = new StyleEnum<UnityEngine.UIElements.DisplayStyle>(UnityEngine.UIElements.DisplayStyle.Flex);
    public static StyleEnum<UnityEngine.UIElements.DisplayStyle> None = new StyleEnum<UnityEngine.UIElements.DisplayStyle>(UnityEngine.UIElements.DisplayStyle.None);
}
