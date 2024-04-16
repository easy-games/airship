using System;
using System.Collections.Generic;
using Airship;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

[LuauAPI]
public class AccessoryBuilder : MonoBehaviour
{
    private static readonly int OrmTex = Shader.PropertyToID("_ORMTex");

    public CharacterRig rig;

    [SerializeField] private MeshCombiner meshCombiner;
    public bool firstPerson;

    [HideInInspector] public int firstPersonLayer;

    [HideInInspector] public int thirdPersonLayer;

    private Dictionary<AccessorySlot, List<ActiveAccessory>> _activeAccessories;

    private void Awake()
    {
        firstPersonLayer = LayerMask.NameToLayer("ViewModel");
        thirdPersonLayer = LayerMask.NameToLayer("Character");
        _activeAccessories = new Dictionary<AccessorySlot, List<ActiveAccessory>>();

        if (!rig)
            Debug.LogError(
                "Unable to find rig references. Assing the rig in the prefab");
    }

    private void OnEnable()
    {
        meshCombiner.OnCombineComplete += OnCombineComplete;
    }

    private void OnDisable()
    {
        meshCombiner.OnCombineComplete -= OnCombineComplete;
    }

    /// <summary>
    ///     Remove all accessories from the character.
    /// </summary>
    public void RemoveAccessories()
    {
        foreach (var pair in _activeAccessories)
        {
            foreach (var activeAccessory in pair.Value)
            foreach (var go in activeAccessory.gameObjects)
                Destroy(go);
            pair.Value.Clear();
        }
    }

    /// <summary>
    ///     Remove all clothing accessories from the character.
    ///     Not clothing: right and left hands.
    /// </summary>
    public void RemoveClothingAccessories()
    {
        foreach (var pair in _activeAccessories) {
            if (pair.Key is AccessorySlot.RightHand or AccessorySlot.LeftHand) continue;
            foreach (var activeAccessory in pair.Value) {
                foreach (var go in activeAccessory.gameObjects)
                    Destroy(go);
            }

            pair.Value.Clear();
        }
    }

