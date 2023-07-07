#if UNITY_EDITOR
using Codice.Client.BaseCommands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelWorldStuff;
using Object = UnityEngine.Object;

[CustomEditor(typeof(VoxelWorld))]
public class VoxelWorldEditor : UnityEditor.Editor
{
 
    GameObject handle = null;
    GameObject raytraceHandle = null;
    bool raycastDebugMode = false;

    public void Load(VoxelWorld world)
    {
        if (world.voxelWorldFile != null)
        {
            world.LoadWorldFromVoxelBinaryFile(world.voxelWorldFile, world.blockDefines);
        }
    }

    public override void OnInspectorGUI()
    {
        VoxelWorld world = (VoxelWorld)target;
        
        if (GUILayout.Button("Generate Full World"))
        {
            world.GenerateWorld(true);
        }
        if (GUILayout.Button("Generate Empty World"))
        {
            world.GenerateWorld(false);
        }
        if (GUILayout.Button("Save As"))
        {
                
            var gameObjects = world.GetChildGameObjects();
            world.mapObjects.Clear();
            world.pointlights.Clear();
            
            foreach (var go in gameObjects) {
                if (!go.name.Equals("Chunk") && !go.name.Equals("Cube") && !go.name.Equals("Pointlight")) {
                    world.mapObjects.Add(go.name, go.transform);
                }

                if (go.name.Equals("Pointlight")) {
                    world.pointlights.Add(go);
                }
            }
                
            VoxelBinaryFile saveFile = CreateInstance<VoxelBinaryFile>();
            saveFile.CreateFromVoxelWorld(world);

            //Create a file picker to save the file, prepopulate it with the asset path of world.asset
            string path = EditorUtility.SaveFilePanel("Save Voxel World", "Assets/Bundles/Server/Resources/Worlds", "VoxelWorld", "asset");
            string relativePath = "Assets/" + path.Split("Assets")[1];
            AssetDatabase.CreateAsset(saveFile, relativePath);
            world.UpdatePropertiesForAllChunksForRendering();
        }
        if (world.voxelWorldFile != null)
        {
            
        }

        if (GUILayout.Button("Clear GameObjects!"))
        {
            VoxelWorld.DeleteChildGameObjects(world.gameObject);
        }
        //Add big divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(3) });

        //Add a file picker for  voxelWorldFile
        world.voxelWorldFile = (VoxelBinaryFile)EditorGUILayout.ObjectField("Voxel World File", world.voxelWorldFile, typeof(VoxelBinaryFile), false);

        //Add a file picker for the world.blockDefines textAsset
        world.blockDefines = (TextAsset)EditorGUILayout.ObjectField("Block Defines", world.blockDefines, typeof(TextAsset), false);

        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(3) });

        if (world.voxelWorldFile != null)
        {
            if (GUILayout.Button("Load"))
            {
                Load(world);
            }

            if (GUILayout.Button("Save")) {

                var gameObjects = world.GetChildGameObjects();
                world.mapObjects.Clear();
                foreach (var go in gameObjects) {
                    if (!go.name.Equals("Chunk") && !go.name.Equals("Cube") && !go.name.Equals("Pointlight")) {
                        world.mapObjects.Add(go.name, go.transform);
                    }
                    
                    if (go.name.Equals("Pointlight")) {
                        world.pointlights.Add(go);
                    }
                }
                
                VoxelBinaryFile saveFile = CreateInstance<VoxelBinaryFile>();
                saveFile.CreateFromVoxelWorld(world);

                //Get path of the asset world.voxelWorldFile
                //string path = AssetDatabase.GetAssetPath(world.voxelWorldFile);
                string path = "Assets/Bundles/Server/Resources/Worlds/" + world.voxelWorldFile.name + ".asset";
                AssetDatabase.CreateAsset(saveFile, path);
                world.voxelWorldFile = saveFile;
                Debug.Log("Saved file " + world.voxelWorldFile.name);
                world.UpdatePropertiesForAllChunksForRendering();
            }

        }
 

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
        
        //Make a toggle for raycast debug mode
        raycastDebugMode = EditorGUILayout.Toggle("Raycast Debug Mode", raycastDebugMode);


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


    private void OnSceneGUI()
    {

        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        VoxelWorld world = (VoxelWorld)target;
        Event e = Event.current;


        if (e.type == EventType.MouseMove)
        {

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
            }
            else
            {

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
                        ren.sharedMaterial = Resources.Load<Material>("Selection");

                    }
                    //handle.transform.position = pos + new Vector3(0.5f, 0.5f, 0.5f); //;//+  VoxelWorld.FloorInt(pos)+ new Vector3(0.5f,0.5f,0.5f);
                    // Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                    Vector3 pos = hitPosition + (normal * 0.1f);
                    handle.transform.position = VoxelWorld.FloorInt(pos) + new Vector3(0.5f, 0.5f, 0.5f);
                    //Debug.Log("Mouse on cell" + VoxelWorld.FloorInt(pos));

                }
            }
        }

        if (e.type == EventType.MouseUp && e.button == 0) //Leftclick up
        {
            // Create a ray from the mouse position
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);
            if (res)
            {
                Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                if (Event.current.shift)
                {
                    //set voxel to 0
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos);
                    world.WriteVoxelAtInternal(voxelPos, 0);
                    world.DirtyNeighborMeshes(voxelPos, true);
#if UNITY_EDITOR
                   // world.FullWorldUpdate();
#endif
                }
                else
                {
                    
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos) + VoxelWorld.FloorInt(normal);
                    world.WriteVoxelAtInternal(voxelPos, (byte)world.selectedBlockIndex);
                    world.DirtyNeighborMeshes(voxelPos, true);
