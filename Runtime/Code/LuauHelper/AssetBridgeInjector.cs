using UnityEngine;

[ExecuteAlways]
public class AssetBridgeInjector : MonoBehaviour
{
    private void OnEnable() {
        AirshipRuntimeScript.AssetBridge = AssetBridge.Instance;
    }
}