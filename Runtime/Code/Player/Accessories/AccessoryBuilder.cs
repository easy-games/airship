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

    [SerializeField] public MeshCombiner meshCombiner;
    public bool firstPerson;

    private Dictionary<AccessorySlot, ActiveAccessory> activeAccessories = new Dictionary<AccessorySlot, ActiveAccessory>();

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

    private void OnDestroy() {
        // Manually release mesh combiner (in case Luau is holding a reference to this AccessoryBuilder)
        meshCombiner = null;
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

    private ActiveAccessory MakeActiveAccessoryFromAlreadyInstantiatedAccessory(AccessoryComponent accessoryComponent) {
        MeshRenderer[] meshRenderers;
        SkinnedMeshRenderer[] skinnedMeshRenderers;
        Renderer[] renderers;
        if (accessoryComponent.skinnedToCharacter) {
            meshRenderers = Array.Empty<MeshRenderer>();
            skinnedMeshRenderers = accessoryComponent.GetComponentsInChildren<SkinnedMeshRenderer>();
            renderers = skinnedMeshRenderers;
        } else {
            meshRenderers = accessoryComponent.GetComponentsInChildren<MeshRenderer>();
            skinnedMeshRenderers = Array.Empty<SkinnedMeshRenderer>();
            renderers = meshRenderers;
        }

        MeshFilter[] meshFilters = accessoryComponent.GetComponentsInChildren<MeshFilter>();
        GameObject[] gameObjects = new GameObject[meshRenderers.Length + skinnedMeshRenderers.Length];
        int i = 0;
        foreach (var r in meshRenderers) {
            gameObjects[i] = r.gameObject;
            i++;
        }
        foreach (var r in skinnedMeshRenderers) {
            gameObjects[i] = r.gameObject;
            i++;
        }

        var activeAccessory = new ActiveAccessory {
            AccessoryComponent = accessoryComponent,
            rootTransform = accessoryComponent.transform,
            gameObjects = gameObjects,
            meshRenderers = meshRenderers,
            skinnedMeshRenderers = skinnedMeshRenderers,
            meshFilters = meshFilters,
            renderers = renderers,
        };
        return activeAccessory;
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
            if (activeAccessories.ContainsKey(accessoryComponent.accessorySlot)) {
                this.RemoveAccessorySlot(accessoryComponent.accessorySlot, false);
            }

            var activeAccessory = this.MakeActiveAccessoryFromAlreadyInstantiatedAccessory(accessoryComponent);
            activeAccessories[accessoryComponent.accessorySlot] = activeAccessory;
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
        foreach (var pair in activeAccessories) {
            if (pair.Value.rootTransform == null) continue;
            if (Application.isPlaying) {
                Destroy(pair.Value.rootTransform.gameObject);
            } else {
                DestroyImmediate(pair.Value.rootTransform.gameObject);
            }
        }
        activeAccessories.Clear();

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    /// <summary>
    ///     Remove all clothing accessories from the character.
    ///     Not clothing: right and left hands.
    /// </summary>
    public void RemoveClothingAccessories(bool rebuildMeshImmediately = true) {
        var toDelete = new List<AccessorySlot>();
        foreach (var pair in activeAccessories) {
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
            activeAccessories.Remove(slot);
        }

        currentOutfit = null;

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    /// <summary>
    ///     Remove all accessories from the entity that are in the given slot.
    /// </summary>
    /// <param name="slot">Slot from which to remove accessories.</param>
    public void RemoveAccessorySlot(AccessorySlot slot, bool rebuildMeshImmediately = true) {
        if (activeAccessories.TryGetValue(slot, out var accessoryObjs)) {
            if (Application.isPlaying) {
                Destroy(accessoryObjs.rootTransform.gameObject);
            } else {
                DestroyImmediate(accessoryObjs.rootTransform.gameObject);
            }
            activeAccessories.Remove(slot);
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

        if (data == null) {
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

        if (outfitDto == null) {
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
                if(activeAccessories.ContainsKey(accessoryTemplate.accessorySlot)){
                    continue;
                }
            } // In 'Replace' mode, remove all accessories that are in the slots of the new accessories:
            else if(addMode == AccessoryAddMode.Replace){
                RemoveAccessorySlot(accessoryTemplate.accessorySlot, false);
            }

            MeshRenderer[] meshRenderers;
            SkinnedMeshRenderer[] skinnedMeshRenderers;
            Renderer[] renderers;

            GameObject[] gameObjects;
            GameObject newAccessoryObj;
            if (accessoryTemplate.skinnedToCharacter) {
                //Anything for skinned meshes connected to the main character
                //Create the prefab at the root of the rig
                newAccessoryObj = Instantiate(accessoryTemplate.gameObject, rig.transform);
                skinnedMeshRenderers = newAccessoryObj.GetComponentsInChildren<SkinnedMeshRenderer>();
                meshRenderers = Array.Empty<MeshRenderer>();
                renderers = skinnedMeshRenderers;
                if (skinnedMeshRenderers.Length == 0) {
                    Debug.LogError("Accessory is marked as skinned but has no SkinnedMeshRenderers on it: " + accessoryTemplate.name);
                }
            } else {
                //Anything for static meshes
                var parent = rig.GetSlotTransform(accessoryTemplate.accessorySlot);
                //Create the prefab on the joint
                newAccessoryObj = Instantiate(accessoryTemplate.gameObject, parent);
                meshRenderers = newAccessoryObj.GetComponentsInChildren<MeshRenderer>();
                skinnedMeshRenderers = Array.Empty<SkinnedMeshRenderer>();
                renderers = meshRenderers;
            }
            MeshFilter[] meshFilters = newAccessoryObj.GetComponentsInChildren<MeshFilter>();

            //Remove (Clone) from name
            newAccessoryObj.name = accessoryTemplate.gameObject.name;

            //Collect game object references
            gameObjects = new GameObject[meshRenderers.Length];
            for (var i = 0; i < meshRenderers.Length; i++) {
                gameObjects[i] = meshRenderers[i].gameObject;
                meshRenderers[i].gameObject.layer = gameObject.layer;
            }

            //Any type of renderer
            var activeAccessory = new ActiveAccessory {
                AccessoryComponent = newAccessoryObj.GetComponent<AccessoryComponent>(),
                rootTransform = newAccessoryObj.transform,
                gameObjects = gameObjects,
                meshRenderers = meshRenderers,
                skinnedMeshRenderers = skinnedMeshRenderers,
                meshFilters = meshFilters,
                renderers = renderers,
            };
            addedAccessories.Add(activeAccessory);
            activeAccessories[accessoryTemplate.accessorySlot] = activeAccessory;
        }

        if (rebuildMeshImmediately) {
            TryCombineMeshes();
        }

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
        foreach (var ren in acc.meshRenderers) {
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
        if (this.meshCombiner.enabled && Application.isPlaying) {
            this.meshCombiner.ClearSourceReferences();
            
            // Add body meshes as source references on mesh combiner
            foreach (var ren in rig.baseMeshes) {
                // this.meshCombiner.AddSourceReference(
                //     ren.transform,
                //     Array.Empty<MeshRenderer>(),
                //     new[] { ren as SkinnedMeshRenderer },
                //     new[] { ren.transform.GetComponent<MeshFilter>() }
                // );
                // meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                ren.gameObject.SetActive(false);
            }

            // Accessories
            var isCombined = false;

            foreach (var pair in activeAccessories) {
                var activeAccessory = pair.Value;
                var acc = pair.Value.AccessoryComponent;

                if (ShouldCombine(acc) == false) {
                    //Debug.Log("Skipping: " + acc.name);
                    continue;
                }

                // Map static objects to bones
                if (!acc.skinnedToCharacter) {
                    var boneMap = acc.gameObject.GetComponent<MeshCombinerBone>();
                    if (boneMap == null) boneMap = acc.gameObject.AddComponent<MeshCombinerBone>();

                    boneMap.boneName = acc.gameObject.transform.parent.name;

                    boneMap.scale = acc.transform.localScale;
                    boneMap.rotationOffset = acc.transform.localEulerAngles;
                    boneMap.positionOffset = acc.transform.localPosition;
                }

                this.meshCombiner.AddSourceReference(activeAccessory);

                foreach (var ren in activeAccessory.skinnedMeshRenderers) {
                    ren.rootBone = rig.bodyMesh.rootBone;
                    ren.bones = rig.bodyMesh.bones;
                }
                foreach (var ren in activeAccessory.renderers) {
                    isCombined = false;
                    if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.ThirdPerson ||
                            acc.visibilityMode == AccessoryComponent.VisibilityMode.Both) && !firstPerson) {
                        // Visible in third person
                        // this.meshCombiner.AddSourceReference(activeAccessory);
                        // meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                        isCombined = true;
                    }

                    if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.FirstPerson ||
                            acc.visibilityMode == AccessoryComponent.VisibilityMode.Both) && firstPerson) {
                        // Visible in first person
                        // this.meshCombiner.AddSourceReference(activeAccessory);
                        // meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                        isCombined = true;
                    }

                    ren.gameObject.SetActive(!isCombined);
                }
            }

            // print("AccessoryBuilder MeshCombine: " + this.gameObject.name);
            meshCombiner.CombineMeshes();
        } else {
            //MAP ITEMS TO RIG
            // print("AccessoryBuilder Manual Rig Mapping: " + this.gameObject.name);
            MapAccessoriesToRig();
            OnCombineComplete(false);
        }
    }

    private void MapAccessoriesToRig(){
        if (activeAccessories == null){
            Debug.LogError("No active accessories but trying to map them?");
            return;
        }
        if (rig.armsMesh == null){
            Debug.LogError("Missing armsMesh on rig. armsMesh is a required reference");
            return;
        }
        foreach (var pair in activeAccessories) {
            foreach (var ren in pair.Value.skinnedMeshRenderers) {
                ren.rootBone = rig.armsMesh.rootBone;
                ren.bones = rig.armsMesh.bones;
            }
        }
    }

    private bool ShouldCombine(AccessoryComponent acc) {
        //Dont combine held hand items
        return acc.canMeshCombine && acc.accessorySlot != AccessorySlot.LeftHand && acc.accessorySlot != AccessorySlot.RightHand;
    }

    public ActiveAccessory GetActiveAccessoryBySlot(AccessorySlot target) {
        if (activeAccessories.TryGetValue(target, out var items)) return items;

        return new ActiveAccessory();
    }

    public ActiveAccessory[] GetActiveAccessories() {
        var results = new ActiveAccessory[activeAccessories.Count];
        activeAccessories.Values.CopyTo(results, 0);
        return results;
    }

    public SkinnedMeshRenderer GetCombinedSkinnedMesh() {
        return meshCombiner.combinedSkinnedMeshRenderer;
    }

    //Event from MeshCombine component
    private void OnMeshCombineCompleted(){
        OnCombineComplete(true);
    }

    private void OnCombineComplete(bool usedMeshCombiner) {
        //Mesh Combine Complete
        OnMeshCombined?.Invoke(usedMeshCombiner, meshCombiner.combinedSkinnedMeshRenderer, meshCombiner.combinedSkinnedMeshRenderer);
    }

    public Renderer[] GetAllAccessoryMeshes() {
        var renderers = new List<Renderer>();
        foreach (var keyValuePair in activeAccessories) {
            foreach (var go in keyValuePair.Value.gameObjects) {
                var rens = go.GetComponentsInChildren<Renderer>();
                for (var i = 0; i < rens.Length; i++) renderers.Add(rens[i]);
            }
        }

        renderers.Add(meshCombiner.combinedSkinnedMeshRenderer);
        // if (meshCombiner.combinedSkinnedMeshRenderer) renderers.Add(meshCombiner.combinedStaticMeshRenderer);

        return renderers.ToArray();
    }

    public Renderer[] GetAccessoryMeshes(AccessorySlot slot) {
        var renderers = new List<Renderer>();
        var activeAccessory = GetActiveAccessoryBySlot(slot);
        if(activeAccessory.meshRenderers != null){
            foreach (var ren in activeAccessory.meshRenderers){
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