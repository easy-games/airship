using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Airship;
using Code.Platform.Server;
using Code.Platform.Shared;
using Code.Player.Accessories;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

[LuauAPI]
[ExecuteInEditMode]
[Icon("Packages/gg.easy.airship/Editor/person-rays-outline-icon.png")]
public class AccessoryBuilder : MonoBehaviour {
    private static readonly int OrmTex = Shader.PropertyToID("_ORMTex");

    public CharacterRig rig;

    [SerializeField] public MeshCombiner meshCombiner;
    public bool firstPerson;

    [HideFromTS]
    public int lodCount = 3;

    [HideInInspector]
    public AccessoryOutfit currentOutfit;

    [HideInInspector]
    public string currentUserId;

    [HideInInspector]
    public string currentUserName;

    private readonly Dictionary<AccessorySlot, ActiveAccessory> activeAccessories = new();

    private static readonly int FaceBaseMapTexture = Shader.PropertyToID("_BaseMap");

    //EVENTS
    /// <summary>
    ///     Called whenever the accessory builder combines the mesh
    /// </summary>
    /// used mesh combiner: bool, combined skin mesh: SkinnedMeshRenderer, combined rigid mesh: MeshRenderer
    public event Action<object, object, object> OnMeshCombined;

    /// <summary>
    ///     Called whenever an accessory was added
    /// </summary>
    /// willCombine: new accessories: ActiveAccessory[]
    public event Action<object> OnAccessoryAdded;

    /// <summary>
    ///     Called whenever an accessory was removed
    /// </summary>
    /// removed accessories: ActiveAccessory[]
    public event Action<object> OnAccessoryRemoved;

    [NonSerialized]
    public AccessorySlot[] firstPersonAllowedSlots = {
        AccessorySlot.LeftHand, AccessorySlot.RightHand, AccessorySlot.Root, AccessorySlot.LeftWrist,
        AccessorySlot.RightWrist,
        AccessorySlot.LeftArmUpper, AccessorySlot.RightArmUpper, AccessorySlot.LeftArmLower,
        AccessorySlot.RightArmLower,
        AccessorySlot.Torso, AccessorySlot.TorsoOuter, AccessorySlot.TorsoInner
    };

    [NonSerialized] private Color skinColor;

    private void Awake() {
        if (!rig) {
            Debug.LogError(
                "Unable to find rig references. Assing the rig in the prefab");
        }
    }

    private void Start() {
        //Have to do it here instead of OnEnable so everything gets initialized
        // if (currentOutfit){
        //     // print("Loading avatar current outfit: " + this.gameObject.name);
        //     var pendingOutfit = currentOutfit;
        //     //Apply outfit skin if provided
        //     RemoveClothingAccessories(false);
        //     EquipAccessoryOutfit(pendingOutfit, true);
        // }
    }

    private void OnEnable() {
        //print("AccessoryBuilder OnEnable: " + this.gameObject.name);
        meshCombiner.OnCombineComplete += OnMeshCombineCompleted;

        if (this.rig.faceMesh.material.mainTexture == null) {
            this.rig.faceMesh.gameObject.SetActive(false);
        }

        // update list of accessories
        // var accessoryComponents = rig.transform.GetComponentsInChildren<AccessoryComponent>();
        // foreach (var accessoryComponent in accessoryComponents) {
        //     // if (!_activeAccessories.ContainsKey(accessoryComponent.accessorySlot)) {
        //     //     _activeAccessories.Add(accessoryComponent.accessorySlot, new ActiveAccessory());
        //     // }
        //
        //     //If we have already tracked this slot, overwrite it
        //     if (activeAccessories.ContainsKey(accessoryComponent.accessorySlot)) {
        //         this.RemoveAccessorySlot(accessoryComponent.accessorySlot, false);
        //     }
        //
        //     var activeAccessory = this.MakeActiveAccessoryFromInstantiatedAccessory(accessoryComponent);
        //     activeAccessories[accessoryComponent.accessorySlot] = activeAccessory;
        // }
        //
        // if(!currentOutfit){
        //     print("AccessoryBuilder combining found meshes: " + this.gameObject.name);
        //     //Mesh combine any found accessories already on the instance
        //     TryCombineMeshes();
        // }
    }

    private void OnDisable() {
        //print("AccessoryBuilder OnDisable: " + this.gameObject.name);
        meshCombiner.OnCombineComplete -= OnMeshCombineCompleted;
    }

