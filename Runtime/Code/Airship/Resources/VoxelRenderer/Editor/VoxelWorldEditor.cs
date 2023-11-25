#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoxelWorld))]
public class VoxelWorldEditor : UnityEditor.Editor {
    private bool blockDatadebug;
    GameObject handle = null;
    GameObject raytraceHandle = null;
    bool raycastDebugMode = false;

    public void Load(VoxelWorld world)
    {
        if (world.voxelWorldFile != null)
        {
            world.LoadWorldFromSaveFile(world.voxelWorldFile);
        }
    }

    public override void OnInspectorGUI() {
        VoxelWorld world = (VoxelWorld)target;

        EditorGUILayout.LabelField("Configure Blocks", EditorStyles.boldLabel);
        {
            var style = EditorStyles.label;
            style.wordWrap = true;
            EditorGUILayout.LabelField("Add additional xml files to expand the list of blocks in the game. For reference, see CoreBlockDefines.xml\nIt is recommended to always include CoreBlockDefines.xml", style);
        }

        EditorGUILayout.Space(4);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blockDefines"), true);
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space(4);

        //Add big divider
        AirshipEditorGUI.HorizontalLine();
        EditorGUILayout.LabelField("Save Files", EditorStyles.boldLabel);
        {
            var style = EditorStyles.label;
            style.wordWrap = true;
            EditorGUILayout.LabelField("Worlds are saved as files. You can set the save file below and then load it.", style);
        }
        EditorGUILayout.Space(4);

        //Add a file picker for  voxelWorldFile
        world.voxelWorldFile = (WorldSaveFile)EditorGUILayout.ObjectField("Voxel World File", world.voxelWorldFile, typeof(WorldSaveFile), false);

        EditorGUILayout.Space(4);

        if (world.voxelWorldFile != null)
        {
            if (GUILayout.Button("Load"))
            {
                Load(world);
            }

            if (world.chunks.Count > 0)
            {
                if (GUILayout.Button("Save")) {
                    world.SaveToFile();
                }
            }
            else
            {
                //Draw greyed out save button
                GUI.enabled = false;

                if (GUILayout.Button("Save"))
                {


                }
                GUI.enabled = true;
                
            }
        } else {
            if (GUILayout.Button("Create New"))
            {

                var gameObjects = world.GetChildGameObjects();
                world.worldPositionEditorIndicators.Clear();
                world.pointLights.Clear();

                foreach (var go in gameObjects) {
                    if (go.name.Equals("Pointlight")) {
                        world.pointLights.Add(go);
                    }
                }

                WorldSaveFile saveFile = CreateInstance<WorldSaveFile>();
                saveFile.CreateFromVoxelWorld(world);

                //Create a file picker to save the file, prepopulate it with the asset path of world.asset
                string path = EditorUtility.SaveFilePanel("Save Voxel World", "Assets/Bundles/Server/Resources/Worlds", "VoxelWorld", "asset");
                string relativePath = "Assets/" + path.Split("Assets")[1];
                AssetDatabase.CreateAsset(saveFile, relativePath);
                world.UpdatePropertiesForAllChunksForRendering();
            }
        }

        EditorGUILayout.Space(4);
        AirshipEditorGUI.HorizontalLine();

        EditorGUILayout.LabelField("World Creator", EditorStyles.boldLabel);
        if (GUILayout.Button("Generate Full World"))
        {
            world.GenerateWorld(true);
        }
        if (GUILayout.Button("Generate Empty World"))
        {
            world.GenerateWorld(false);
        }

        AirshipEditorGUI.HorizontalLine();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);
        if (GUILayout.Button("Clear Visual Chunks"))
        {
            world.DeleteRenderedGameObjects();
        }
        EditorGUILayout.Space(4);
        AirshipEditorGUI.HorizontalLine();

        EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        //Add a Vector3 editor for globalLightDirection
        world.globalSunDirection = EditorGUILayout.Vector3Field("Sun Light Direction", world.globalSunDirection);

        //Add a float slider for globalBrightness
        world.globalSunBrightness = EditorGUILayout.Slider("Sun Brightness", world.globalSunBrightness, 0, 10);
        

        //Add a color picker for sun + sky (sky should sample skybox)
        world.globalSunColor = EditorGUILayout.ColorField("Sun Color", world.globalSunColor);
        world.globalSkySaturation = EditorGUILayout.Slider("Sky Saturation", world.globalSkySaturation,0,2);

        //add a text field to get just the string asset path to a cubemap for the skybox, for world.cubeMapPath
        world.cubeMapPath = EditorGUILayout.TextField("Cube Map Path", world.cubeMapPath);
        //Add a button to pick the cubeMap file, and store its path in world.cubeMapPath
        if (GUILayout.Button("Pick Cube Map"))
        {
            CubemapPickerWindow.Show(cubemapPath =>
            {
                cubemapPath = cubemapPath.ToLower();
                
                string relativePath = cubemapPath.Split("/resources/")[1];
                world.cubeMapPath = relativePath;
            });
        }

        if (GUILayout.Button("Reload Atlas"))
        {
            world.ReloadTextureAtlas();
        }

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        //Add a color picker for ambient
        world.globalAmbientLight = EditorGUILayout.ColorField("Global Ambient Light", world.globalAmbientLight);
        //Add brightness for ambient
        world.globalAmbientBrightness = EditorGUILayout.Slider("Ambient Brightness", world.globalAmbientBrightness, 0, 10);

        //Add a float slider for globalAmbientOcclusion
        world.globalAmbientOcclusion = EditorGUILayout.Slider("Global AmbientOcclusion", world.globalAmbientOcclusion, 0, 1);

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        //Add a toggle button for world.isRadiosityEnabled
        world.radiosityEnabled = EditorGUILayout.Toggle("Radiosity Enabled", world.radiosityEnabled);