#if UNITY_EDITOR
                 //   world.FullWorldUpdate();
#endif
                }
            }

        }

        if (Event.current.GetTypeForControl(controlID) == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.LeftShift)
            {
                //shiftDown = true;
            }
        }
        if (Event.current.GetTypeForControl(controlID) == EventType.KeyUp)
        {
            if (Event.current.keyCode == KeyCode.LeftShift)
            {
                //shiftDown = false;
            }
        }

    }
}


public class EditorWindowScript : EditorWindow
{
    // Enum to represent the different modes
    enum Mode
    {
        Add,
        Delete,
    }

    int gridSize = 10;
    bool[,] grid;

    // The current mode
    Mode currentMode;

    [MenuItem("Chronos/VoxelEditor")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:

        if (HasOpenInstances<EditorWindowScript>())
        {
            GetWindow<EditorWindowScript>().Close();
        }
        else
        {
            var myWindow = GetWindow<EditorWindowScript>();
            myWindow.titleContent = new GUIContent("Voxel Editor");
        }
    }

    VoxelWorld GetVoxelWorld()
    {
        GameObject go = GameObject.Find("VoxelWorld");
        if (go == null)
        {
            return null;
        }
        // if (go == null)
        // {
        //     go = new GameObject("VoxelWorld");
        //     go.AddComponent<VoxelWorld>();
        // }
        return go.GetComponent<VoxelWorld>();
    }

    VoxelBlocks.BlockDefinition GetBlock(byte index)
    {
        VoxelWorld world = GetVoxelWorld();
        if (world == null)
        {
            return null;
        }
        return world.blocks.GetBlock(index);
    }

    void OnGUI()
    {
        VoxelWorld world = GetVoxelWorld();
        if (world == null)
        {
            return;
        }

        // Initialize the grid array if it hasn't been initialized yet
        if (grid == null)
        {
            grid = new bool[gridSize, gridSize];
            int idx = world.selectedBlockIndex;
            grid[idx % 10, idx / 10] = true;
        }


        // Calculate the size of each square in the grid based on the size of the editor window
        float squareSize = position.width / gridSize;

        int index = 0;
        // Use a nested loop to create a grid of buttons
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {

                // Calculate the position and size of the button
                Rect buttonRect = new Rect(x * squareSize, y * squareSize, squareSize, squareSize);

                // Use the GUI.color property to change the color of the button based on its state (selected or not selected)
                GUI.color = grid[x, y] ? Color.green : Color.white;


                VoxelBlocks.BlockDefinition block = GetBlock((byte)index);

                if (block != null)
                {
                    if (GUI.Button(buttonRect, block.editorTexture))
                    {
                        // Toggle the state of the button when it's clicked
                        for (int yy = 0; yy < gridSize; yy++)
                        {
                            for (int xx = 0; xx < gridSize; xx++)
                            {
                                grid[xx, yy] = false;

                            }
                        }
                        world.selectedBlockIndex = block.index;
                        grid[x, y] = true;
                    }
                }
                else
                {
                    string name = "Air";
                    if (index > 0)
                    {
                        name = "";
                    }


                    if (GUI.Button(buttonRect, name))
                    {

                        // Toggle the state of the button when it's clicked
                        for (int yy = 0; yy < gridSize; yy++)
                        {
                            for (int xx = 0; xx < gridSize; xx++)
                            {
                                grid[xx, yy] = false;

                            }
                        }
                        world.selectedBlockIndex = 0;

                        grid[x, y] = true;
                    }
                }


                index += 1;
            }
        }


    }



}





#endif