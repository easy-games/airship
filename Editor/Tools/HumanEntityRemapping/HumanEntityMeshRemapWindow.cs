using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class HumanEntityMeshRemapWindow : EditorWindow {
    private const string HumanEntityPath = "Assets/Bundles/@Easy/Core/Shared/Resources/Character/AirshipCharacter.prefab";
    [Header("Templates")]
    public GameObject newMeshTemplate;

    [Header("Variables")]
    public string meshHolderName = "HumanEntityMesh";
    public string armatureName = "Armature";
    
    private GameObjectReferences entityRefs;
    private SkinnedMeshBoneSetter boneSetter;
    
    [MenuItem("Window/Airship/Human Entity Tools")]
    [MenuItem("Airship/Misc/Prefab Tools/Human Entity Tools")]
    public static HumanEntityMeshRemapWindow OpenWindow() {
        var window = GetWindow<HumanEntityMeshRemapWindow>();
        return window;
    }

    public void OnGUI() {
        GUI.enabled = true;
        //TITLE
        EditorGUILayout.LabelField("Apply a new mesh to the HumanEntity prefab");

        newMeshTemplate = EditorGUILayout.ObjectField(new GUIContent("New Mesh"), newMeshTemplate, typeof(GameObject), true) as GameObject;
        //DrawDefaultInspector(); 
        
        //Render apply button
        GUILayout.Space(40);
        if (newMeshTemplate == null) {
            GUI.enabled = false;
        }
        if (GUILayout.Button("Apply New Mesh")) {
            ApplyNewMesh();
        }
        if (newMeshTemplate == null) {
            EditorGUILayout.LabelField("You need to choose a new mesh template");
        }
        GUI.enabled = true;
    }

    public void ApplyNewMesh() {
        GameObject gameObject = PrefabUtility.LoadPrefabContents(HumanEntityPath);
        if (gameObject == null) {
            Debug.LogError("Human Entity prefab not found at: " + HumanEntityPath);
            return;
        }
        Debug.Log("Applying new mesh to HumanEntity");
        Undo.RecordObject(gameObject, "New Human Entity Mesh");
        
        entityRefs = gameObject.GetComponent<GameObjectReferences>();
        boneSetter = gameObject.GetComponent<SkinnedMeshBoneSetter>();
        
        //Spawn the new graphics
        Debug.Log("Spawning new graphics");
        var graphicsHolder = entityRefs.GetValueTyped<Transform>("Bones", "Root");
        var oldMesh = graphicsHolder.Find(meshHolderName);
        var newMesh = GameObject.Instantiate(newMeshTemplate, graphicsHolder);
        newMesh.name = meshHolderName;
        
        //Remap skinned meshes to existing armature
        boneSetter.remappingMode = SkinnedMeshBoneSetter.RemappingMode.EXISTING_ARMATURE;
        boneSetter.targetSkinnedMeshRenderers = newMesh.GetComponentsInChildren<SkinnedMeshRenderer>();
        boneSetter.ApplyBones();
        
        //Destroy the old mesh
        if (oldMesh) {
            Debug.Log("Destroying old graphics");
            GameObject.DestroyImmediate(oldMesh.gameObject);
        }
        
        //Save these meshes to the references
        Debug.Log("Assigning to GameObjectReferences");
        SkinnedMeshRenderer body = null;
        SkinnedMeshRenderer FPS = null;
        foreach (var skinnedMesh in boneSetter.targetSkinnedMeshRenderers) {
            if (skinnedMesh.gameObject.name.Contains("FPS") || skinnedMesh.gameObject.name.Contains("FirstPerson")) {
                FPS = skinnedMesh;
            } else {
                if (body) {
                    Debug.LogError("Found multiple body meshes, this is not currently supported!");
                }
                body = skinnedMesh;
            }
        }

        if (body) {
            entityRefs.SetValue("Meshes", "Body", body);
        } else {
            Debug.LogError("Unable to find skinned mesh on spawned human entity graphic");
        }

        if (FPS) {
            entityRefs.SetValue("Meshes", "FirstPerson", FPS);
        } else {
            Debug.LogError("Unable to find FPS skinned mesh on spawned human entity graphic");
        }
        
        //Record changes to material color for any prefab components
        PrefabUtility.RecordPrefabInstancePropertyModifications(gameObject);
        PrefabUtility.SaveAsPrefabAsset(gameObject, HumanEntityPath);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(entityRefs);
        
        //Add this to Undo Stack
        Undo.FlushUndoRecordObjects();
        PrefabUtility.UnloadPrefabContents(gameObject);
        
        
        Debug.Log("Done applying new mesh");
    }
}