    private void OnDestroy() {
        // Manually release mesh combiner (in case Luau is holding a reference to this AccessoryBuilder)
        meshCombiner = null;
    }


    // private ActiveAccessory MakeActiveAccessoryFromInstantiatedAccessory(AccessoryComponent accessoryComponent) {
    //     MeshRenderer[] meshRenderers;
    //     SkinnedMeshRenderer[] skinnedMeshRenderers;
    //     Renderer[] renderers;
    //     if (accessoryComponent.skinnedToCharacter) {
    //         meshRenderers = Array.Empty<MeshRenderer>();
    //         skinnedMeshRenderers = accessoryComponent.GetComponentsInChildren<SkinnedMeshRenderer>();
    //         renderers = skinnedMeshRenderers;
    //     } else {
    //         meshRenderers = accessoryComponent.GetComponentsInChildren<MeshRenderer>();
    //         skinnedMeshRenderers = Array.Empty<SkinnedMeshRenderer>();
    //         renderers = meshRenderers;
    //     }
    //
    //     var meshFilters = accessoryComponent.GetComponentsInChildren<MeshFilter>();
    //     var gameObjects = new GameObject[meshRenderers.Length + skinnedMeshRenderers.Length];
    //     var i = 0;
    //     foreach (var r in meshRenderers) {
    //         gameObjects[i] = r.gameObject;
    //         i++;
    //     }
    //
    //     foreach (var r in skinnedMeshRenderers) {
    //         gameObjects[i] = r.gameObject;
    //         i++;
    //     }
    //
    //     var activeAccessory = new ActiveAccessory {
    //         AccessoryComponent = accessoryComponent,
    //         rootTransform = accessoryComponent.transform,
    //         gameObjects = gameObjects,
    //         meshRenderers = meshRenderers,
    //         skinnedMeshRenderers = skinnedMeshRenderers,
    //         meshFilters = meshFilters,
    //         renderers = renderers
    //     };
    //     return activeAccessory;
    // }

    private void DestroyActiveAccessory(ActiveAccessory activeAccessory) {
        foreach (var lod in activeAccessory.lods) {
            if (lod.rootTransform == null) {
                continue;
            }

            if (Application.isPlaying) {
                Destroy(lod.rootTransform.gameObject);
            } else {
                DestroyImmediate(lod.rootTransform.gameObject);
            }
        }

        if (activeAccessory.rootTransform == null) {
            return;
        }

        if (Application.isPlaying) {
            Destroy(activeAccessory.rootTransform.gameObject);
        } else {
            DestroyImmediate(activeAccessory.rootTransform.gameObject);
        }
    }

    /// <summary>
    ///     Remove all accessories from the character.
    /// </summary>
    public void RemoveAll() {
        foreach (var pair in activeAccessories) {
            DestroyActiveAccessory(pair.Value);
        }

        //Fire event with removed elements
        OnAccessoryRemoved?.Invoke(activeAccessories.Values.ToArray());

        activeAccessories.Clear();
    }

    /// <summary>
    ///     Remove all clothing accessories from the character.
    ///     Not clothing: right and left hands.
    /// </summary>
    public void RemoveClothingAccessories() {
        var toDelete = new List<AccessorySlot>();
        foreach (var pair in activeAccessories) {
            if (pair.Key is AccessorySlot.RightHand or AccessorySlot.LeftHand) {
                continue;
            }

            DestroyActiveAccessory(pair.Value);
            toDelete.Add(pair.Key);
        }

        // Delete the slot from the active accessories
        foreach (var slot in toDelete) {
            activeAccessories.Remove(slot);
        }

        currentOutfit = null;

        // Fire event with removed elements
        OnAccessoryRemoved?.Invoke(toDelete.ToArray());
    }

    /// <summary>
    ///     Remove all accessories from the entity that are in the given slot.
    /// </summary>
    /// <param name="slot">Slot from which to remove accessories.</param>
    public void RemoveBySlot(AccessorySlot slot) {
        if (activeAccessories.TryGetValue(slot, out var activeAccessory)) {
            DestroyActiveAccessory(activeAccessory);
            activeAccessories.Remove(slot);

            //Fire event with removed elements
            OnAccessoryRemoved?.Invoke(new[] {
                activeAccessory
            });
        }
    }

    public ActiveAccessory Add(AccessoryComponent accessoryTemplate) {
        var results = AddRange(new[] { accessoryTemplate });
        if (results.Length == 0) {
            return null;
        }

        return results[0];
    }

