#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/*
[CustomEditor(typeof(VoxelWorldPositionIndicator))]
[CanEditMultipleObjects]
public class VoxelWorldPositionEditor : Editor
{
    private VoxelWorldPositionIndicator script;

    private void OnEnable()
    {
        script = (VoxelWorldPositionIndicator)target;
    }

    public override void OnInspectorGUI()
    {
        base.DrawDefaultInspector();
        if (GUILayout.Button("Spawn Block"))
        {
            var world = FindObjectOfType<VoxelWorld>();
            var pos = Vector3Int.RoundToInt(script.gameObject.transform.position);
            world.WriteVoxelAt(pos, 1, true);
        }
    }
}*/
#endif