    /// <summary>
    ///     Remove all accessories from the entity that are in the given slot.
    /// </summary>
    /// <param name="slot">Slot from which to remove accessories.</param>
    public void RemoveAccessorySlot(AccessorySlot slot, bool rebuildMeshImmediately)
    {
        DestroyAccessorySlot(slot);

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    private void DestroyAccessorySlot(AccessorySlot slot)
    {
        if (_activeAccessories.TryGetValue(slot, out var accessoryObjs))
        {
            foreach (var activeAccessory in accessoryObjs) Destroy(activeAccessory.rootTransform.gameObject);
            accessoryObjs.Clear();
        }
    }

    public ActiveAccessory AddSingleAccessory(AccessoryComponent accessoryTemplate, bool rebuildMeshImmediately)
    {
        return AddAccessories(new[] { accessoryTemplate }, AccessoryAddMode.Replace, rebuildMeshImmediately)[0];
    }

    public ActiveAccessory[] EquipAccessoryOutfit(AccessoryOutfit outfit, bool rebuildMeshImmediately = true)
    {
        if (outfit.customSkin) AddSkinAccessory(outfit.customSkin, false);
        return AddAccessories(outfit.accessories, AccessoryAddMode.Replace, rebuildMeshImmediately);
    }

    /// <summary>
    ///     Add all accessories to the entity. The <c>addMode</c> parameter describes <i>how</i> the
    ///     accessories to be added to the entity, assuming other accessories might already exist
    ///     on the entity.
    /// </summary>
    /// <param name="accessoryTemplates">Accessories to add.</param>
    /// <param name="addMode">The add behavior.</param>
    public ActiveAccessory[] AddAccessories(AccessoryComponent[] accessoryTemplates, AccessoryAddMode addMode,
        bool rebuildMeshImmediately)
    {
        var addedAccessories = new List<ActiveAccessory>();

        // In 'Replace' mode, remove all accessories that are in the slots of the new accessories:
        if (addMode == AccessoryAddMode.Replace)
            foreach (var accessory in accessoryTemplates)
                DestroyAccessorySlot(accessory.accessorySlot);
        // In 'ReplaceAll' mode, remove all existing accessories:
        else if (addMode == AccessoryAddMode.ReplaceAll)
            foreach (var pair in _activeAccessories)
            {
                foreach (var activeAccessory in pair.Value) Destroy(activeAccessory.rootTransform.gameObject);
                pair.Value.Clear();
            }

        // Add accessories:
        foreach (var accessoryTemplate in accessoryTemplates)
        {
            if (!_activeAccessories.ContainsKey(accessoryTemplate.accessorySlot))
                _activeAccessories.Add(accessoryTemplate.accessorySlot, new List<ActiveAccessory>());

            // In 'AddIfNone' mode, don't add the accessory if one already exists in the slot:
            if (addMode == AccessoryAddMode.AddIfNone &&
                _activeAccessories[accessoryTemplate.accessorySlot].Count > 0) continue;

            //Create the accessory game object
            ActiveAccessory? activeAccessory = null;
            Renderer[] renderers;
            GameObject[] gameObjects;
            GameObject newAccessoryObj;
            if (accessoryTemplate.skinnedToCharacter)
            {
                //Anything for skinned meshes connected to the main character
                //Create the prefab at the root
                newAccessoryObj = Instantiate(accessoryTemplate.gameObject, rig.bodyMesh.transform.parent);
                renderers = newAccessoryObj.GetComponentsInChildren<SkinnedMeshRenderer>();
                if(renderers.Length == 0){
                    Debug.LogError("Accessory marked as skinned but no skinned renderers are on it: " + accessoryTemplate.name);
                }
            }
            else
            {
                //Anything for static meshes
                var parent = rig.GetSlotTransform(accessoryTemplate.accessorySlot);
                //Create the prefab on the joint
                newAccessoryObj = Instantiate(accessoryTemplate.gameObject, parent);
                renderers = newAccessoryObj.GetComponentsInChildren<Renderer>();
                if(renderers.Length == 0){
                    Debug.LogError("Accessory with no renderers are on it: " + accessoryTemplate.name);
                }
            }

            //Remove (Clone) from name
            newAccessoryObj.name = accessoryTemplate.gameObject.name;

            //Collect game object references
            gameObjects = new GameObject[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) gameObjects[i] = renderers[i].gameObject;

            //Any type of renderer
            activeAccessory = new ActiveAccessory
            {
                AccessoryComponent = newAccessoryObj.GetComponent<AccessoryComponent>(),
                rootTransform = newAccessoryObj.transform,
                gameObjects = gameObjects,
                renderers = renderers
            };
            addedAccessories.Add(activeAccessory.Value);
            _activeAccessories[accessoryTemplate.accessorySlot].Add(activeAccessory.Value);
        }

        if (rebuildMeshImmediately) TryCombineMeshes();

        return addedAccessories.ToArray();
    }

    public void AddSkinAccessory(AccessorySkin skin, bool rebuildMeshImmediately)
    {
        if (skin.skinTextureDiffuse == null) Debug.LogError("Trying to set entity skin to empty texture");

        foreach (var mesh in rig.baseMeshes)
        {
            mesh.material.mainTexture = skin.skinTextureDiffuse;
            if (skin.skinTextureORM) mesh.material.SetTexture(OrmTex, skin.skinTextureORM);
        }

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    public void SetSkinColor(Color color, bool rebuildMeshImmediately)
    {
        foreach (var mesh in rig.baseMeshes)
        {
            var mat = mesh.GetComponent<MaterialColor>();
            if (!mat) continue;
            mat.SetMaterialColor(0, color);
        }

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    public void SetFaceTexture(Texture2D texture){
        rig.faceMesh.material.SetTexture("_MainTex", texture);
    }

    public void SetAccessoryColor(AccessorySlot slot, Color color, bool rebuildMeshImmediately)
    {
        var accs = GetActiveAccessoriesBySlot(slot);
        foreach (var acc in accs)
        foreach (var ren in acc.renderers)
        {
            var mat = ren.GetComponent<MaterialColor>();
            if (!mat) continue;
            mat.SetMaterialColor(0, color);
        }

        if (rebuildMeshImmediately) TryCombineMeshes();
    }

    public void TryCombineMeshes()
    {
        if (meshCombiner.enabled)
        {
            //COMBINE MESHES
            meshCombiner.sourceReferences.Clear();

            //BODY
            foreach (var ren in rig.baseMeshes) {
                meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                ren.gameObject.SetActive(false);
            }

            //ACCESSORIES
            var meshCombinedAcc = false;
            foreach (var kvp in _activeAccessories)
            foreach (var liveAcc in kvp.Value)
            {
                var acc = liveAcc.AccessoryComponent;
                if (ShouldCombine(acc))
                    foreach (var ren in liveAcc.renderers)
                    {
                        //Map static objects to bones
                        if (!acc.skinnedToCharacter)
                        {
                            var boneMap = ren.GetComponent<MeshCombinerBone>();
                            if (boneMap == null) boneMap = ren.gameObject.AddComponent<MeshCombinerBone>();

                            boneMap.boneName = liveAcc.gameObjects[0].transform.parent.name;
                            boneMap.scale = acc.transform.localScale;
                            boneMap.rotationOffset = acc.transform.localEulerAngles;
                            boneMap.positionOffset = acc.transform.localPosition;
                        }

                        meshCombinedAcc = false;
                        if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.THIRD_PERSON ||
                             acc.visibilityMode == AccessoryComponent.VisibilityMode.BOTH) && !firstPerson)
                        {
                            //VISIBLE IN THIRD PERSON
                            meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                            meshCombinedAcc = true;
                        }

                        if ((acc.visibilityMode == AccessoryComponent.VisibilityMode.FIRST_PERSON ||
                             acc.visibilityMode == AccessoryComponent.VisibilityMode.BOTH) && firstPerson)
                        {
                            //VISIBLE IN FIRST PERSON
                            meshCombiner.sourceReferences.Add(new MeshCombiner.MeshCopyReference(ren.transform));
                            meshCombinedAcc = true;
                        }

                        ren.gameObject.SetActive(!meshCombinedAcc);
                    }
            }

            meshCombiner.LoadMeshCopies();
            meshCombiner.CombineMeshes();
        }
        else
        {
            //MAP ITEMS TO RIG
            foreach (var kvp in _activeAccessories)
            foreach (var liveAcc in kvp.Value)
            foreach (var ren in liveAcc.renderers)
            {
                if (ren == null)
                {
                    Debug.LogError("null renderer in renderers array");
                    continue;
                }

                var skinnedRen = ren as SkinnedMeshRenderer;
                if (skinnedRen)
                {
                    skinnedRen.rootBone = rig.bodyMesh.rootBone;
                    skinnedRen.bones = rig.bodyMesh.bones;
                }
            }

            OnCombineComplete();
        }
    }

    private bool ShouldCombine(AccessoryComponent acc)
    {
        //Dont combine held hand items
        return acc.accessorySlot != AccessorySlot.LeftHand && acc.accessorySlot != AccessorySlot.RightHand;

        //Dont combine held hand items with rigs
        //return !((acc.AccessorySlot == AccessorySlot.LeftHand || acc.AccessorySlot == AccessorySlot.RightHand) && acc.HasSkinnedMeshes);
    }

    public ActiveAccessory[] GetActiveAccessoriesBySlot(AccessorySlot target)
    {
        if (_activeAccessories.TryGetValue(target, out var items)) return items.ToArray();

        return Array.Empty<ActiveAccessory>();
    }

    public ActiveAccessory[] GetActiveAccessories()
    {
        var results = new List<ActiveAccessory>();
        foreach (var keyValuePair in _activeAccessories)
        foreach (var activeAccessory in keyValuePair.Value)
            results.Add(activeAccessory);

        return results.ToArray();
    }

    public SkinnedMeshRenderer GetCombinedSkinnedMesh()
    {
        return meshCombiner.combinedSkinnedMeshRenderer;
    }

    public MeshRenderer GetCombinedStaticMesh()
    {
        return meshCombiner.combinedStaticMeshRenderer;
    }

    private void OnCombineComplete()
    {
        UpdateAccessoryLayers();
    }

    public void UpdateAccessoryLayers()
    {
        // Update layers of individual accessories
        foreach (var keyValuePair in _activeAccessories)
        foreach (var activeAccessory in keyValuePair.Value)
        foreach (var ren in activeAccessory.renderers)
        {
            ren.enabled
                = (!firstPerson && activeAccessory.AccessoryComponent.visibilityMode !=
                      AccessoryComponent.VisibilityMode.FIRST_PERSON) ||
                  (firstPerson && activeAccessory.AccessoryComponent.visibilityMode !=
                      AccessoryComponent.VisibilityMode.THIRD_PERSON);
            // print("AccessoryBuilder " + ren.gameObject.name + " enabled=" + ren.enabled);
            ren.gameObject.layer = firstPerson ? firstPersonLayer : thirdPersonLayer;
            ren.shadowCastingMode = firstPerson ? ShadowCastingMode.Off : ShadowCastingMode.On;
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

    public Renderer[] GetAllAccessoryMeshes()
    {
        var renderers = new List<Renderer>();
        foreach (var keyValuePair in _activeAccessories)
        foreach (var activeAccessory in keyValuePair.Value)
        foreach (var go in activeAccessory.gameObjects)
        {
            var rens = go.GetComponentsInChildren<Renderer>();
            for (var i = 0; i < rens.Length; i++) renderers.Add(rens[i]);
        }

        if (meshCombiner.combinedSkinnedMeshRenderer) renderers.Add(meshCombiner.combinedSkinnedMeshRenderer);
        if (meshCombiner.combinedSkinnedMeshRenderer) renderers.Add(meshCombiner.combinedStaticMeshRenderer);

        return renderers.ToArray();
    }

    public Renderer[] GetAccessoryMeshes(AccessorySlot slot)
    {
        var renderers = new List<Renderer>();
        var activeAccessories = GetActiveAccessoriesBySlot(slot);
        foreach (var aa in activeAccessories)
        foreach (var ren in aa.renderers)
            renderers.Add(ren);
        return renderers.ToArray();
    }

    public ParticleSystem[] GetAccessoryParticles(AccessorySlot slot)
    {
        var results = new List<ParticleSystem>();
        var activeAccessories = GetActiveAccessoriesBySlot(slot);
        foreach (var aa in activeAccessories)
        foreach (var go in aa.gameObjects)
        {
            var particles = go.GetComponentsInChildren<ParticleSystem>();
            foreach (var particle in particles) results.Add(particle);
        }

        return results.ToArray();
    }
}