    [HideFromTS]
    public ActiveAccessory[] LoadOutfit(AccessoryOutfit outfit) {
        this.currentOutfit = outfit;

        this.SetSkinColor(outfit.skinColor);

        if (outfit.faceDecal && outfit.faceDecal.decalTexture) {
            this.SetFaceTexture(outfit.faceDecal.decalTexture);
        }

        return AddRange(outfit.accessories);
    }


    /// <summary>
    ///     Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
    ///     accessories to be added to the entity, assuming other accessories might already exist
    ///     on the entity.
    /// </summary>
    /// <param name="accessoryPrefabs">Accessories to add.</param>
    /// <param name="addMode">The add behavior.</param>
    public ActiveAccessory[] AddRange(AccessoryComponent[] accessoryPrefabs) {
        var addedAccessories = new List<ActiveAccessory>();

        // Add accessories:
        foreach (var accessoryTemplate in accessoryPrefabs) {
            if (firstPerson) {
                if (accessoryTemplate.visibilityMode == AccessoryComponent.VisibilityMode.ThirdPerson ||
                    !firstPersonAllowedSlots.Contains(accessoryTemplate.accessorySlot))
                    // print("ignoring " + accessoryTemplate.gameObject.name + " on slot " + accessoryTemplate.accessorySlot);
                {
                    continue;
                }
            }

            this.RemoveBySlot(accessoryTemplate.accessorySlot);

            var lods = new List<ActiveAccessory>();
            for (var lodLevel = 0; lodLevel < lodCount; lodLevel++) {
                if (lodLevel - 1 >= accessoryTemplate.meshLods.Count) {
                    break;
                }

                // static mesh lods not supported yet.
                if (!accessoryTemplate.skinnedToCharacter && lodLevel > 0) {
                    continue;
                }

                MeshRenderer[] meshRenderers;
                SkinnedMeshRenderer[] skinnedMeshRenderers;
                Renderer[] renderers;

                GameObject[] gameObjects;
                GameObject newAccessoryObj;
                if (accessoryTemplate.skinnedToCharacter) {
                    // Anything for skinned meshes connected to the main character
                    // Create the prefab at the root of the rig
                    newAccessoryObj = Instantiate(accessoryTemplate.gameObject, rig.transform);
                    skinnedMeshRenderers = newAccessoryObj.GetComponentsInChildren<SkinnedMeshRenderer>();
                    meshRenderers = Array.Empty<MeshRenderer>();
                    renderers = skinnedMeshRenderers;
                    if (lodLevel > 0) {
                        newAccessoryObj.name = $"{accessoryTemplate.gameObject.name} (LOD {lodLevel})";
                        skinnedMeshRenderers[0].sharedMesh = accessoryTemplate.meshLods[lodLevel - 1];
                    }

                    if (skinnedMeshRenderers.Length == 0) {
                        Debug.LogError("Accessory is marked as skinned but has no SkinnedMeshRenderers on it: " +
                                       accessoryTemplate.name);
                    }
                } else {
                    // Anything for static meshes
                    var parent = rig.GetSlotTransform(accessoryTemplate.accessorySlot);
                    // Create the prefab on the joint
                    newAccessoryObj = Instantiate(accessoryTemplate.gameObject, parent);
                    meshRenderers = newAccessoryObj.GetComponentsInChildren<MeshRenderer>();
                    skinnedMeshRenderers = Array.Empty<SkinnedMeshRenderer>();
                    renderers = meshRenderers;
                }

                var meshFilters = newAccessoryObj.GetComponentsInChildren<MeshFilter>();

                // Remove (Clone) from name
                if (lodLevel == 0) {
                    newAccessoryObj.name = accessoryTemplate.gameObject.name;
                }

                // Collect game object references
                var goList = new List<GameObject>();
                for (var i = 0; i < meshRenderers.Length; i++) {
                    goList.Add(meshRenderers[i].gameObject);
                    //If layer is not specified than set it to be the same as the root game object
                    if (meshRenderers[i].gameObject.layer == 0) {
                        meshRenderers[i].gameObject.layer = rig.gameObject.layer;
                    }
                }

                for (var i = 0; i < skinnedMeshRenderers.Length; i++) {
                    goList.Add(skinnedMeshRenderers[i].gameObject);
                    //If layer is not specified than set it to be the same as the root game object
                    if (skinnedMeshRenderers[i].gameObject.layer == 0) {
                        skinnedMeshRenderers[i].gameObject.layer = rig.gameObject.layer;
                    }
                }

                // Any type of renderer
                var activeAccessory = new ActiveAccessory {
                    AccessoryComponent = newAccessoryObj.GetComponent<AccessoryComponent>(),
                    rootTransform = newAccessoryObj.transform,
                    gameObjects = goList.ToArray(),
                    meshRenderers = meshRenderers,
                    skinnedMeshRenderers = skinnedMeshRenderers,
                    meshFilters = meshFilters,
                    renderers = renderers,
                    lodLevel = lodLevel,
                    maxLodLevel = accessoryTemplate.meshLods.Count,
                    lods = Array.Empty<ActiveAccessory>()
                };
                if (lodLevel == 0) {
                    addedAccessories.Add(activeAccessory);
                    activeAccessories[accessoryTemplate.accessorySlot] = activeAccessory;
                } else {
                    // LOD accessories exist in memory but don't get added to the activeAccessories list.
                    // They are only immediately used to generate mesh information.
                    // They are cleaned up because each lod is added
                    // to the lods list in LOD0 active accessory.
                    lods.Add(activeAccessory);
                }
            }

            activeAccessories[accessoryTemplate.accessorySlot].lods = lods.ToArray();
        }

        //Fire event for added accessories
        var arrayAccessories = addedAccessories.ToArray();
        OnAccessoryAdded?.Invoke(arrayAccessories);

        return arrayAccessories;
    }

