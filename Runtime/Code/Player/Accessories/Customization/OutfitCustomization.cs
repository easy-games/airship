/// <summary>
/// This is the JSON data saved into the Outfit custom meta data
/// </summary>
[LuauAPI]
[System.Serializable]
public class OutfitCustomization {
    public OutfitCustomizationSlot[]  platformCustomSlots = new OutfitCustomizationSlot[0];
}

[LuauAPI]
[System.Serializable]
public class OutfitCustomizationSlot {
    public int slot = 0;
    public int variant = 0;
    public OutfitCustomizationColor[] colors = new OutfitCustomizationColor[0];
}

[LuauAPI]
[System.Serializable]
public class OutfitCustomizationColor {
    public string key = "Color";
    public string colorHex = "#FFFFFF";
}