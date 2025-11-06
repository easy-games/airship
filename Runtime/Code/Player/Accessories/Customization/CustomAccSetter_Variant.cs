using UnityEngine;

public class CustomAccSetter_Variant : MonoBehaviour {
    [Header("References")]
    public Renderer[] affectedRenderers;

    [Header("Variables")]
    public VariantGroup[] variants;

    public void Set(int variantIndex) {
        var num = this.variants.Length;
        if (num > 0 && num > variantIndex) {
            var variant = variants[variantIndex];
            // Set a custom material
            if (variant.customMat != null) {
                foreach (var ren in affectedRenderers) {
                    ren.material = variant.customMat;
                }
            }
            // Set a custom Mesh
            if (variant.customMesh != null) {
                foreach (var ren in affectedRenderers) {
                    ren.material = variant.customMat;
                    var skinned = ren as SkinnedMeshRenderer;
                    if (skinned) {
                        skinned.sharedMesh = variant.customMesh;
                    } else {
                        ren.gameObject.GetComponent<MeshFilter>().sharedMesh = variant.customMesh;
                    }
                }
            }
        }
    }
}

[System.Serializable]
public class VariantGroup {
    public Mesh customMesh = null;
    public Material customMat = null;
}
