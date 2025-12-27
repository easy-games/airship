using System;

[Flags]
public enum AirshipPropertyTargets {
    /// <summary>
    /// Target any value (non-collection) instances
    /// </summary>
    ValueProperty = 1 << 0,
    /// <summary>
    /// Target any array instances
    /// </summary>
    ArrayProperty = 1 << 1,
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class CustomAirshipPropertyDrawerAttribute : Attribute {
    public string TypeName { get; }
    public string AssetPath { get; set; }
    public bool UseForChildren { get; set; }

    public AirshipPropertyTargets PropertyTargets { get; set; } = AirshipPropertyTargets.ValueProperty;

    public CustomAirshipPropertyDrawerAttribute(string className, bool useForChildren = false) {
        TypeName = className;
        UseForChildren = useForChildren;
    }

    public CustomAirshipPropertyDrawerAttribute(string className, string assetPath, bool useForChildren = false) {
        TypeName = className;
        AssetPath = assetPath;
        UseForChildren = useForChildren;
    }
}