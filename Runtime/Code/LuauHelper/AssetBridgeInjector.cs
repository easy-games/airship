using UnityEngine;

[ExecuteAlways]
public class AssetBridgeInjector : MonoBehaviour
{
    private void OnEnable() {
        AirshipComponent.AssetBridge = AssetBridge.Instance;
        LuauScript.AssetBridge = AssetBridge.Instance;
    }
}