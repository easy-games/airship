using Code.Player.Accessories;
using UnityEngine;
using UnityEngine.Serialization;

public class ActiveAccessory {
    public AccessoryComponent AccessoryComponent;
    public int lodLevel;
    public int maxLodLevel;
    public Transform rootTransform;
    public GameObject[] gameObjects;
    public MeshRenderer[] meshRenderers;
    public SkinnedMeshRenderer[] skinnedMeshRenderers;
    public Renderer[] renderers;
    public MeshFilter[] meshFilters;
    public ActiveAccessory[] lods;

    // public MeshRenderer[] meshRenderersLOD1;
    // public SkinnedMeshRenderer[] skinnedMeshRenderersLOD1;
    // public Renderer[] renderersLOD1;
    // public MeshFilter[] meshFiltersLOD1;
}