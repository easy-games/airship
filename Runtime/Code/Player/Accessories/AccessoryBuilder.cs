using System;
using System.Collections.Generic;
using System.Linq;
using MTAssets.SkinnedMeshCombiner;
using UnityEngine;

public class AccessoryBuilder : MonoBehaviour {
	public const string boneKey = "Bones";
	[SerializeField] private SkinnedMeshCombiner combiner;
	public CapsuleCollider[] clothColliders;

	private Dictionary<AccessorySlot, List<ActiveAccessory>> _activeAccessories;
	private GameObjectReferences entityReferences;
	private SkinnedMeshRenderer referenceMesh;

	private void Awake() {
		_activeAccessories = new Dictionary<AccessorySlot, List<ActiveAccessory>>();
		entityReferences = gameObject.GetComponent<GameObjectReferences>();
		referenceMesh = entityReferences.GetValueTyped<SkinnedMeshRenderer>("Meshes", "Body");
		if (!referenceMesh) {
			Debug.LogError("Unable to find Meshes > Body on HumanEntity. Did you forget to assign the correct component type?");
		}
	}

	private void TryUndoCombine() {
		if (combiner.enabled && combiner.isMeshesCombined()) {
			combiner.UndoCombineMeshes(true, true);
		}
	}

	/// <summary>
	/// Remove all accessories from the entity.
	/// </summary>
	public void RemoveAccessories() {
		TryUndoCombine();
		
		foreach (var pair in _activeAccessories) {
			foreach (var activeAccessory in pair.Value) {
				foreach (var go in activeAccessory.gameObjects) {
					Destroy(go);
				}
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
		
		DestroyAccessorySlot(slot);
		
		TryCombineMeshes();
	}

	private void DestroyAccessorySlot(AccessorySlot slot) {
		if (_activeAccessories.TryGetValue(slot, out var accessoryObjs)) {
			foreach (var activeAccessory in accessoryObjs) {
				foreach (var go in activeAccessory.gameObjects) {
					Destroy(go);
				}
			}
			accessoryObjs.Clear();
		}
	}

	private IEnumerable<GameObject> SetupSkinnedMeshAccessory(GameObject accessory) {
		//Apply colliders to any cloth items
		//ApplyClothProperties(accessory);
		
		// Get all skinned mesh renderers in the given accessory:
		var skinnedMeshRenderers = accessory.GetComponentsInChildren<SkinnedMeshRenderer>();
		var objects = new GameObject[skinnedMeshRenderers.Length];

		// Remap the accessory's skinned mesh renderers to point to the same bones as the entity:
		for (var i = 0; i < skinnedMeshRenderers.Length; i++) {
			var smr = skinnedMeshRenderers[i];
			
			smr.transform.parent = combiner.transform;
			smr.rootBone = referenceMesh.rootBone;
			smr.bones = referenceMesh.bones;
			
			objects[i] = smr.gameObject;
		}

		return objects;
	}

	public ActiveAccessory SetAccessory(Accessory accessory)
	{
		return AddAccessories(new List<Accessory>() {accessory}, AccessoryAddMode.Replace)[0];
	}

	public ActiveAccessory[] EquipAccessoryCollection(AccessoryCollection collection) {
		return AddAccessories(collection.accessories, AccessoryAddMode.Replace);
	}
	
	/// <summary>
	/// Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
	/// accessories to be added to the entity, assuming other accessories might already exist
	/// on the entity.
	/// </summary>
	/// <param name="accessories">Accessories to add.</param>
	/// <param name="addMode">The add behavior.</param>
	public ActiveAccessory[] AddAccessories(List<Accessory> accessories, AccessoryAddMode addMode)
	{
		List<ActiveAccessory> addedAccessories = new List<ActiveAccessory>();
		// foreach (var accessory in accessories)
		// {
		// 	if (accessory.MeshDeformed)
		// 	{
		// 		shouldMeshCombine = true;
		// 		break;
		// 	}
		// }
		
		TryUndoCombine();

		// In 'Replace' mode, remove all accessories that are in the slots of the new accessories:
		if (addMode == AccessoryAddMode.Replace) {
			foreach (var accessory in accessories) {
				this.DestroyAccessorySlot(accessory.AccessorySlot);
			}
		}
		// In 'ReplaceAll' mode, remove all existing accessories:
		else if (addMode == AccessoryAddMode.ReplaceAll) {
			foreach (var pair in _activeAccessories) {
				foreach (var activeAccessory in pair.Value) {
					foreach (var obj in activeAccessory.gameObjects) {
						Destroy(obj);
					}
				}
				pair.Value.Clear();
			}
		}

		// Add accessories:
		foreach (var accessory in accessories) {
			if (!_activeAccessories.ContainsKey(accessory.AccessorySlot)) {
				_activeAccessories.Add(accessory.AccessorySlot, new List<ActiveAccessory>());
			}

			// In 'AddIfNone' mode, don't add the accessory if one already exists in the slot:
			if (addMode == AccessoryAddMode.AddIfNone && _activeAccessories[accessory.AccessorySlot].Count > 0) {
				continue;
			}

			if (accessory.MeshDeformed) {
				var newAccessoryObj = Instantiate(accessory.Prefab, transform);
				var gameObjects = SetupSkinnedMeshAccessory(newAccessoryObj);
				List<Renderer> renderers = new(gameObjects.Count());
				foreach (var go in gameObjects) {
					var ren = go.GetComponent<SkinnedMeshRenderer>();
					renderers.Add(ren);
				}
				ActiveAccessory activeAccessory = new() {
					accessory = accessory,
					gameObjects = gameObjects.ToArray(),
					renderers = renderers.ToArray()
				};
				addedAccessories.Add(activeAccessory);
				_activeAccessories[accessory.AccessorySlot].Add(activeAccessory);
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

				List<Renderer> renderers = new();
				var rens = newAccessoryObj.GetComponentsInChildren<Renderer>();
				foreach (var ren in rens) {
					renderers.Add(ren);
				}

				ActiveAccessory activeAccessory = new ActiveAccessory() {
					accessory = accessory,
					gameObjects = new[] { newAccessoryObj },
					renderers = renderers.ToArray()
				};

				//Apply colliders to any cloth items
				//Don't need for static accessories?
				//ApplyClothProperties(newAccessoryObj);
				
				_activeAccessories[accessory.AccessorySlot].Add(activeAccessory);
				addedAccessories.Add(activeAccessory);
			}
		}
		
		TryCombineMeshes();

		return addedAccessories.ToArray();
	}
	
	private void ApplyClothProperties(GameObject root) {
		foreach (var cloth in root.GetComponentsInChildren<Cloth>()) {
			cloth.capsuleColliders = clothColliders;
		}
	}

	private void TryCombineMeshes() {
		if (combiner.enabled) {
			combiner.CombineMeshes();
		}
	}

	public ActiveAccessory[] GetActiveAccessoriesBySlot(AccessorySlot target) {
		if (_activeAccessories.TryGetValue(target, out List<ActiveAccessory> items)) {
			return items.ToArray();
		}

		return Array.Empty<ActiveAccessory>();
	}

	public ActiveAccessory[] GetActiveAccessories() {
		var results = new List<ActiveAccessory>();
		foreach (var keyValuePair in _activeAccessories) {
			foreach (var activeAccessory in keyValuePair.Value) {
				results.Add(activeAccessory);
			}
		}

		return results.ToArray();
	}

	public Renderer[] GetAllAccessoryMeshes() {
		var renderers = new List<Renderer>();
		foreach (var keyValuePair in _activeAccessories) {
			foreach (var activeAccessory in keyValuePair.Value) {
				foreach (var go in activeAccessory.gameObjects) {
					var rens = go.GetComponentsInChildren<Renderer>();
					for (int i = 0; i < rens.Length; i++) {
						renderers.Add(rens[i]);
					}
				}
			}
		}
		return renderers.ToArray();
	}

	public Renderer[] GetAccessoryMeshes(AccessorySlot slot) {
		var renderers = new List<Renderer>();
		var activeAccessories = GetActiveAccessoriesBySlot(slot);
		foreach (var aa in activeAccessories) {
			foreach (var go in aa.gameObjects) {
				var rens = go.GetComponentsInChildren<Renderer>();
				foreach (var ren in rens) {
					renderers.Add(ren);
				}
			}
		}
		return renderers.ToArray();
	}
	
	public ParticleSystem[] GetAccessoryParticles(AccessorySlot slot) {
		var results = new List<ParticleSystem>();
		var activeAccessories = GetActiveAccessoriesBySlot(slot);
		foreach (var aa in activeAccessories) {
			foreach (var go in aa.gameObjects) {
				var particles = go.GetComponentsInChildren<ParticleSystem>();
				foreach (var particle in particles) {
					results.Add(particle);
				}
			}
		}
		return results.ToArray();
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