    public void SetSkinColor(Color color) {
        skinColor = color;
        // foreach (var mesh in this.rig.bodyMeshLOD) {
        //     SetMeshColor(mesh, color);
        // }
        // foreach (var mesh in this.rig.armsMeshLOD) {
        //     SetMeshColor(mesh, color);
        // }
        // foreach (var mesh in this.rig.headMeshLOD) {
        //     SetMeshColor(mesh, color);
        // }
    }

    // private void SetMeshColor(Renderer ren, Color color) {
    //     var mat = ren.gameObject.GetComponent<MaterialColorURP>();
    //     if (mat) {
    //         var colors = mat.colorSettings;
    //         colors[0].baseColor = color;
    //         mat.colorSettings = colors;
    //         mat.DoUpdate();
    //     }
    // }

    // public void SetAccessoryColor(AccessorySlot slot, Color color, bool rebuildMeshImmediately = true) {
    //     var acc = GetActiveAccessoryBySlot(slot);
    //     foreach (var ren in acc.meshRenderers) {
    //         SetMeshColor(ren, color);
    //     }
    //
    //     if (rebuildMeshImmediately) {
    //         UpdateImmediately();
    //     }
    // }

    public void SetFaceTexture(Texture2D texture) {
        var propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetTexture(FaceBaseMapTexture, texture);
        rig.faceMesh.SetPropertyBlock(propertyBlock);
        rig.faceMesh.gameObject.SetActive(true);
    }

    public void UpdateCombinedMesh() {
        // Debug.Log("UpdateCombinedMesh (" + this.gameObject.name + ")");
        Profiler.BeginSample("AB.TryCombineMeshes");
        if (meshCombiner.enabled && Application.isPlaying) {
            meshCombiner.ClearSourceReferences();

            // Add body meshes as source references on mesh combiner
            // foreach (var ren in rig.baseMeshes) {
            // this.meshCombiner.AddSourceReference(
            //     ren.transform,
            //     Array.Empty<MeshRenderer>(),
            //     new[] { ren as SkinnedMeshRenderer },
            //     new[] { ren.transform.GetComponent<MeshFilter>() }
            // );
            // meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
            // ren.gameObject.SetActive(false);
            // }

            // Accessories
            var isCombined = false;

            foreach (var pair in activeAccessories) {
                var activeAccessory = pair.Value;
                var accessoryComponent = pair.Value.AccessoryComponent;

                if (!accessoryComponent.skinnedToCharacter)
                    //Debug.Log("Skipping: " + acc.name);
                {
                    continue;
                }

                // Map static objects to bones
                // if (!accessoryComponent.skinnedToCharacter) {
                // var boneMap = acc.gameObject.GetComponent<MeshCombinerBone>();
                // if (boneMap == null) boneMap = acc.gameObject.AddComponent<MeshCombinerBone>();
                //
                // boneMap.boneName = acc.gameObject.transform.parent.name;
                //
                // boneMap.scale = acc.transform.localScale;
                // boneMap.rotationOffset = acc.transform.localEulerAngles;
                // boneMap.positionOffset = acc.transform.localPosition;
                // }

                meshCombiner.AddSourceReference(activeAccessory);

                {
                    var bodyMesh = firstPerson ? rig.viewmodelArmsMesh : rig.bodyMesh;

                    foreach (var ren in activeAccessory.skinnedMeshRenderers) {
                        ren.rootBone = bodyMesh.rootBone;
                        ren.bones = bodyMesh.bones;
                    }

                    foreach (var lod in activeAccessory.lods) {
                        foreach (var ren in lod.skinnedMeshRenderers) {
                            ren.rootBone = bodyMesh.rootBone;
                            ren.bones = bodyMesh.bones;
                        }
                    }
                }

                foreach (var ren in activeAccessory.renderers) {
                    isCombined = false;
                    if ((accessoryComponent.visibilityMode == AccessoryComponent.VisibilityMode.ThirdPerson ||
                         accessoryComponent.visibilityMode == AccessoryComponent.VisibilityMode.Both) && !firstPerson)
                        // Visible in third person
                        // this.meshCombiner.AddSourceReference(activeAccessory);
                        // meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                    {
                        isCombined = true;
                    }

                    if ((accessoryComponent.visibilityMode == AccessoryComponent.VisibilityMode.FirstPerson ||
                         accessoryComponent.visibilityMode == AccessoryComponent.VisibilityMode.Both) && firstPerson)
                        // Visible in first person
                        // this.meshCombiner.AddSourceReference(activeAccessory);
                        // meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                    {
                        isCombined = true;
                    }

                    ren.gameObject.SetActive(!isCombined);
                }

                foreach (var lod in activeAccessory.lods) {
                    foreach (var ren in lod.skinnedMeshRenderers) {
                        ren.gameObject.SetActive(false);
                    }
                }
            }

