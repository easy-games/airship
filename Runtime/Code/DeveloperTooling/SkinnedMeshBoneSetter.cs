using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SkinnedMeshBoneSetter : MonoBehaviour {
    public enum RemappingMode {
        EXISTING_SKINNED_MESH,
        EXISTING_ARMATURE,
    }
    [Tooltip("Existing Mesh copies bones from a mesh that already has the correct armature assigned. Existing armature finds bones on your target skinned mesh and attempts to find them in the new target Root.")]
    public RemappingMode remappingMode = RemappingMode.EXISTING_ARMATURE;
    public SkinnedMeshRenderer sourceSkinnedMeshRenderer;
    public Transform targetRootBone;
    public SkinnedMeshRenderer[] targetSkinnedMeshRenderers = {};
    
    public void ApplyBones() {
        switch (remappingMode){
            case RemappingMode.EXISTING_SKINNED_MESH:
                Debug.Log("Copying bones from the existing Skinned Mesh to the target skinned meshes");
                foreach (var meshRen in targetSkinnedMeshRenderers) {
                    AssignBonesFromSkinnedSource(meshRen);
                } 
                break;
            case RemappingMode.EXISTING_ARMATURE:
                Debug.Log("Using bones from the existing armature and searching for matching bones on the target armature");
                foreach (var meshRen in targetSkinnedMeshRenderers) {
                    AssignBonesFromExistingArmature(meshRen);
                }
                break;
        }
    }

    private void AssignBonesFromSkinnedSource(SkinnedMeshRenderer target) {
        int i = 0;
        foreach (var bone in sourceSkinnedMeshRenderer.bones) {
            i++;
        }
        target.bones = sourceSkinnedMeshRenderer.bones;
        target.rootBone = sourceSkinnedMeshRenderer.rootBone;
    }

    private void AssignBonesFromExistingArmature(SkinnedMeshRenderer target) {
        //Loop through target to get existing bones
        Queue<string> originalBoneNames = new Queue<string>();
        foreach (var boneTransform in targetSkinnedMeshRenderers[0].bones) {
            originalBoneNames.Enqueue(boneTransform.name.ToLower());
        }
        //Grab the new bone from the target armature
        List<Transform> newBones = new List<Transform>();
        Transform[] targetArmature = targetRootBone.parent.GetComponentsInChildren<Transform>();
        int i = 0;
        while (originalBoneNames.Count > 0) {
            var boneName = originalBoneNames.Dequeue();
            bool foundBone = false;
            foreach (var boneTransform in targetArmature) {
                if (boneTransform.name.ToLower().Equals(boneName)) {
                    //Found bone
                    newBones.Add(boneTransform);
                    foundBone = true;
                    break;
                }
            }
            i++;
            if (!foundBone) {
                Debug.LogError("Unable to find bone \"" + boneName + "\" in target armature");
            }
        }
        target.rootBone = targetRootBone;
        target.bones = newBones.ToArray();
    }
}
