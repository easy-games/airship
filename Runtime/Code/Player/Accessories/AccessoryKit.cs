using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Accessory Kit", menuName = "EasyGG/BedWars/Entity Accessory Kit", order = 0)]
public class AccessoryKit : ScriptableObject {
	public List<Accessory> accessories;
}