            // print("AccessoryBuilder MeshCombine: " + this.gameObject.name);
            meshCombiner.CombineMeshes(this.skinColor);
        } else {
            // print("AccessoryBuilder Manual Rig Mapping: " + this.gameObject.name);
            MapAccessoriesToRig();
            OnCombineComplete(false);
        }

        Profiler.EndSample();
    }

    private void MapAccessoriesToRig() {
        foreach (var pair in activeAccessories) {
            foreach (var ren in pair.Value.skinnedMeshRenderers) {
                if (ren) {
                    ren.rootBone = rig.armsMesh.rootBone;
                    ren.bones = rig.armsMesh.bones;
                }
            }

            if (pair.Value.lods == null) {
                continue;
            }

            foreach (var lod in pair.Value.lods) {
                foreach (var ren in lod.skinnedMeshRenderers) {
                    ren.rootBone = rig.bodyMesh.rootBone;
                    ren.bones = rig.bodyMesh.bones;
                }
            }
        }
    }

    private bool ShouldCombine(AccessoryComponent acc) {
        //Dont combine held hand items
        return acc.canMeshCombine && acc.accessorySlot != AccessorySlot.LeftHand &&
               acc.accessorySlot != AccessorySlot.RightHand;
    }

    public ActiveAccessory GetActiveAccessoryBySlot(AccessorySlot target) {
        if (activeAccessories.TryGetValue(target, out var items)) {
            return items;
        }

        return null;
    }

    public ActiveAccessory[] GetActiveAccessories() {
        var results = new ActiveAccessory[activeAccessories.Count];
        activeAccessories.Values.CopyTo(results, 0);
        return results;
    }

    public SkinnedMeshRenderer GetCombinedSkinnedMesh() {
        return meshCombiner.outputSkinnedMeshRenderers[0];
    }

    //Event from MeshCombine component
    private void OnMeshCombineCompleted() {
        OnCombineComplete(true);
    }

    private void OnCombineComplete(bool usedMeshCombiner) {
        //Mesh Combine Complete
        OnMeshCombined?.Invoke(usedMeshCombiner, meshCombiner.outputSkinnedMeshRenderers[0],
            meshCombiner.outputSkinnedMeshRenderers[0]);
    }

    public Renderer[] GetAllAccessoryRenderers() {
        var renderers = new List<Renderer>();

        //Main renderers
        foreach (var keyValuePair in activeAccessories) {
            //Main Renderers
            foreach (var ren in keyValuePair.Value.renderers) {
                renderers.Add(ren);
            }

            //LOD Renderers
            foreach (var lodAcc in keyValuePair.Value.lods) {
                foreach (var ren in keyValuePair.Value.renderers) {
                    renderers.Add(ren);
                }
            }
        }


        //Combined renderers
        if (meshCombiner.enabled) {
            for (var i = 0; i < meshCombiner.outputSkinnedMeshRenderers.Count; i++) {
                renderers.Add(meshCombiner.outputSkinnedMeshRenderers[i]);
            }
        } else { }

        return renderers.ToArray();
    }

    public Renderer[] GetAccessoryRenderers(AccessorySlot slot) {
        var activeAccessory = GetActiveAccessoryBySlot(slot);
        if (activeAccessory == null) {
            return Array.Empty<Renderer>();
        }

        var renderers = new List<Renderer>();
        if (activeAccessory.meshRenderers != null) {
            foreach (var ren in activeAccessory.meshRenderers) {
                renderers.Add(ren);
            }
        }

        return renderers.ToArray();
    }

    public ParticleSystem[] GetAccessoryParticles(AccessorySlot slot) {
        var results = new List<ParticleSystem>();
        var activeAccessory = GetActiveAccessoryBySlot(slot);
        if (activeAccessory.gameObjects != null) {
            foreach (var go in activeAccessory.gameObjects) {
                var particles = go.GetComponentsInChildren<ParticleSystem>();
                foreach (var particle in particles) {
                    results.Add(particle);
                }
            }
        }

        return results.ToArray();
    }

    public void SetCreateOverlayMeshOnCombine(bool on) {
        if (meshCombiner) {
            meshCombiner.createOverlayMesh = on;
        }
    }

