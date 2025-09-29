using UnityEngine;

[LuauAPI]
[ExecuteInEditMode]
public class EasyGridAlign : MonoBehaviour {
    public EngineRunMode rebuildMode = EngineRunMode.EDITOR;

    [Header("References")]
    public Transform contentHolder;

    [Header("Variables")]
    public bool centerGrid = false;

    public Vector3Int numberOfGridElements = Vector3Int.one;
    public Vector3 localGridElementSize = Vector3.one;
    public Vector3 randomLocalPositionOffset = Vector3.zero;
    public Vector3 randomLocalEulerOffset = Vector3.zero;

    private bool isDirty = true;

    private Vector3Int builtNumberOfGridElements = Vector3Int.zero;
    private Vector3 builtLocalGridElementSize = Vector3Int.zero;
    private Vector3 builtRandomLocalPositionOffset = Vector3.zero;
    private Vector3 builtRandomLocalEulerOffset = Vector3.zero;
    private bool builtCenterGrid = false;

    public void LateUpdate() {
        if (!contentHolder) {
            return;
        }

        if (EasyTooling.IsValidRunMode(rebuildMode)) {
            isDirty = builtLocalGridElementSize != localGridElementSize ||
                      builtNumberOfGridElements != numberOfGridElements ||
                      builtRandomLocalEulerOffset != randomLocalEulerOffset ||
                      builtRandomLocalPositionOffset != randomLocalPositionOffset ||
                      builtCenterGrid != centerGrid;

            if (!isDirty) {
                return;
            }

            Rebuild();
        }
    }

    public void Rebuild() {
        if (contentHolder == null) {
            contentHolder = transform;
        }

        isDirty = false;
        var startPos = new Vector3(0, 0, 0);
        if (centerGrid) {
            float count = contentHolder.childCount;
            var xCount = Mathf.Min(count, numberOfGridElements.x);
            var yCount = Mathf.Min(Mathf.Ceil(count / xCount), numberOfGridElements.y);
            var zCount = Mathf.Min(Mathf.Ceil(count / (xCount * yCount)), numberOfGridElements.z);
            startPos.x = xCount * localGridElementSize.x / -2f + localGridElementSize.x / 2f;
            startPos.y = yCount * localGridElementSize.y / -2f + localGridElementSize.y / 2f;
            startPos.z = zCount * localGridElementSize.z / -2f + localGridElementSize.z / 2f;
        }

        var localPos = startPos;
        var numberOfElements = Vector3Int.zero;
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
                localPos.x = startPos.x;
                numberOfElements.x = 0;
                numberOfElements.y++;
                localPos.y += localGridElementSize.y;
            }

            if (numberOfElements.y >= numberOfGridElements.y) {
                localPos.y = startPos.y;
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
        builtCenterGrid = centerGrid;
    }
}