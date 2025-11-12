using System.Collections.Generic;
using UnityEngine;

public class CustomAccSetter_Variant : MonoBehaviour {
    [Header("References")]
    public Renderer[] affectedRenderers;

    [Header("Variables")]
    public VariantGroup[] variants;

    public void Set(int variantIndex) {
        var num = variants.Length;
        if (num > 0 && num > variantIndex) {
            var variant = variants[variantIndex];
            if (variant.customMat && !variant.customMat.shader.isSupported) {
                variant.customMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            foreach (var ren in affectedRenderers) {
                // // Set a custom Mesh
                if (variant.customMesh != null) {
                    var skinned = ren as SkinnedMeshRenderer;
                    if (skinned) {
                        skinned.sharedMesh = variant.customMesh;
                    } else {
                        ren.gameObject.GetComponent<MeshFilter>().sharedMesh = variant.customMesh;
                    }
                }

                // Set a custom material
                if (variant.customMat != null) {
                    ren.material = variant.customMat;
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