#if UNITY_EDITOR
    [HideInInspector]
    public bool cancelPendingDownload;

    // [HideFromTS]
    // public async Task<ActiveAccessory[]> EquipOutfitFromUsername(string username) {
    //     var res = await UsersServiceBackend.GetUserByUsername(username);
    //
    //     if (!res.success) {
    //         Debug.LogError("failed to load username: " + username + " error: " + (res.error ?? "Empty Error"));
    //         return new ActiveAccessory[0];
    //     }
    //
    //     var data = JsonUtility.FromJson<UserResponse>(res.data).user;
    //
    //     if (data == null) {
    //         Debug.LogError("failed to load username: " + username + " error: " + (res.error ?? "Empty Data"));
    //         return new ActiveAccessory[0];
    //     }
    //
    //     currentUserName = username;
    //     currentUserId = data.uid;
    //     return await AddOutfitFromUserId(currentUserId);
    // }

    // [HideFromTS]
    // public async Task<ActiveAccessory[]> AddOutfitFromUserId(string userId) {
    //     currentUserId = userId;
    //     cancelPendingDownload = false;
    //     AccessoryOutfit outfit; //Load outfit from server
    //     var res = await AirshipInventoryServiceBackend.GetEquippedOutfitByUserId(userId);
    //     if (cancelPendingDownload) {
    //         cancelPendingDownload = false;
    //         return new ActiveAccessory[0];
    //     }
    //
    //     if (!res.success) {
    //         Debug.LogError("failed to load player equipped outfit, http call failed");
    //         return new ActiveAccessory[0];
    //     }
    //
    //     var outfitDto = JsonUtility.FromJson<OutfitResponse>(res.data).outfit;
    //
    //     if (outfitDto == null) {
    //         Debug.LogError("failed to load player equipped outfit, no data");
    //         return new ActiveAccessory[0];
    //     }
    //
    //     RemoveClothingAccessories();
    //     //Skin color
    //     if (ColorUtility.TryParseHtmlString(outfitDto.skinColor, out var skinColor)) {
    //         SetSkinColor(skinColor);
    //     }
    //
    //     //Accessories
    //     var collection = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>(
    //         "Assets/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems/EntireAvatarCollection.asset");
    //     //print("Found collection: " + collection.accessories.Length);
    //     foreach (var acc in outfitDto.gear) {
    //         var foundItem = false;
    //         foreach (var face in collection.faces) {
    //             if (face && face.serverClassId == acc.@class.classId) {
    //                 foundItem = true;
    //                 SetFaceTexture(face.decalTexture);
    //                 break;
    //             }
    //         }
    //
    //         if (!foundItem) {
    //             foreach (var accComponent in collection.accessories) {
    //                 if (accComponent && accComponent.serverClassId == acc.@class.classId) {
    //                     foundItem = true;
    //                     this.Add(accComponent);
    //                     break;
    //                 }
    //             }
    //         }
    //
    //         if (!foundItem) {
    //             Debug.LogError("Unable to load acc: " + acc.@class.classId);
    //         }
    //     }
    //
    //     this.UpdateCombinedMesh();
    //
    //     return new ActiveAccessory[0];
    // }
#endif
}