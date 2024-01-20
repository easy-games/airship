using UnityEngine;

public class PrewarmShaderVariants : MonoBehaviour {
    public ShaderVariantCollection shaderVariantCollection;

    private void Start() {
        shaderVariantCollection.WarmUp();
    }
}