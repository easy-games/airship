/// <summary>
/// This is the JSON data saved into the Outfit custom meta data
/// </summary>
[LuauAPI]
[System.Serializable]
public class OutfitCustomization {
    public OutfitCustomizationGear[] platformCustomGear;
}

[LuauAPI]
[System.Serializable]
public class OutfitCustomizationGear {
    public int[] slots;
    public int variant = 0;
    public OutfitCustomizationColor[] colors;
}

[LuauAPI]
[System.Serializable]
public class OutfitCustomizationColor {
    public string key = "Color";
    public string colorHex = "#FFFFFF";
}