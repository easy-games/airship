using System;
using System.Collections.Generic;
using MTAssets.SkinnedMeshCombiner;
using UnityEngine;

public class AccessoryBuilder : MonoBehaviour {
	public const string boneKey = "Bones";
	
	[SerializeField] private SkinnedMeshRenderer hip;
	[SerializeField] private SkinnedMeshCombiner combiner;
	public CapsuleCollider[] clothColliders;

	private Dictionary<AccessorySlot, List<GameObject>> _currentAccessories;
	private GameObjectReferences entityReferences;

	private void Awake() {
		_currentAccessories = new Dictionary<AccessorySlot, List<GameObject>>();
		entityReferences = gameObject.GetComponent<GameObjectReferences>();
	}

	private void TryUndoCombine() {
		if (combiner.isMeshesCombined()) {
			combiner.UndoCombineMeshes(true, true);
		}
	}

	/// <summary>
	/// Remove all accessories from the entity.
	/// </summary>
	public void RemoveAccessories() {
		TryUndoCombine();
		
		foreach (var pair in _currentAccessories) {
			foreach (var obj in pair.Value) {
				Destroy(obj);
			}
			pair.Value.Clear();
		}
	}

	/// <summary>
	/// Remove all accessories from the entity that are in the given slot.
	/// </summary>
	/// <param name="slot">Slot from which to remove accessories.</param>
	public void RemoveAccessorySlot(AccessorySlot slot) {
		TryUndoCombine();
		
		if (_currentAccessories.TryGetValue(slot, out var accessoryObjs)) {
			foreach (var accessoryObj in accessoryObjs) {
				accessoryObj.SetActive(false);
				Destroy(accessoryObj);
			}
			accessoryObjs.Clear();
		}

		CombineMeshes();
	}

	private IEnumerable<GameObject> SetupSkinnedMeshAccessory(GameObject accessory) {
		//Apply colliders to any cloth items
		ApplyClothProperties(accessory);
		
		// Get all skinned mesh renderers in the given accessory:
		var skinnedMeshRenderers = accessory.GetComponentsInChildren<SkinnedMeshRenderer>();
		var objects = new GameObject[skinnedMeshRenderers.Length];

		// Remap the accessory's skinned mesh renderers to point to the same bones as the entity:
		for (var i = 0; i < skinnedMeshRenderers.Length; i++) {
			var smr = skinnedMeshRenderers[i];
			
			smr.transform.parent = combiner.transform;
			smr.rootBone = hip.rootBone;
			smr.bones = hip.bones;
			
			objects[i] = smr.gameObject;
		}

		return objects;
	}

	public void SetAccessory(Accessory accessory)
	{
		AddAccessories(new List<Accessory>() {accessory}, AccessoryAddMode.Replace);
	}

	public void SetAccessoryKit(AccessoryKit kit) {
		AddAccessories(kit.accessories, AccessoryAddMode.Replace);
	}
	
