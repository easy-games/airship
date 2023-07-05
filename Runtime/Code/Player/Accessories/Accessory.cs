using UnityEngine;

[CreateAssetMenu(fileName = "New Accessory", menuName = "EasyGG/BedWars/Entity Accessory", order = 0)]
public class Accessory : ScriptableObject {
    public string DisplayName;
    public AccessorySlot AccessorySlot;
    public GameObject Prefab;
    public Vector3 Position = new Vector3(0, 0, 0);
    public Vector3 Rotation = new Vector3(0, 0, 0);
    public Vector3 Scale = new Vector3(1, 1, 1);
    public bool MeshDeformed;

    private bool _checkedForSkinnedMeshes;
    private bool _hasSkinnedMeshes;

    public bool HasSkinnedMeshes {
        get {
            if (!_checkedForSkinnedMeshes) {
                _checkedForSkinnedMeshes = true;
                _hasSkinnedMeshes = Prefab.GetComponentInChildren<SkinnedMeshRenderer>() != null;
            }
            
            return _hasSkinnedMeshes;
        }
    }
}
