using UnityEngine;

[ExecuteInEditMode]
public class EasyGridAlign : MonoBehaviour {
    public EngineRunMode rebuildMode = EngineRunMode.EDITOR;
    [Header("References")]
    public Transform contentHolder;

    [Header("Variables")]
    public Vector3Int numberOfGridElements = Vector3Int.one;
    public Vector3 localGridElementSize = Vector3.one;
    public Vector3 randomLocalPositionOffset = Vector3.zero;
    public Vector3 randomLocalEulerOffset = Vector3.zero;

    private bool isDirty = true;

    private Vector3Int builtNumberOfGridElements = Vector3Int.zero;
    private Vector3 builtLocalGridElementSize = Vector3Int.zero;
    private Vector3 builtRandomLocalPositionOffset = Vector3.zero;
    private Vector3 builtRandomLocalEulerOffset = Vector3.zero;
    
    public void LateUpdate() {
        if (rebuildMode == EngineRunMode.NONE) {
            return;
        }
        if (Application.isPlaying && rebuildMode == EngineRunMode.EDITOR) {
            isDirty = false;
            return;
        }

        isDirty = builtLocalGridElementSize != localGridElementSize ||
                  builtNumberOfGridElements != numberOfGridElements ||
                  builtRandomLocalEulerOffset != randomLocalEulerOffset ||
                  builtRandomLocalPositionOffset != randomLocalPositionOffset;
        
        if (!isDirty || !contentHolder) {
            return;
        }
        Rebuild();
    }

    public void Rebuild() {
        if (contentHolder == null) {
            contentHolder = this.transform;
        }
        
        isDirty = false;
        Vector3 localPos = Vector3.zero;
        Vector3Int numberOfElements = Vector3Int.zero;
        foreach (Transform child in contentHolder) {
            child.position = contentHolder.TransformPoint(localPos + new Vector3(
                Random.Range(-randomLocalPositionOffset.x, randomLocalPositionOffset.x),
                Random.Range(-randomLocalPositionOffset.y, randomLocalPositionOffset.y),
                Random.Range(-randomLocalPositionOffset.z, randomLocalPositionOffset.z)));
            child.localEulerAngles = new Vector3(
                Random.Range(-randomLocalEulerOffset.x, randomLocalEulerOffset.x),
                Random.Range(-randomLocalEulerOffset.y, randomLocalEulerOffset.y),
                Random.Range(-randomLocalEulerOffset.z, randomLocalEulerOffset.z));
            localPos.x += localGridElementSize.x;
            numberOfElements.x++;
            if (numberOfElements.x >= numberOfGridElements.x) {
                localPos.x = 0;
                numberOfElements.x = 0;
                numberOfElements.y++;
                localPos.y += localGridElementSize.y;
            }
            if (numberOfElements.y >= numberOfGridElements.y) {
                localPos.y = 0;
                numberOfElements.y = 0;
                numberOfElements.z++;
                localPos.z += localGridElementSize.z;
            }
            if (numberOfElements.z >= numberOfGridElements.z) {
                //Debug.LogWarning("Grid overflow: " + contentHolder.name);
            }
        }

        builtLocalGridElementSize = localGridElementSize;
        builtNumberOfGridElements = numberOfGridElements;
        builtRandomLocalEulerOffset = randomLocalEulerOffset;
        builtRandomLocalPositionOffset = randomLocalPositionOffset;
    }
}
