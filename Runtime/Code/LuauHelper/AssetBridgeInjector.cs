using UnityEngine;

[ExecuteAlways]
public class AssetBridgeInjector : MonoBehaviour
{
    private void OnEnable() {
        AirshipModuleScript.AssetBridge = AssetBridge.Instance;
    }
}