using UnityEngine;

[ExecuteAlways]
public class AssetBridgeInjector : MonoBehaviour {
    private void OnEnable() {
        LuauScript.AssetBridge = AssetBridge.Instance;
    }
}
