using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Accessory Collection", menuName = "Airship/Accessories/Accessory Collection", order = 0)]
public class AccessoryCollection : ScriptableObject {
	public List<AccessoryComponent> accessories;
	public AccessorySkin customSkin;
}
