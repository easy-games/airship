using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterRig : MonoBehaviour {
    
    [Header("Meshes")]
	public SkinnedMeshRenderer bodyMesh;
	public SkinnedMeshRenderer headMesh;
	public SkinnedMeshRenderer faceMesh;

	[Header("Root")]
	public Transform rigHolder;
	public Transform rootMotion;
	public Transform master;

	[Header("Spine")]
	public Transform hips;
	public Transform spine;
	public Transform head;

	[Header("Left Arm")]
	public Transform upperArmL;
	public Transform forearmL;
	public Transform handL;
	public Transform fingersL;
	public Transform thumbL;

	[Header("Right Arm")]
	public Transform upperArmR;
	public Transform forearmR;
	public Transform handR;
	public Transform fingersR;
	public Transform thumbR;

	[Header("Left Leg")]
	public Transform thighL;
	public Transform shinL;
	public Transform footL;

	[Header("Right Leg")]
	public Transform thighR;
	public Transform shinR;
	public Transform footR;

	[Header("Accessory Slots")]
	public Transform headTop;
	public Transform neck;
	public Transform spineChest;

    [NonSerialized]
    public SkinnedMeshRenderer[] baseMeshes;

    private void Awake(){
        List<SkinnedMeshRenderer> meshes = new List<SkinnedMeshRenderer>();
        if(bodyMesh != null){
            meshes.Add(bodyMesh);
        }
        if(headMesh != null){
            meshes.Add(headMesh);
        }
        if(faceMesh != null){
            meshes.Add(faceMesh);
        }
        baseMeshes = meshes.ToArray();   
    }

    public Transform GetSlotTransform(AccessorySlot slot)
    {
        switch (slot){
                case AccessorySlot.RightHand:
                    return handR;
                case AccessorySlot.LeftHand:
                    return handL;
                case AccessorySlot.Torso:
                case AccessorySlot.TorsoInner:
                case AccessorySlot.TorsoOuter:
                    return spine;
                case AccessorySlot.Backpack:
                    return spineChest;
                case AccessorySlot.Head:
                case AccessorySlot.Hair:
                case AccessorySlot.Face:
                case AccessorySlot.Ears:
                case AccessorySlot.Nose:
                    return head;
                case AccessorySlot.Neck:
                    return neck;
                case AccessorySlot.Waist:
                case AccessorySlot.Legs:
                    return hips;
                case AccessorySlot.Feet:
                case AccessorySlot.Root:
                    return rootMotion;
                default:
                    return rootMotion;
            }
    }
}
