using UnityEngine;

[CreateAssetMenu(fileName = "New Accessory", menuName = "Airship/Accessories/Entity Accessory", order = 0)]
public class Accessory : ScriptableObject {
    public enum VisibilityMode {
        THIRD_PERSON,
        FIRST_PERSON,
        BOTH
    }
    
    public string DisplayName;
    public AccessorySlot AccessorySlot;
    public GameObject Prefab;
    public Vector3 Position = new Vector3(0, 0, 0);
    public Vector3 Rotation = new Vector3(0, 0, 0);
    public Vector3 Scale = new Vector3(1, 1, 1);
    public VisibilityMode visibilityMode = VisibilityMode.BOTH;

    private bool _checkedForSkinnedMeshes = false;
    private bool _hasSkinnedMeshes = false;

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
