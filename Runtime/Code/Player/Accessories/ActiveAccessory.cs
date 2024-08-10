using Code.Player.Accessories;
using UnityEngine;

public struct ActiveAccessory {
    public AccessoryComponent AccessoryComponent;
    public Transform rootTransform;
    public GameObject[] gameObjects;
    public Renderer[] renderers;
}