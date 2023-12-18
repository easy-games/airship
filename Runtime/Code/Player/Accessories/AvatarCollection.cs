using UnityEngine;

[CreateAssetMenu(fileName = "New Avatar Collection", menuName = "Airship/Accessories/Avatar Collection", order = 0)]
public class AvatarCollection : ScriptableObject {
    public AccessorySkin[] skinAccessories;
    public Accessory[] headShapeAccessories;
    public Accessory[] headAccessories;
    public Accessory[] torsoAccessories;
    public Accessory[] handAccessories;
    public Accessory[] legAccessories;
    public Accessory[] feetAccessories;
}