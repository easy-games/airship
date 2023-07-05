using UnityEngine;

[LuauAPI]
public class AccessoryHelper : MonoBehaviour {
	[SerializeField] private Transform rightHand;

	public Transform RightHand => rightHand;
}
