using System;
using System.Collections.Generic;
using UnityEngine;

[LuauAPI]
[ExecuteInEditMode]
public class CharacterRig : MonoBehaviour {
    
    [Header("Meshes")]
	public SkinnedMeshRenderer bodyMesh;
    public SkinnedMeshRenderer armsMesh;
    public Renderer headMesh;
	public Renderer faceMesh;

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
	public Transform heldItemL;
	public Transform heldItemR;

    [NonSerialized]
    public Renderer[] baseMeshes; //All skin based Meshes (not face decal)

    private void Awake(){
        List<Renderer> meshes = new List<Renderer>();
        if(bodyMesh != null){
            meshes.Add(bodyMesh);
        }
        if (armsMesh != null)
        {
            meshes.Add(armsMesh);
        }
        if (headMesh != null){
            meshes.Add(headMesh);
        }
        baseMeshes = meshes.ToArray();   
    }

    public Transform GetSlotTransform(AccessorySlot slot)
    {
        switch (slot){
                
                //HEAD
                case AccessorySlot.Head:
                case AccessorySlot.Hair:
                case AccessorySlot.Face:
                case AccessorySlot.Ears:
                case AccessorySlot.Nose:
                    return head;
                case AccessorySlot.Neck:
                    return neck;
                case AccessorySlot.Waist:

                //TORSO
                case AccessorySlot.Torso:
                case AccessorySlot.TorsoInner:
                case AccessorySlot.TorsoOuter:
                    return spine;
                case AccessorySlot.Backpack:
                    return spineChest;

                //ARMS
                case AccessorySlot.Hands:
                case AccessorySlot.RightHand:
                    return heldItemR;
                case AccessorySlot.LeftHand:
                    return heldItemL;
                case AccessorySlot.LeftArmUpper:
                    return upperArmL;
                case AccessorySlot.LeftArmLower:
                    return forearmL;
                case AccessorySlot.RightArmUpper:
                    return upperArmR;
                case AccessorySlot.RightArmLower:
                    return forearmR;

                //LEGS
                case AccessorySlot.Legs:
                case AccessorySlot.Feet:
                    return hips;
                case AccessorySlot.LeftLegUpper:
                    return thighL;
                case AccessorySlot.LeftLegLower:
                    return shinL;
                case AccessorySlot.RightLegUpper:
                    return thighL;
                case AccessorySlot.RightLegLower:
                    return shinL;
                case AccessorySlot.LeftFoot:
                    return footL;
                case AccessorySlot.RightFoot:
                    return footR;
                case AccessorySlot.Root:
                    return transform;
                default:
                    return rootMotion;
            }
    }
}
