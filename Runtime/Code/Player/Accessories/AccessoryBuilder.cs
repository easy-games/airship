using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Airship;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

[RequireComponent(typeof(EntityDriver))]
public class AccessoryBuilder : MonoBehaviour {
	public const string boneKey = "Bones";
	[SerializeField] private MeshCombiner combinerTP;
	[SerializeField] private MeshCombiner combinerFP;

	private Dictionary<AccessorySlot, List<ActiveAccessory>> _activeAccessories;
	private EntityDriver driver;
	private Transform graphicsRoot;
	private GameObjectReferences entityReferences;
	private SkinnedMeshRenderer[] baseMeshesThirdPerson;
	private SkinnedMeshRenderer[] baseMeshesFirstPerson;
	private SkinnedMeshRenderer[] allBaseMeshes;
	private SkinnedMeshRenderer faceMesh;

	private int firstPersonLayer;
	private int thirdPersonLayer;
	
	private void Awake() {
		combinerTP.OnCombineComplete = OnCombineComplete;
		combinerFP.OnCombineComplete = OnCombineComplete;
		firstPersonLayer = LayerMask.NameToLayer("FirstPerson");
		thirdPersonLayer = LayerMask.NameToLayer("Character");
		
		driver = gameObject.GetComponent<EntityDriver>();
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
		allBaseMeshes = new SkinnedMeshRenderer[baseMeshesFirstPerson.Length + baseMeshesThirdPerson.Length];
		int allMeshesI = 0;
		for (int i = 0; i < baseMeshesThirdPerson.Length; i++) {
			allBaseMeshes[allMeshesI] = baseMeshesThirdPerson[i];
			allMeshesI++;
		}
		for (int i = 0; i < baseMeshesFirstPerson.Length; i++) {
			allBaseMeshes[allMeshesI] = baseMeshesFirstPerson[i];
			allMeshesI++;
		}
		
		if (!baseMeshesThirdPerson[0] || !baseMeshesThirdPerson[1] || !baseMeshesFirstPerson[0]) {
			Debug.LogError("Unable to find base Meshes on HumanEntity. Did you forget to assign the correct component type on the reference builder?");
		}
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
	public void RemoveAccessorySlot(AccessorySlot slot, bool rebuildImmediately) {

		DestroyAccessorySlot(slot);

		if (rebuildImmediately) {
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

	public ActiveAccessory SetAccessory(Accessory accessory, bool combineMeshes)
	{
		return AddAccessories(new List<Accessory>() {accessory}, AccessoryAddMode.Replace, combineMeshes)[0];
	}

	public ActiveAccessory[] EquipAccessoryCollection(AccessoryCollection collection, bool combineMeshes = true) {
		if (collection.customSkin) {
			AddSkinAccessory(collection.customSkin, false);
		}
		return AddAccessories(collection.accessories, AccessoryAddMode.Replace, combineMeshes);
	}
	
	/// <summary>
	/// Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
	/// accessories to be added to the entity, assuming other accessories might already exist
	/// on the entity.
	/// </summary>
	/// <param name="accessories">Accessories to add.</param>
	/// <param name="addMode">The add behavior.</param>
	public ActiveAccessory[] AddAccessories(List<Accessory> accessories, AccessoryAddMode addMode, bool combineMeshes)
	{
		List<ActiveAccessory> addedAccessories = new List<ActiveAccessory>();

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
					Destroy(activeAccessory.rootTransform.gameObject);
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

			//Create the accessory game object
			ActiveAccessory? activeAccessory = null;
			Renderer[] renderers;
			GameObject[] gameObjects;
			GameObject newAccessoryObj;
			if (accessory.SkinnedToCharacter && accessory.HasSkinnedMeshes) {
				//Anything for skinned meshes connected to the main character
				//Create the prefab at the root
				newAccessoryObj = Instantiate(accessory.Prefab, graphicsRoot);
				renderers = newAccessoryObj.GetComponentsInChildren<SkinnedMeshRenderer>();
			} else {
				//Anything for static meshes
				Transform parent = GetSlotTransform(accessory.AccessorySlot);
				//Create the prefab on the joint
				newAccessoryObj = Instantiate(accessory.Prefab, parent);
				newAccessoryObj.transform.localScale = accessory.Scale;
				newAccessoryObj.transform.localEulerAngles = accessory.Rotation;
				newAccessoryObj.transform.localPosition = accessory.Position;
				renderers = newAccessoryObj.GetComponentsInChildren<Renderer>();
			}
			
			//Remove (Clone) from name
			newAccessoryObj.name = accessory.Prefab.name;

			//Collect game object references
			gameObjects = new GameObject[renderers.Length];
			for (var i = 0; i < renderers.Length; i++) {
				gameObjects[i] = renderers[i].gameObject;
			}
			
			//Any type of renderer
			activeAccessory = new() {
				accessory = accessory,
				rootTransform = newAccessoryObj.transform,
				gameObjects = gameObjects,
				renderers = renderers
			};
			addedAccessories.Add(activeAccessory.Value);
			_activeAccessories[accessory.AccessorySlot].Add(activeAccessory.Value);
		}

		if (combineMeshes) {
			TryCombineMeshes();
		} else {
			OnCombineComplete();
		}

		return addedAccessories.ToArray();
	}

	public void AddSkinAccessory(AccessorySkin skin, bool combineMeshes) {
		if (skin.skinTextureDiffuse == null) {
			Debug.LogError("Trying to set entity skin to empty texture");
		}

		foreach (var mesh in allBaseMeshes) {
			mesh.material.mainTexture = skin.skinTextureDiffuse;
			if (skin.skinTextureORM) {
				mesh.material.SetTexture(OrmTex, skin.skinTextureORM);
			}
		}

		if (combineMeshes) {
			TryCombineMeshes();
		}
	}

	public void SetSkinColor(Color color, bool combineMeshes) {
		foreach (var mesh in allBaseMeshes) {
			var mat = mesh.GetComponent<MaterialColor>();
			if (!mat) {
				continue;
			}
			mat.SetMaterialColor(0, color);
			mat.DoUpdate();
		}
		
		if (combineMeshes) {
			TryCombineMeshes();
		}
	}

	public void SetAccessoryColor(AccessorySlot slot, Color color, bool combineMeshes) {
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
		
		if (combineMeshes) {
			TryCombineMeshes();
		}
	}

	public void TryCombineMeshes() {
		if (combinerTP.enabled) {
			//COMBINE MESHES
			combinerTP.sourceReferences.Clear();
			combinerFP.sourceReferences.Clear();

			//BODY
			foreach (var ren in baseMeshesThirdPerson) {
				combinerTP.sourceReferences.Add(new (ren.transform));
				ren.gameObject.SetActive(false);
			}

			//Only local owners need to render first person meshes
			if (driver.IsOwner) {
				foreach (var ren in baseMeshesFirstPerson) {
					combinerFP.sourceReferences.Add(new (ren.transform));
					ren.gameObject.SetActive(false);
				}
			}
			
			//ACCESSORIES
			bool meshCombinedAcc = false;
			foreach (var kvp in _activeAccessories) {
				foreach (var liveAcc in kvp.Value) {
					var acc = liveAcc.accessory;
					if (ShouldCombine(acc)) {
						foreach (var ren in liveAcc.renderers) {
							//Map static objects to bones
							if (!acc.HasSkinnedMeshes) {
								var boneMap = ren.GetComponent<MeshCombinerBone>();
								if (boneMap == null) {
									boneMap = ren.AddComponent<MeshCombinerBone>();
								}

								boneMap.boneName = liveAcc.gameObjects[0].transform.parent.name;
								boneMap.scale = acc.Scale;
								boneMap.rotationOffset = acc.Rotation;
								boneMap.positionOffset = acc.Position;
							}
							
							meshCombinedAcc = false;
							if (acc.visibilityMode != Accessory.VisibilityMode.FIRST_PERSON) {
								//VISIBLE IN THIRD PERSON
								combinerTP.sourceReferences.Add(new (ren.transform));
								meshCombinedAcc = true;
							}
							if (acc.visibilityMode != Accessory.VisibilityMode.THIRD_PERSON) {
								//VISIBLE IN FIRST PERSON
								combinerFP.sourceReferences.Add(new (ren.transform));
								meshCombinedAcc = true;
							}
							
							ren.gameObject.SetActive(!meshCombinedAcc);
						}
					}
				}
			}
			
			combinerTP.LoadMeshCopies();
			combinerTP.CombineMeshes();
			if (driver.IsOwner) {
				combinerFP.LoadMeshCopies();
				combinerFP.CombineMeshes();
			}
		} else {
			//MAP ITEMS TO RIG
			foreach (var kvp in _activeAccessories) {
				foreach (var liveAcc in kvp.Value) {
					foreach (var ren in liveAcc.renderers) {
						var skinnedRen = (SkinnedMeshRenderer)ren;
						if (skinnedRen) {
							skinnedRen.rootBone = baseMeshesThirdPerson[0].rootBone;
							skinnedRen.bones = baseMeshesThirdPerson[0].bones;
						}
					}
				}
			}
		}
	}

	private bool ShouldCombine(Accessory acc) {
		//Dont combine held hand items
		return acc.AccessorySlot != AccessorySlot.LeftHand && acc.AccessorySlot != AccessorySlot.RightHand;

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

	public SkinnedMeshRenderer GetCombinedSkinnedMesh(bool firstPerson) {
		return firstPerson ? combinerFP.combinedSkinnedMeshRenderer : combinerTP.combinedSkinnedMeshRenderer;
	}

	public MeshRenderer GetCombinedStaticMesh(bool firstPerson) {
		return firstPerson ? combinerFP.combinedStaticMeshRenderer : combinerTP.combinedStaticMeshRenderer;
	}

	private void OnCombineComplete() {
		SetFirstPersonEnabled(firstPersonEnabled);
	}

	private bool firstPersonEnabled = false;
	private static readonly int OrmTex = Shader.PropertyToID("_ORMTex");

	public void SetFirstPersonEnabled(bool firstPersonEnabled) {
		this.firstPersonEnabled = firstPersonEnabled;
		if (combinerTP.combinedSkinnedMeshRenderer != null) {
			//Set combined mesh
			combinerTP.combinedSkinnedMeshRenderer.enabled = !firstPersonEnabled;
			combinerTP.combinedStaticMeshRenderer.enabled = !firstPersonEnabled;
			if (combinerFP.combinedSkinnedMeshRenderer != null) {
				combinerFP.combinedSkinnedMeshRenderer.enabled = firstPersonEnabled;
				combinerFP.combinedStaticMeshRenderer.enabled = firstPersonEnabled;
			}
		}
		
		//Set individual accessories
		foreach (var keyValuePair in _activeAccessories) {
			foreach (var activeAccessory in keyValuePair.Value) {
				foreach (var ren in activeAccessory.renderers) {
					ren.enabled
						= (!firstPersonEnabled && activeAccessory.accessory.visibilityMode != Accessory.VisibilityMode.FIRST_PERSON) ||
						  (firstPersonEnabled && activeAccessory.accessory.visibilityMode != Accessory.VisibilityMode.THIRD_PERSON);
					ren.gameObject.layer = firstPersonEnabled ? firstPersonLayer : thirdPersonLayer;
					ren.shadowCastingMode = firstPersonEnabled ? ShadowCastingMode.Off : ShadowCastingMode.On;
					
					//Modifying shadow casting requires this component for now
					var meshUpdater = ren.GetComponent<VoxelWorldMeshUpdater>();
					if (!meshUpdater) {
						meshUpdater = ren.AddComponent<VoxelWorldMeshUpdater>();
					}
				}
			}
		}
		
		//Set body meshes
		faceMesh.enabled = !this.firstPersonEnabled;
		foreach (var ren in baseMeshesThirdPerson) {
			ren.enabled = !firstPersonEnabled;
		}
		foreach (var ren in baseMeshesFirstPerson) {
			ren.enabled = firstPersonEnabled;
		}
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

		if (combinerFP.combinedSkinnedMeshRenderer) {
			renderers.Add(combinerFP.combinedSkinnedMeshRenderer);
		}
		if (combinerTP.combinedSkinnedMeshRenderer) {
			renderers.Add(combinerTP.combinedSkinnedMeshRenderer);
		}
		if (combinerFP.combinedSkinnedMeshRenderer) {
			renderers.Add(combinerFP.combinedStaticMeshRenderer);
		}
		if (combinerTP.combinedSkinnedMeshRenderer) {
			renderers.Add(combinerTP.combinedStaticMeshRenderer);
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
				return "Torso";
			case AccessorySlot.Head:
			case AccessorySlot.Hair:
			case AccessorySlot.Face:
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
