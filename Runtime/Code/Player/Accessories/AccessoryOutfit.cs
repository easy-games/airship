using System.Collections.Generic;
using Code.Player.Accessories;
using UnityEngine;

[LuauAPI]
[CreateAssetMenu(fileName = "New Accessory Outfit", menuName = "Airship/Accessories/Accessory Outfit", order = 0)]
public class AccessoryOutfit : ScriptableObject {
	public AccessoryComponent[] accessories;
	public AccessoryFace faceDecal;
	//public AccessorySkin customSkin;
	public bool forceSkinColor = false;
	public Color skinColor = new Color(.85f, .65f, .5f);
}