	/// <summary>
	/// Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
	/// accessories to be added to the entity, assuming other accessories might already exist
	/// on the entity.
	/// </summary>
	/// <param name="accessories">Accessories to add.</param>
	/// <param name="addMode">The add behavior.</param>
	public void AddAccessories(List<Accessory> accessories, AccessoryAddMode addMode)
	{
		bool shouldMeshCombine = false;
		// foreach (var accessory in accessories)
		// {
		// 	if (accessory.MeshDeformed)
		// 	{
		// 		shouldMeshCombine = true;
		// 		break;
		// 	}
		// }

		if (shouldMeshCombine)
		{
			TryUndoCombine();
		}
		

		// In 'Replace' mode, remove all accessories that are in the slots of the new accessories:
		if (addMode == AccessoryAddMode.Replace) {
			foreach (var accessory in accessories) {
				if (_currentAccessories.TryGetValue(accessory.AccessorySlot, out var accessoryObjs)) {
					foreach (var existing in accessoryObjs) {
						Destroy(existing);
					}
					_currentAccessories[accessory.AccessorySlot].Clear();
				}
			}
		}
		// In 'ReplaceAll' mode, remove all existing accessories:
		else if (addMode == AccessoryAddMode.ReplaceAll) {
			foreach (var pair in _currentAccessories) {
				foreach (var obj in pair.Value) {
					Destroy(obj);
				}
				pair.Value.Clear();
			}
		}

		// Add accessories:
		foreach (var accessory in accessories) {
			if (!_currentAccessories.ContainsKey(accessory.AccessorySlot)) {
				_currentAccessories.Add(accessory.AccessorySlot, new List<GameObject>());
			}

			// In 'AddIfNone' mode, don't add the accessory if one already exists in the slot:
			if (addMode == AccessoryAddMode.AddIfNone && _currentAccessories[accessory.AccessorySlot].Count > 0) {
				continue;
			}

			if (accessory.MeshDeformed) {
				var newAccessoryObj = Instantiate(accessory.Prefab, transform);
				var gameObjects = SetupSkinnedMeshAccessory(newAccessoryObj);
				foreach (var go in gameObjects) {
					_currentAccessories[accessory.AccessorySlot].Add(go);
				}
			} else {
				// TODO: Anything for static meshes
				Transform parent;
				if (accessory.AccessorySlot == AccessorySlot.Root) {
					parent = combiner.manualRootBoneToUse;
				} else {
					string itemKey = GetBoneItemKey(accessory.AccessorySlot);
					if (string.IsNullOrEmpty(itemKey)) {
						parent = combiner.transform;
					} else {
						parent = entityReferences.GetValueTyped<Transform>(boneKey, itemKey);
					}
				}
				var newAccessoryObj = Instantiate(accessory.Prefab, parent);

				newAccessoryObj.transform.localScale = accessory.Scale;
				newAccessoryObj.transform.localEulerAngles = accessory.Rotation;
				newAccessoryObj.transform.localPosition = accessory.Position;

				//Apply colliders to any cloth items
				//Don't need for static accessories?
				//ApplyClothProperties(newAccessoryObj);
				
				_currentAccessories[accessory.AccessorySlot].Add(newAccessoryObj);
			}
		}

		if (shouldMeshCombine)
		{
			CombineMeshes();	
		}
	}
	
	private void ApplyClothProperties(GameObject root) {
		foreach (var cloth in root.GetComponentsInChildren<Cloth>()) {
			cloth.capsuleColliders = clothColliders;
		}
	}

	private void CombineMeshes() {
		if (combiner.enabled) {
			combiner.CombineMeshes();
		}
	}

	public GameObject[] GetAccessories(AccessorySlot target) {
		if (_currentAccessories.TryGetValue(target, out List<GameObject> items)) {
			return items.ToArray();
		}

		return Array.Empty<GameObject>();
	}
	
	public Renderer[] GetAllAccessoryMeshes() {
		var renderers = new List<Renderer>();
		foreach (var keyValuePair in _currentAccessories) {
			foreach (var go in keyValuePair.Value) {
				var rens = go.GetComponentsInChildren<Renderer>();
				for (int i = 0; i < rens.Length; i++) {
					renderers.Add(rens[i]);
				}
			}
		}
		return renderers.ToArray();
	}

	public Renderer[] GetAccessoryMeshes(AccessorySlot slot) {
		var renderers = new List<Renderer>();
		var gos = GetAccessories(slot);
		for (int i = 0; i < gos.Length; i++) {
			var rens = gos[i].GetComponentsInChildren<Renderer>();
			for (int j = 0; j < rens.Length; j++) {
				renderers.Add(rens[j]);
			}
		}
		return renderers.ToArray();
	}
	
	public ParticleSystem[] GetAccessoryParticles(AccessorySlot slot) {
		var renderers = new List<ParticleSystem>();
		var gos = GetAccessories(slot);
		for (int i = 0; i < gos.Length; i++) {
			var particles = gos[i].GetComponentsInChildren<ParticleSystem>();
			for (int j = 0; j < particles.Length; j++) {
				renderers.Add(particles[j]);
			}
		}
		return renderers.ToArray();
	}
	
	public static string GetBoneItemKey(AccessorySlot slot) {
		switch (slot) {
			case AccessorySlot.RightHand:
				return "HandR";
			case AccessorySlot.LeftHand:
				return "HandL";
			case AccessorySlot.Shirt:
				return "Torso";
			case AccessorySlot.Hat:
			case AccessorySlot.Hair:
				return "HeadTop";
			case AccessorySlot.Root:
				return "Root";
			default:
				return "";
		}
	}
}
