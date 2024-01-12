using UnityEngine;

[CreateAssetMenu(fileName = "New Avatar Collection", menuName = "Airship/Accessories/Avatar Collection", order = 0)]
public class AvatarCollection : ScriptableObject {
    public AccessorySkin[] skinAccessories;
    public AccessoryComponent[] generalAccessories;
}