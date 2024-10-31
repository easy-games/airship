using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Airship;
using Code.Platform.Server;
using Code.Platform.Shared;
using Code.Player.Accessories;
using UnityEditor;
using UnityEngine;

[LuauAPI]
[ExecuteInEditMode]
[Icon("Packages/gg.easy.airship/Editor/person-rays-outline-icon.png")]
public class AccessoryBuilder : MonoBehaviour
{
    private static readonly int OrmTex = Shader.PropertyToID("_ORMTex");

    public CharacterRig rig;

    [SerializeField] private MeshCombiner meshCombiner;
    public bool firstPerson;

    private Dictionary<AccessorySlot, ActiveAccessory> _activeAccessories = new Dictionary<AccessorySlot, ActiveAccessory>();

    //EVENTS
    /// <summary>
    /// Called whenever the accessory builder combines the mesh
    /// </summary>
    /// used mesh combiner: bool, combined skin mesh: SkinnedMeshRenderer, combined rigid mesh: MeshRenderer
    public event Action<object, object, object> OnMeshCombined;

    private void Awake() {
        if (!rig)
            Debug.LogError(
                "Unable to find rig references. Assing the rig in the prefab");

    }

    private void Start() {
        if(!enabled){
            return;
        }
        
        //Have to do it here instead of OnEnable so everything gets initialized
        if(currentOutfit){
            // print("Loading avatar current outfit: " + this.gameObject.name);
            var pendingOutfit = currentOutfit;
            //Apply outfit skin if provided
            RemoveClothingAccessories(false);
            EquipAccessoryOutfit(pendingOutfit, true);
        }
    }

    private void OnEnable() {
        //print("AccessoryBuilder OnEnable: " + this.gameObject.name);
        meshCombiner.OnCombineComplete += OnMeshCombineCompleted;

        // update list of accessories
        var accessoryComponents = rig.transform.GetComponentsInChildren<AccessoryComponent>();
        foreach (var accessoryComponent in accessoryComponents) {
            // if (!_activeAccessories.ContainsKey(accessoryComponent.accessorySlot)) {
            //     _activeAccessories.Add(accessoryComponent.accessorySlot, new ActiveAccessory());
            // }
            
            //If we have already tracked this slot, overwrite it
            if (_activeAccessories.ContainsKey(accessoryComponent.accessorySlot)) {
                this.RemoveAccessorySlot(accessoryComponent.accessorySlot, false);
            }

            Renderer[] renderers;
            if (accessoryComponent.skinnedToCharacter) {
                renderers = accessoryComponent.GetComponentsInChildren<SkinnedMeshRenderer>();
            } else {
                renderers = accessoryComponent.GetComponentsInChildren<Renderer>();
            }
            GameObject[] gameObjects = new GameObject[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) {
                gameObjects[i] = renderers[i].gameObject;
            }

            var activeAccessory = new ActiveAccessory {
                AccessoryComponent = accessoryComponent,
                rootTransform = accessoryComponent.transform,
                gameObjects = gameObjects,
                renderers = renderers
            };
            _activeAccessories[accessoryComponent.accessorySlot] = activeAccessory;
        }

        if(!currentOutfit){
            print("AccessoryBuilder combining found meshes: " + this.gameObject.name);
            //Mesh combine any found accessories already on the instance
            TryCombineMeshes();
        }

    }

    private void OnDisable() {
        //print("AccessoryBuilder OnDisable: " + this.gameObject.name);
        meshCombiner.OnCombineComplete -= OnMeshCombineCompleted;
    }

