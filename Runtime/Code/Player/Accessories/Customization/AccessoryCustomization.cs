[LuauAPI]
[System.Serializable]
public class AccessoryCustomization {
    public AccessoryCustomizationGear[] platformCustomGear;
}

[LuauAPI]
[System.Serializable]
public class AccessoryCustomizationGear {
    public int[] slots;
    public int variant;
    public string[] colors;
}