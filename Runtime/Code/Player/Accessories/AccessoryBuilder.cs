using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Airship;
using Animancer;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class AccessoryBuilder : MonoBehaviour {
	public const string boneKey = "Bones";
	[SerializeField] private MeshCombiner meshCombiner;

	private Dictionary<AccessorySlot, List<ActiveAccessory>> _activeAccessories;
	public bool firstPerson = false;
	private Transform graphicsRoot;
	private GameObjectReferences entityReferences;
	private SkinnedMeshRenderer[] baseMeshesThirdPerson;
	private SkinnedMeshRenderer[] baseMeshesFirstPerson;
	// private SkinnedMeshRenderer[] allBaseMeshes;
	private SkinnedMeshRenderer faceMesh;

[HideInInspector]
	public int firstPersonLayer = LayerMask.NameToLayer("FirstPerson");
[HideInInspector]
	public int thirdPersonLayer = LayerMask.NameToLayer("Character");
	
	private void Awake() {
		_activeAccessories = new Dictionary<AccessorySlot, List<ActiveAccessory>>();
		entityReferences = gameObject.GetComponent<GameObjectReferences>();
		graphicsRoot = entityReferences.GetValueTyped<Transform>("Bones", "Root");

		faceMesh = entityReferences.GetValueTyped<SkinnedMeshRenderer>("Meshes", "Face");
		//Third Person Body
		baseMeshesThirdPerson = new [] {
			entityReferences.GetValueTyped<SkinnedMeshRenderer>("Meshes", "Body"),
			entityReferences.GetValueTyped<SkinnedMeshRenderer>("Meshes", "Head"),
			//faceMesh
		};

		//First Person Body
		baseMeshesFirstPerson = new [] {
			entityReferences.GetValueTyped<SkinnedMeshRenderer>("Meshes", "FirstPerson")
		};

		//Pack body meshes into one array
		// allBaseMeshes = new SkinnedMeshRenderer[baseMeshesFirstPerson.Length + baseMeshesThirdPerson.Length];
		// int allMeshesI = 0;
		// for (int i = 0; i < baseMeshesThirdPerson.Length; i++) {
		// 	allBaseMeshes[allMeshesI] = baseMeshesThirdPerson[i];
		// 	allMeshesI++;
		// }
		// for (int i = 0; i < baseMeshesFirstPerson.Length; i++) {
		// 	allBaseMeshes[allMeshesI] = baseMeshesFirstPerson[i];
		// 	allMeshesI++;
		// }

		if (!this.firstPerson && (!baseMeshesThirdPerson[0] || !baseMeshesThirdPerson[1])) {
			Debug.LogError("Unable to find base third person meshes. Did you forget to assign the correct component type on the reference builder?");
		}
		if (this.firstPerson && !baseMeshesFirstPerson[0]) {
			Debug.LogError("Unable to find base first person meshes. Did you forget to assign the correct component type on the reference builder?");
		}
	}

	private void OnEnable() {
		meshCombiner.OnCombineComplete += OnCombineComplete;
	}

	private void OnDisable() {
		meshCombiner.OnCombineComplete -= OnCombineComplete;
	}

	/// <summary>
	/// Remove all accessories from the entity.
	/// </summary>
	public void RemoveAccessories() {
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
	public void RemoveAccessorySlot(AccessorySlot slot, bool rebuildMeshImmediately) {

		DestroyAccessorySlot(slot);

		if (rebuildMeshImmediately) {
			TryCombineMeshes();
		}
	}

	private void DestroyAccessorySlot(AccessorySlot slot) {
		if (_activeAccessories.TryGetValue(slot, out var accessoryObjs)) {
			foreach (var activeAccessory in accessoryObjs) {
				Destroy(activeAccessory.rootTransform.gameObject);
			}
			accessoryObjs.Clear();
		}
	}

	public ActiveAccessory AddSingleAccessory(AccessoryComponent accessoryTemplate, bool rebuildMeshImmediately)
	{
		return AddAccessories(new List<AccessoryComponent>() {accessoryTemplate}, AccessoryAddMode.Replace, rebuildMeshImmediately)[0];
	}

	public ActiveAccessory[] EquipAccessoryCollection(AccessoryCollection collection, bool rebuildMeshImmediately = true) {
		if (collection.customSkin) {
			AddSkinAccessory(collection.customSkin, false);
		}
		return AddAccessories(collection.accessories, AccessoryAddMode.Replace, rebuildMeshImmediately);
	}
	
	/// <summary>
	/// Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
	/// accessories to be added to the entity, assuming other accessories might already exist
	/// on the entity.
	/// </summary>
	/// <param name="accessoryTemplates">Accessories to add.</param>
	/// <param name="addMode">The add behavior.</param>
	public ActiveAccessory[] AddAccessories(List<AccessoryComponent> accessoryTemplates, AccessoryAddMode addMode, bool rebuildMeshImmediately)
	{
		List<ActiveAccessory> addedAccessories = new List<ActiveAccessory>();

		// In 'Replace' mode, remove all accessories that are in the slots of the new accessories:
		if (addMode == AccessoryAddMode.Replace) {
			foreach (var accessory in accessoryTemplates) {
				this.DestroyAccessorySlot(accessory.accessorySlot);
			}
		}
		// In 'ReplaceAll' mode, remove all existing accessories:
		else if (addMode == AccessoryAddMode.ReplaceAll) {
			foreach (var pair in _activeAccessories) {
				foreach (var activeAccessory in pair.Value) {
					Destroy(activeAccessory.rootTransform.gameObject);
				}
				pair.Value.Clear();
			}
		}

		// Add accessories:
		foreach (var accessoryTemplate in accessoryTemplates) {
			if (!_activeAccessories.ContainsKey(accessoryTemplate.accessorySlot)) {
				_activeAccessories.Add(accessoryTemplate.accessorySlot, new List<ActiveAccessory>());
			}

			// In 'AddIfNone' mode, don't add the accessory if one already exists in the slot:
			if (addMode == AccessoryAddMode.AddIfNone && _activeAccessories[accessoryTemplate.accessorySlot].Count > 0) {
				continue;
			}

			//Create the accessory game object
			ActiveAccessory? activeAccessory = null;
			Renderer[] renderers;
			GameObject[] gameObjects;
			GameObject newAccessoryObj;
			if (accessoryTemplate.skinnedToCharacter && accessoryTemplate.HasSkinnedMeshes) {
				//Anything for skinned meshes connected to the main character
				//Create the prefab at the root
				newAccessoryObj = Instantiate(accessoryTemplate.gameObject, graphicsRoot);
				renderers = newAccessoryObj.GetComponentsInChildren<SkinnedMeshRenderer>();
			} else {
				//Anything for static meshes
				Transform parent = GetSlotTransform(accessoryTemplate.accessorySlot);
				//Create the prefab on the joint
				newAccessoryObj = Instantiate(accessoryTemplate.gameObject, parent);
				renderers = newAccessoryObj.GetComponentsInChildren<Renderer>();
			}
			
			//Remove (Clone) from name
			newAccessoryObj.name = accessoryTemplate.gameObject.name;

			//Collect game object references
			gameObjects = new GameObject[renderers.Length];
			for (var i = 0; i < renderers.Length; i++) {
				gameObjects[i] = renderers[i].gameObject;
			}
			
			//Any type of renderer
			activeAccessory = new() {
				AccessoryComponent = newAccessoryObj.GetComponent<AccessoryComponent>(),
				rootTransform = newAccessoryObj.transform,
				gameObjects = gameObjects,
				renderers = renderers
			};
			addedAccessories.Add(activeAccessory.Value);
			_activeAccessories[accessoryTemplate.accessorySlot].Add(activeAccessory.Value);
		}

		if (rebuildMeshImmediately) {
			TryCombineMeshes();
		}

		return addedAccessories.ToArray();
	}

	public void AddSkinAccessory(AccessorySkin skin, bool rebuildMeshImmediately) {
		if (skin.skinTextureDiffuse == null) {
			Debug.LogError("Trying to set entity skin to empty texture");
		}

		var meshes = this.firstPerson ? this.baseMeshesFirstPerson : this.baseMeshesThirdPerson;
		foreach (var mesh in meshes) {
			mesh.material.mainTexture = skin.skinTextureDiffuse;
			if (skin.skinTextureORM) {
				mesh.material.SetTexture(OrmTex, skin.skinTextureORM);
			}
		}

		if (rebuildMeshImmediately) {
			TryCombineMeshes();
		}
	}

	public void SetSkinColor(Color color, bool rebuildMeshImmediately) {
		var meshes = this.firstPerson ? this.baseMeshesFirstPerson : this.baseMeshesThirdPerson;
		foreach (var mesh in meshes) {
			var mat = mesh.GetComponent<MaterialColor>();
			if (!mat) {
				continue;
			}
			mat.SetMaterialColor(0, color);
			mat.DoUpdate();
		}
		
		if (rebuildMeshImmediately) {
			TryCombineMeshes();
		}
	}

	public void SetAccessoryColor(AccessorySlot slot, Color color, bool rebuildMeshImmediately) {
		var accs = GetActiveAccessoriesBySlot(slot);
		foreach (var acc in accs) {
			foreach (var ren in acc.renderers) {
				var mat = ren.GetComponent<MaterialColor>();
				if (!mat) {
					continue;
				}
				mat.SetMaterialColor(0, color);
				mat.DoUpdate();
			}
		}
		
		if (rebuildMeshImmediately) {
			TryCombineMeshes();
		}
	}

	public void TryCombineMeshes() {
		if (meshCombiner.enabled) {
			//COMBINE MESHES
			meshCombiner.sourceReferences.Clear();

			if (this.firstPerson) {
				//Only local owners need to render first person meshes
				foreach (var ren in baseMeshesFirstPerson) {
					meshCombiner.sourceReferences.Add(new (ren.transform));
					ren.gameObject.SetActive(false);
				}
			} else {
				//BODY
				foreach (var ren in baseMeshesThirdPerson) {
					meshCombiner.sourceReferences.Add(new (ren.transform));
					ren.gameObject.SetActive(false);
				}
			}

			//ACCESSORIES
			bool meshCombinedAcc = false;
			foreach (var kvp in _activeAccessories) {
				foreach (var liveAcc in kvp.Value) {
					var acc = liveAcc.AccessoryComponent;
					if (ShouldCombine(acc)) {
						foreach (var ren in liveAcc.renderers) {
							//Map static objects to bones
							if (!acc.HasSkinnedMeshes) {
								var boneMap = ren.GetComponent<MeshCombinerBone>();
								if (boneMap == null) {
									boneMap = ren.AddComponent<MeshCombinerBone>();
								}

								boneMap.boneName = liveAcc.gameObjects[0].transform.parent.name;
								boneMap.scale = acc.transform.localScale;
								boneMap.rotationOffset = acc.transform.localEulerAngles;
								boneMap.positionOffset = acc.transform.localPosition;
							}
							
							meshCombinedAcc = false;
							if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.THIRD_PERSON || acc.visibilityMode == AccessoryComponent.VisibilityMode.BOTH) && !firstPerson) {
								//VISIBLE IN THIRD PERSON
								meshCombiner.sourceReferences.Add(new (ren.transform));
								meshCombinedAcc = true;
							}
							if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.FIRST_PERSON || acc.visibilityMode == AccessoryComponent.VisibilityMode.BOTH) && this.firstPerson) {
								//VISIBLE IN FIRST PERSON
								meshCombiner.sourceReferences.Add(new (ren.transform));
								meshCombinedAcc = true;
							}
							
							ren.gameObject.SetActive(!meshCombinedAcc);
						}
					}
				}
			}

			meshCombiner.LoadMeshCopies();
			meshCombiner.CombineMeshes();
		} else {
			//MAP ITEMS TO RIG
			foreach (var kvp in _activeAccessories) {
				foreach (var liveAcc in kvp.Value) {
					foreach (var ren in liveAcc.renderers) {
						if (ren == null) {
							Debug.LogError("null renderer in renderers array");
							continue;
						}
						var skinnedRen = ren as SkinnedMeshRenderer;
						if (skinnedRen) {
							skinnedRen.rootBone = baseMeshesThirdPerson[0].rootBone;
							skinnedRen.bones = baseMeshesThirdPerson[0].bones;
						}
					}
				}
			}
			OnCombineComplete();
		}
	}

	private bool ShouldCombine(AccessoryComponent acc) {
		//Dont combine held hand items
		return acc.accessorySlot != AccessorySlot.LeftHand && acc.accessorySlot != AccessorySlot.RightHand;

		//Dont combine held hand items with rigs
		//return !((acc.AccessorySlot == AccessorySlot.LeftHand || acc.AccessorySlot == AccessorySlot.RightHand) && acc.HasSkinnedMeshes);
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

	public SkinnedMeshRenderer GetCombinedSkinnedMesh() {
		return meshCombiner.combinedSkinnedMeshRenderer;
	}

	public MeshRenderer GetCombinedStaticMesh() {
		return meshCombiner.combinedStaticMeshRenderer;
	}

	private void OnCombineComplete() {
		this.UpdateAccessoryLayers();
	}

	private bool firstPersonEnabled = false;
	private static readonly int OrmTex = Shader.PropertyToID("_ORMTex");

	public void UpdateAccessoryLayers() {
		// this.firstPersonEnabled = firstPersonEnabled;
		// if (combinerTP.combinedSkinnedMeshRenderer != null) {
		// 	//Set combined mesh
		// 	combinerTP.combinedSkinnedMeshRenderer.enabled = !firstPersonEnabled;
		// 	combinerTP.combinedStaticMeshRenderer.enabled = !firstPersonEnabled;
		// 	if (combinerFP.combinedSkinnedMeshRenderer != null) {
		// 		combinerFP.combinedSkinnedMeshRenderer.enabled = firstPersonEnabled;
		// 		combinerFP.combinedStaticMeshRenderer.enabled = firstPersonEnabled;
		// 	}
		// }

		// Update layers of individual accessories
		foreach (var keyValuePair in _activeAccessories) {
			foreach (var activeAccessory in keyValuePair.Value) {
				foreach (var ren in activeAccessory.renderers) {
					ren.enabled
						= (!firstPerson && activeAccessory.AccessoryComponent.visibilityMode != AccessoryComponent.VisibilityMode.FIRST_PERSON) ||
						  (firstPerson && activeAccessory.AccessoryComponent.visibilityMode != AccessoryComponent.VisibilityMode.THIRD_PERSON);
					// print("AccessoryBuilder " + ren.gameObject.name + " enabled=" + ren.enabled);
					ren.gameObject.layer = firstPerson ? firstPersonLayer : thirdPersonLayer;
					ren.shadowCastingMode = firstPerson ? ShadowCastingMode.Off : ShadowCastingMode.On;

					//Modifying shadow casting requires this component for now
					var meshUpdater = ren.GetComponent<VoxelWorldMeshUpdater>();
					if (!meshUpdater) {
						meshUpdater = ren.AddComponent<VoxelWorldMeshUpdater>();
					}
				}
			}
		}

		//Set body meshes
		// faceMesh.enabled = !this.firstPerson;
		// foreach (var ren in baseMeshesThirdPerson) {
		// 	ren.enabled = !firstPerson;
		// }
		// foreach (var ren in baseMeshesFirstPerson) {
		// 	ren.enabled = firstPerson;
		// }
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

		if (meshCombiner.combinedSkinnedMeshRenderer) {
			renderers.Add(meshCombiner.combinedSkinnedMeshRenderer);
		}
		if (meshCombiner.combinedSkinnedMeshRenderer) {
			renderers.Add(meshCombiner.combinedStaticMeshRenderer);
		}
		
		return renderers.ToArray();
	}

	public Renderer[] GetAccessoryMeshes(AccessorySlot slot) {
		var renderers = new List<Renderer>();
		var activeAccessories = GetActiveAccessoriesBySlot(slot);
		foreach (var aa in activeAccessories) {
			foreach (var ren in aa.renderers) {
				renderers.Add(ren);
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

	public Transform GetSlotTransform(AccessorySlot slot) {
		if (slot == AccessorySlot.Root) {
			return graphicsRoot;
		} 
		
		string itemKey = GetBoneItemKey(slot);
		if (string.IsNullOrEmpty(itemKey)) {
			return graphicsRoot;
		}
		
		Transform foundTransform = entityReferences.GetValueTyped<Transform>(boneKey, itemKey);
		if (!foundTransform) {
			Debug.LogError("Unable to find transform for slot: " + slot + " boneID: " + itemKey);
			return graphicsRoot;
		}

		return foundTransform;
	}
	
	public static string GetBoneItemKey(AccessorySlot slot) {
		switch (slot) {
			case AccessorySlot.RightHand:
				return "HandR";
			case AccessorySlot.LeftHand:
				return "HandL";
			case AccessorySlot.Torso:
			case AccessorySlot.TorsoInner:
			case AccessorySlot.TorsoOuter:
				return "SpineMiddle";
			case AccessorySlot.Backpack:
				return "SpineTop";
			case AccessorySlot.Head:
			case AccessorySlot.Hair:
			case AccessorySlot.Face:
			case AccessorySlot.Ears:
			case AccessorySlot.Nose:
				return "Head";
			case AccessorySlot.Neck:
				return "Neck";
			case AccessorySlot.Waist:
			case AccessorySlot.Legs:
				return "SpineRoot";
			case AccessorySlot.Feet:
			case AccessorySlot.Root:
				return "Root";
			default:
				return "";
		}
	}
}
