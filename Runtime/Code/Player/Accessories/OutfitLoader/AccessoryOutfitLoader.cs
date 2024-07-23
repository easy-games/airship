using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(AccessoryBuilder))]
#if UNITY_EDITOR
[CanEditMultipleObjects]
#endif
public class AccessoryOutfitLoader : MonoBehaviour {
    public AccessoryOutfit outfit;
}