    /// <summary>
    ///     Remove all accessories from the character.
    /// </summary>
    public void RemoveAllAccessories(bool rebuildMeshImmediately = true) {
        foreach (var pair in _activeAccessories) {
            if (Application.isPlaying) {
                Destroy(pair.Value.rootTransform.gameObject);
            } else {
                DestroyImmediate(pair.Value.rootTransform.gameObject);
            }
        }
        _activeAccessories.Clear();

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    /// <summary>
    ///     Remove all clothing accessories from the character.
    ///     Not clothing: right and left hands.
    /// </summary>
    public void RemoveClothingAccessories(bool rebuildMeshImmediately = true) {
        var toDelete = new List<AccessorySlot>();
        foreach (var pair in _activeAccessories) {
            if (pair.Key is AccessorySlot.RightHand or AccessorySlot.LeftHand) continue;
            if (Application.isPlaying) {
                Destroy(pair.Value.rootTransform.gameObject);
            } else {
                DestroyImmediate(pair.Value.rootTransform.gameObject);
            }
            toDelete.Add(pair.Key);
        }

        //Delte the slot from the active accessories
        foreach(var slot in toDelete){
            _activeAccessories.Remove(slot);
        }

        currentOutfit = null;

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    /// <summary>
    ///     Remove all accessories from the entity that are in the given slot.
    /// </summary>
    /// <param name="slot">Slot from which to remove accessories.</param>
    public void RemoveAccessorySlot(AccessorySlot slot, bool rebuildMeshImmediately = true) {
        if (_activeAccessories.TryGetValue(slot, out var accessoryObjs)) {
            if (Application.isPlaying) {
                Destroy(accessoryObjs.rootTransform.gameObject);
            } else {
                DestroyImmediate(accessoryObjs.rootTransform.gameObject);
            }
            _activeAccessories.Remove(slot);
        }

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    public ActiveAccessory AddSingleAccessory(AccessoryComponent accessoryTemplate, bool rebuildMeshImmediately) {
        return AddAccessories(new[] { accessoryTemplate }, AccessoryAddMode.Replace, rebuildMeshImmediately)[0];
    }


    [HideInInspector]
    public AccessoryOutfit currentOutfit;
    public ActiveAccessory[] EquipAccessoryOutfit(AccessoryOutfit outfit, bool rebuildMeshImmediately = true) {
        this.currentOutfit = outfit;
        if (outfit.forceSkinColor) SetSkinColor(outfit.skinColor, false);
        if(outfit.faceDecal?.decalTexture) SetFaceTexture(outfit.faceDecal.decalTexture);
        return AddAccessories(outfit.accessories, AccessoryAddMode.Replace, rebuildMeshImmediately);
    }

    [HideInInspector]
    public string currentUserId;
    [HideInInspector]
    public string currentUserName;
#if UNITY_EDITOR
    [HideInInspector]
    public bool cancelPendingDownload = false;

    [HideFromTS]
    public async Task<ActiveAccessory[]> EquipOutfitFromUsername(string username){
        var res = await UsersServiceBackend.GetUserByUsername(username);

        if (!res.success) {
			Debug.LogError("failed to load username: " + username+ " error: " + (res.error ?? "Empty Error"));
            return new ActiveAccessory[0];
        }

        var data = JsonUtility.FromJson<UserResponse>(res.data).user;

        if (!data) {
			Debug.LogError("failed to load username: " + username+ " error: " + (res.error ?? "Empty Data"));
            return new ActiveAccessory[0];
        }

        this.currentUserName = username;
        this.currentUserId = data.uid;
        return await AddOutfitFromUserId(this.currentUserId);
    }

    [HideFromTS]
    public async Task<ActiveAccessory[]> AddOutfitFromUserId(string userId) {
        this.currentUserId = userId;
        this.cancelPendingDownload = false;
        AccessoryOutfit outfit;//Load outfit from server
		var res = await AirshipInventoryServiceBackend.GetEquippedOutfitByUserId(userId);
        if(cancelPendingDownload){
            cancelPendingDownload = false;
            return new ActiveAccessory[0];
        }

        if (!res.success) {
			Debug.LogError("failed to load player equipped outfit, http call failed");
            return new ActiveAccessory[0];
        }

        var outfitDto = JsonUtility.FromJson<OutfitResponse>(res.data).outfit;

        if (!outfitDto) {
			Debug.LogError("failed to load player equipped outfit, no data");
            return new ActiveAccessory[0];
        }

        RemoveClothingAccessories();
        //Skin color
        if(ColorUtility.TryParseHtmlString(outfitDto.skinColor, out Color skinColor)){
            SetSkinColor(skinColor, true);
        }
        //Accessories
        var collection = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>("Assets/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems/EntireAvatarCollection.asset");
        //print("Found collection: " + collection.accessories.Length);
        foreach(var acc in outfitDto.accessories){
            bool foundItem = false;
            foreach(var face in collection.faces){
                if(face && face.serverClassId == acc.@class.classId){
                    foundItem = true;
                    SetFaceTexture(face.decalTexture);
                    break;
                }
            }
            if(!foundItem){
                foreach(var accComponent in collection.accessories){
                    if(accComponent && accComponent.serverClassId == acc.@class.classId){
                        foundItem = true;
                        AddSingleAccessory(accComponent, false);
                        break;
                    }
                }
            }
            if(!foundItem){
                Debug.LogError("Unable to load acc: " + acc.@class.classId);
            }
        }
        TryCombineMeshes();

        return new ActiveAccessory[0];
    }
#endif


    /// <summary>
    ///     Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
    ///     accessories to be added to the entity, assuming other accessories might already exist
    ///     on the entity.
    /// </summary>
    /// <param name="accessoryTemplates">Accessories to add.</param>
    /// <param name="addMode">The add behavior.</param>
    public ActiveAccessory[] AddAccessories(AccessoryComponent[] accessoryTemplates, AccessoryAddMode addMode,
        bool rebuildMeshImmediately) {
        var addedAccessories = new List<ActiveAccessory>();

        // In 'ReplaceAll' mode, remove all existing accessories:
        if (addMode == AccessoryAddMode.ReplaceAll) {
            RemoveAllAccessories();
        }

        // Add accessories:
        foreach (var accessoryTemplate in accessoryTemplates) {
            // In 'AddIfNone' mode, don't add the accessory if one already exists in the slot:
            if (addMode == AccessoryAddMode.AddIfNone){
                if(_activeAccessories.ContainsKey(accessoryTemplate.accessorySlot)){
                    continue;
                }
            } // In 'Replace' mode, remove all accessories that are in the slots of the new accessories:
            else if(addMode == AccessoryAddMode.Replace){
                RemoveAccessorySlot(accessoryTemplate.accessorySlot, false);
            }

            Renderer[] renderers;
            GameObject[] gameObjects;
            GameObject newAccessoryObj;
            if (accessoryTemplate.skinnedToCharacter) {
                //Anything for skinned meshes connected to the main character
                //Create the prefab at the root of the rig
                newAccessoryObj = Instantiate(accessoryTemplate.gameObject, rig.transform);
                renderers = newAccessoryObj.GetComponentsInChildren<SkinnedMeshRenderer>();
                if(renderers.Length == 0){
                    Debug.LogError("Accessory marked as skinned but no skinned renderers are on it: " + accessoryTemplate.name);
                }
            } else {
                //Anything for static meshes
                var parent = rig.GetSlotTransform(accessoryTemplate.accessorySlot);
                //Create the prefab on the joint
                newAccessoryObj = Instantiate(accessoryTemplate.gameObject, parent);
                renderers = newAccessoryObj.GetComponentsInChildren<Renderer>();
                // if(renderers.Length == 0){
                //     Debug.LogWarning("Accessory with no renderers are on it: " + accessoryTemplate.name);
                // }
            }

            //Remove (Clone) from name
            newAccessoryObj.name = accessoryTemplate.gameObject.name;

            //Collect game object references
            gameObjects = new GameObject[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) {
                gameObjects[i] = renderers[i].gameObject;
                renderers[i].gameObject.layer = gameObject.layer;
            }

            //Any type of renderer
            var activeAccessory = new ActiveAccessory {
                AccessoryComponent = newAccessoryObj.GetComponent<AccessoryComponent>(),
                rootTransform = newAccessoryObj.transform,
                gameObjects = gameObjects,
                renderers = renderers
            };
            addedAccessories.Add(activeAccessory);
            _activeAccessories[accessoryTemplate.accessorySlot] = activeAccessory;
        }

        if (rebuildMeshImmediately) TryCombineMeshes();

        return addedAccessories.ToArray();
    }

    public void AddSkinAccessory(AccessorySkin skin, bool rebuildMeshImmediately) {
        if (skin.skinTextureDiffuse == null) Debug.LogError("Trying to set entity skin to empty texture");

        if (rig.baseMeshes != null) {
            foreach (var mesh in rig.baseMeshes) {
                mesh.material.mainTexture = skin.skinTextureDiffuse;
                if (skin.skinTextureORM) mesh.material.SetTexture(OrmTex, skin.skinTextureORM);
            }
        }

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    public void SetSkinColor(Color color, bool rebuildMeshImmediately = true) {
        SetMeshColor(rig.bodyMesh, color);
        SetMeshColor(rig.headMesh, color);
        SetMeshColor(rig.armsMesh, color);

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    private void SetMeshColor(Renderer ren, Color color) {
        var mat = ren.gameObject.GetComponent<MaterialColorURP>();
        if(mat){
            var colors = mat.colorSettings;
            colors[0].baseColor = color;
            mat.colorSettings = colors;
            mat.DoUpdate();
        }
    }

    public void SetAccessoryColor(AccessorySlot slot, Color color, bool rebuildMeshImmediately = true) {
        var acc = GetActiveAccessoryBySlot(slot);
        foreach (var ren in acc.renderers) {
            SetMeshColor(ren, color);
        }

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    public void SetFaceTexture(Texture2D texture) {
        var propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetTexture("_BaseMap", texture);
        rig.faceMesh.SetPropertyBlock(propertyBlock);
    }

    public void TryCombineMeshes() {
        if (meshCombiner.enabled && Application.isPlaying) {
            //COMBINE MESHES
            meshCombiner.sourceReferences.Clear();
            
            //BODY
            if (rig.baseMeshes != null) {
                foreach (var ren in rig.baseMeshes) {
                    //Debug.Log("BaseMesh Add: " + ren.gameObject.name);D
                    meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                    ren.gameObject.SetActive(false);
                }
            }

            //ACCESSORIES
            var meshCombinedAcc = false;

            foreach (var kvp in _activeAccessories) {
                var acc = kvp.Value.AccessoryComponent;
                //Debug.Log("Adding accessory: " + acc.name);
                
                if (ShouldCombine(acc) == false) {
                    //Debug.Log("Skipping: " + acc.name);
                    continue;
                }

                //Map static objects to bones
                if (!acc.skinnedToCharacter) {
                    
                    var boneMap = acc.gameObject.GetComponent<MeshCombinerBone>();
                    if (boneMap == null) boneMap = acc.gameObject.AddComponent<MeshCombinerBone>();

                    boneMap.boneName = acc.gameObject.transform.parent.name;
                    
                    boneMap.scale = acc.transform.localScale;
                    boneMap.rotationOffset = acc.transform.localEulerAngles;
                    boneMap.positionOffset = acc.transform.localPosition;
                }
                    
                foreach (var ren in kvp.Value.renderers) {
                    
                    meshCombinedAcc = false;
                    if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.ThirdPerson ||
                            acc.visibilityMode == AccessoryComponent.VisibilityMode.Both) && !firstPerson) {
                        //VISIBLE IN THIRD PERSON
                        meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                        meshCombinedAcc = true;
                    }

                    if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.FirstPerson ||
                            acc.visibilityMode == AccessoryComponent.VisibilityMode.Both) && firstPerson) {
                        //VISIBLE IN FIRST PERSON
                        meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                        meshCombinedAcc = true;
                    }

                    ren.gameObject.SetActive(!meshCombinedAcc);

                    var skinnedRen = ren as SkinnedMeshRenderer;
                    if (skinnedRen) {
                        skinnedRen.rootBone = rig.bodyMesh.rootBone;
                        skinnedRen.bones = rig.bodyMesh.bones;
                    }
                }
            }


            // print("AccessoryBuilder MeshCombine: " + this.gameObject.name);
            meshCombiner.LoadMeshCopies();
            meshCombiner.CombineMeshes();
        } else {
            //MAP ITEMS TO RIG
            // print("AccessoryBuilder Manual Rig Mapping: " + this.gameObject.name);
            MapAccessoriesToRig();
            OnCombineComplete(false);
        }
    }

    private void MapAccessoriesToRig(){
        if(_activeAccessories == null){
            Debug.LogError("No active accessories but trying to map them?");
            return;
        }
        if(rig.armsMesh == null){
            Debug.LogError("Missing armsMesh on rig. armsMesh is a required reference");
            return;
        }
        foreach (var kvp in _activeAccessories) {
            if(kvp.Value.renderers == null){
                Debug.LogError("Missing renderers on active accessory: " + kvp.Key);
                continue;
            }
            foreach (var ren in kvp.Value.renderers) {
                if (ren == null) {
                    Debug.LogError("null renderer in renderers array");
                    continue;
                }

                var skinnedRen = ren as SkinnedMeshRenderer;
                if (skinnedRen) {
                    skinnedRen.rootBone = rig.armsMesh.rootBone;
                    skinnedRen.bones = rig.armsMesh.bones;
                }
            }
        }
    }

    private bool ShouldCombine(AccessoryComponent acc) {
        //Dont combine held hand items
        return acc.canMeshCombine && acc.accessorySlot != AccessorySlot.LeftHand && acc.accessorySlot != AccessorySlot.RightHand;
    }

    public ActiveAccessory GetActiveAccessoryBySlot(AccessorySlot target) {
        if (_activeAccessories.TryGetValue(target, out var items)) return items;

        return new ActiveAccessory();
    }

    public ActiveAccessory[] GetActiveAccessories() {
        var results = new ActiveAccessory[_activeAccessories.Count];
        _activeAccessories.Values.CopyTo(results, 0);
        return results;
    }

    public SkinnedMeshRenderer GetCombinedSkinnedMesh() {
        return meshCombiner.combinedSkinnedMeshRenderer;
    }

    public MeshRenderer GetCombinedStaticMesh() {
        return meshCombiner.combinedStaticMeshRenderer;
    }

    //Event from MeshCombine component
    private void OnMeshCombineCompleted(){
        OnCombineComplete(true);
    }

    private void OnCombineComplete(bool usedMeshCombiner) {
        //Mesh Combine Complete
        OnMeshCombined?.Invoke(usedMeshCombiner, meshCombiner.combinedSkinnedMeshRenderer, meshCombiner.combinedStaticMeshRenderer);
    }

    public Renderer[] GetAllAccessoryMeshes() {
        var renderers = new List<Renderer>();
        foreach (var keyValuePair in _activeAccessories) {
            foreach (var go in keyValuePair.Value.gameObjects) {
                var rens = go.GetComponentsInChildren<Renderer>();
                for (var i = 0; i < rens.Length; i++) renderers.Add(rens[i]);
            }
        }

        if (meshCombiner.combinedSkinnedMeshRenderer) renderers.Add(meshCombiner.combinedSkinnedMeshRenderer);
        if (meshCombiner.combinedSkinnedMeshRenderer) renderers.Add(meshCombiner.combinedStaticMeshRenderer);

        return renderers.ToArray();
    }

    public Renderer[] GetAccessoryMeshes(AccessorySlot slot) {
        var renderers = new List<Renderer>();
        var activeAccessory = GetActiveAccessoryBySlot(slot);
        if(activeAccessory.renderers != null){
            foreach (var ren in activeAccessory.renderers){
                renderers.Add(ren);
            }
        }
        return renderers.ToArray();
    }

    public ParticleSystem[] GetAccessoryParticles(AccessorySlot slot) {
        var results = new List<ParticleSystem>();
        var activeAccessory = GetActiveAccessoryBySlot(slot);
        if(activeAccessory.gameObjects != null){
            foreach (var go in activeAccessory.gameObjects) {
                var particles = go.GetComponentsInChildren<ParticleSystem>();
                foreach (var particle in particles) results.Add(particle);
            }
        }

        return results.ToArray();
    }

    public void SetCreateOverlayMeshOnCombine(bool on){
        if(this.meshCombiner){
            this.meshCombiner.createOverlayMesh = on;
        }
    }
}