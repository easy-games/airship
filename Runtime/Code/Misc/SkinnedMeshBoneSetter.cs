using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SkinnedMeshBoneSetter : MonoBehaviour {
    public SkinnedMeshRenderer sourceSkinnedMeshRenderer;
    public SkinnedMeshRenderer newSkinnedMeshRenderer;

    private void Update() {
        if (sourceSkinnedMeshRenderer != null && newSkinnedMeshRenderer != null) {
            AssignBones();
            this.enabled = false;
        }
    }
    
    private void AssignBones() {
        newSkinnedMeshRenderer.bones = sourceSkinnedMeshRenderer.bones;
        newSkinnedMeshRenderer.rootBone = sourceSkinnedMeshRenderer.rootBone;
    }
}
