using Code.Player.Accessories;
using UnityEngine;
using UnityEngine.Serialization;

public class ActiveAccessory {
    public AccessoryComponent AccessoryComponent;
    public Transform rootTransform;
    public GameObject[] gameObjects;
    public MeshRenderer[] meshRenderers;
    public SkinnedMeshRenderer[] skinnedMeshRenderers;
    public Renderer[] renderers;
    public MeshFilter[] meshFilters;
}