#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoxelWorldPosition))]
[CanEditMultipleObjects]
public class VoxelWorldPositionEditor : Editor
{
    private VoxelWorldPosition script;

    private void OnEnable()
    {
        script = (VoxelWorldPosition)target;
    }

    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("SpawnBlock"))
        {
            var world = FindObjectOfType<VoxelWorld>();
            var pos = Vector3Int.RoundToInt(script.gameObject.transform.position);
            world.WriteVoxelAt(pos, 1, true);
        }
    }
}
#endif