        //Add a float slider for globalRadiosityScale
        world.globalRadiosityScale = EditorGUILayout.Slider("Global RadiosityScale", world.globalRadiosityScale, 0, 3);

        //Add a float slider for globalRadiosityScale
        world.globalRadiosityDirectLightAmp = EditorGUILayout.Slider("Radiosity Direct Light Amp", world.globalRadiosityDirectLightAmp, 0, 5);
        world.globalSkyBrightness = EditorGUILayout.Slider("Radiosity Sky Brightness", world.globalSkyBrightness, 0, 10);


        // World Networker picker
        world.worldNetworker = (VoxelWorldNetworker)EditorGUILayout.ObjectField("Voxel World Networker", world.worldNetworker, typeof(VoxelWorldNetworker), true);

        //Add a seperator
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(3) });

        //Add globalFogStart, globalFogEnd, and globalFogColor
        world.globalFogStart = EditorGUILayout.Slider("Fog Start", world.globalFogStart, 0.0f, 10000.0f);
        world.globalFogEnd = EditorGUILayout.Slider("Fog End", world.globalFogEnd, 0.0f, 10000.0f);
        world.globalFogColor = EditorGUILayout.ColorField("Fog Color", world.globalFogColor);

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });
        world.autoLoad = EditorGUILayout.Toggle("Auto Load", world.autoLoad);

        //if (GUILayout.Button("Emit block"))
        //{
        //    MeshProcessor.ProduceSingleBlock(1, world);
        //}

        if (GUI.changed)
        {
            // writing changes of the testScriptable into Undo
            Undo.RecordObject(target, "Test Scriptable Editor Modify");
            // mark the testScriptable object as "dirty" and save it
            EditorUtility.SetDirty(target);

            // Trigger a repaint
            world.FullWorldUpdate();
        }
    }

    private void OnDisable() {
        if (this.handle) {
            DestroyImmediate(this.handle);
        }


        if (this.raytraceHandle) {
            DestroyImmediate(this.raytraceHandle);
        }
    }

    private void OnSceneGUI() {
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        VoxelWorld world = (VoxelWorld)target;
        Event e = Event.current;

        if (e.type == EventType.MouseMove) {
            if (raycastDebugMode)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);//, out Vector3 pos, out Vector3 hitNormal);

                if (res == true)
                {
                    if (raytraceHandle)
                    {
                        GameObject.DestroyImmediate(raytraceHandle);


                    }
                    Vector3[] raySamples = world.radiosityRaySamples[world.Vector3ToNearestIndex(normal)];
                    float range = 16;

                    //create rayTraceHandle
                    raytraceHandle = new GameObject("RaytraceHandle");

                    for (int i = 0; i < raySamples.Length; i++)
                    {
                        Vector3 rayPos = hitPosition;
                        Vector3 rayDir = raySamples[i];
                        (bool res2, float distance2, Vector3 hitPosition2, Vector3 normal2) = world.RaycastVoxel_Internal(rayPos, rayDir, range);//, out Vector3 pos, out Vector3 hitNormal);

                        if (res2)
                        {
                            //Create debug lines from rayPos to hitPosition2
                            GameObject line = new GameObject("Line");
                            line.transform.parent = raytraceHandle.transform;
                            line.transform.position = rayPos;
                            LineRenderer lineRenderer = line.AddComponent<LineRenderer>();
                            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                            lineRenderer.startColor = Color.red;
                            lineRenderer.endColor = Color.red;
                            lineRenderer.startWidth = 0.1f;
                            lineRenderer.endWidth = 0.1f;
                            lineRenderer.SetPosition(0, rayPos);
                            lineRenderer.SetPosition(1, hitPosition2);


                        }
                    }
                }
            } else {
                // Create a ray from the mouse position
                Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

                (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);//, out Vector3 pos, out Vector3 hitNormal);

                if (res == true)
                {
                    if (handle == null)
                    {
                        handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        handle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);
                        handle.transform.parent = world.transform;
                        MeshRenderer ren = handle.GetComponent<MeshRenderer>();
                        ren.sharedMaterial = UnityEngine.Resources.Load<Material>("Selection");
                    }
                    //handle.transform.position = pos + new Vector3(0.5f, 0.5f, 0.5f); //;//+  VoxelWorld.FloorInt(pos)+ new Vector3(0.5f,0.5f,0.5f);
                    // Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                    Vector3 pos = hitPosition + (normal * 0.1f);
                    handle.transform.position = VoxelWorld.FloorInt(pos) + new Vector3(0.5f, 0.5f, 0.5f);
                    //Debug.Log("Mouse on cell" + VoxelWorld.FloorInt(pos));

                }
            }
        }

        //Leftclick up
        if (e.type == EventType.MouseUp && e.button == 0) {
            // Create a ray from the mouse position
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);
            if (res) {
                Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                if (Event.current.shift) {
                    //set voxel to 0
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos);
                    world.WriteVoxelAtInternal(voxelPos, 0);
                    world.DirtyNeighborMeshes(voxelPos, true);
                } else {
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos) + VoxelWorld.FloorInt(normal);
                    world.WriteVoxelAtInternal(voxelPos, (byte)world.selectedBlockIndex);
                    world.DirtyNeighborMeshes(voxelPos, true);
                }
            }

        }

        if (Event.current.GetTypeForControl(controlID) == EventType.KeyDown) {
            if (Event.current.keyCode == KeyCode.LeftShift) {
                //shiftDown = true;
            }
        }
        if (Event.current.GetTypeForControl(controlID) == EventType.KeyUp) {
            if (Event.current.keyCode == KeyCode.LeftShift) {
                //shiftDown = false;
            }
        }
    }
}
#endif