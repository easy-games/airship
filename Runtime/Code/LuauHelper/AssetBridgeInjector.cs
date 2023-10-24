using UnityEngine;

[ExecuteAlways]
public class AssetBridgeInjector : MonoBehaviour
{
    private void OnEnable() {
        ScriptBinding.AssetBridge = AssetBridge.Instance;
    }
}