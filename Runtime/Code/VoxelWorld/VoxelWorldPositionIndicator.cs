using System;
using UnityEngine;

/*
[ExecuteInEditMode]
public class VoxelWorldPositionIndicator : MonoBehaviour {
    public VoxelWorld voxelWorld;

    private string existingName = string.Empty;

    [SerializeField, Tooltip("True to make the Minecraft Map Loader not replace this value.")]
    public bool doNotOverwrite = false;

    public void Init(VoxelWorld voxelWorld) {
        this.voxelWorld = voxelWorld;
    }

    private void Start()
    {
        this.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        if (!Application.isPlaying) {
            voxelWorld.worldPositionEditorIndicators.Remove(gameObject.name);
            voxelWorld.worldPositionEditorIndicators.Add(gameObject.name, transform);
            this.existingName = gameObject.name;
        }
    }

    private void Update() {
        if (gameObject.name != this.existingName) {
            voxelWorld.worldPositionEditorIndicators.Remove(existingName);
            voxelWorld.worldPositionEditorIndicators.TryAdd(gameObject.name, transform);
            this.existingName = gameObject.name;
        }
    }
}*/