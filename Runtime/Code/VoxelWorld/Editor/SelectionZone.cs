using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SelectionZone : MonoBehaviour
{
    public Color color = Color.white;
    [Range(0f, 1f)]
    public float alpha = 1f;

    [Range(0f, 3f)]
    public float thickness = 0.02f;

    public Vector3 size = Vector3.one;

    [NonSerialized]
    private Material material;

    [NonSerialized]
    private Mesh mesh;

    [NonSerialized]
    private Vector3 previousScale;
    [NonSerialized]
    private float previousAlpha;
    [NonSerialized]
    private Color previousColor;
    [NonSerialized]
    private float previousThickness;

    [NonSerialized]
    public VoxelWorld voxelWorld;

    void OnEnable()
    {
        mesh = new Mesh();
        mesh.name = "Wire Cube";
        previousScale = transform.localScale;
        previousColor = color;
        previousAlpha = alpha;
        previousThickness = thickness;
        material = Resources.Load<Material>("WireFrame");
        BuildCube();
    }

    void Update()
    {
        //if anything has changed
        if (previousScale != transform.localScale || mesh == null || previousColor != color || previousAlpha != alpha || previousThickness != thickness)
        {
            BuildCube();
            previousColor = color;
            previousAlpha = alpha;
            previousScale = transform.localScale;
            previousThickness = thickness;
        }
    }

    public void BuildCube()
    {
        if (thickness > 0)
        {
            BuildPrismCube();
        }
        else
        {
            BuildWireCube();
        }
    }

    void BuildWireCube()
    {
        float size = Mathf.Min(transform.localScale.x, transform.localScale.y, transform.localScale.z);
        Vector3 halfSize = new Vector3(size * 0.5f, size * 0.5f, size * 0.5f);

        Vector3[] vertices = new Vector3[8]
        {
            new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
            new Vector3(halfSize.x, halfSize.y, -halfSize.z),
            new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
            new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
            new Vector3(-halfSize.x, halfSize.y, halfSize.z),
            new Vector3(halfSize.x, halfSize.y, halfSize.z),
            new Vector3(halfSize.x, -halfSize.y, halfSize.z)
        };

        int[] indices = new int[]
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        Color[] colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color(color.r, color.g, color.b, alpha);
        }

        if (gameObject.GetComponent<MeshFilter>() == null)
        {
            gameObject.AddComponent<MeshFilter>();
        }

        if (gameObject.GetComponent<MeshRenderer>() == null)
        {
            gameObject.AddComponent<MeshRenderer>();
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.colors = colors;

        gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;

        if (material != null)
        {
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    void BuildPrismCube()
    {
        //float size = Mathf.Min(transform.localScale.x, transform.localScale.y, transform.localScale.z);
        Vector3 halfSize = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f);

        // 12 prisms, 10 verts each, 16 tris each
        Vector3[] vertices = new Vector3[10 * 12];
        int[] indices = new int[16 * 12 * 3];

        int vertexCount = 0;
        int indexCount = 0;

        // Top face edges
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(-halfSize.x, halfSize.y, halfSize.z), new Vector3(halfSize.x, halfSize.y, halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(halfSize.x, halfSize.y, halfSize.z), new Vector3(halfSize.x, halfSize.y, -halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(halfSize.x, halfSize.y, -halfSize.z), new Vector3(-halfSize.x, halfSize.y, -halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(-halfSize.x, halfSize.y, -halfSize.z), new Vector3(-halfSize.x, halfSize.y, halfSize.z));

        // Bottom face edges
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(-halfSize.x, -halfSize.y, halfSize.z), new Vector3(halfSize.x, -halfSize.y, halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(halfSize.x, -halfSize.y, halfSize.z), new Vector3(halfSize.x, -halfSize.y, -halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(halfSize.x, -halfSize.y, -halfSize.z), new Vector3(-halfSize.x, -halfSize.y, -halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), new Vector3(-halfSize.x, -halfSize.y, halfSize.z));

        // Vertical edges
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(-halfSize.x, -halfSize.y, halfSize.z), new Vector3(-halfSize.x, halfSize.y, halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(halfSize.x, -halfSize.y, halfSize.z), new Vector3(halfSize.x, halfSize.y, halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(halfSize.x, -halfSize.y, -halfSize.z), new Vector3(halfSize.x, halfSize.y, -halfSize.z));
        AddSegmenent(vertices, indices, ref vertexCount, ref indexCount, new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), new Vector3(-halfSize.x, halfSize.y, -halfSize.z));


        Color[] colors = new Color[vertices.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color(color.r, color.g, color.b, alpha);
        }

        Mesh mesh = new Mesh();
        mesh.name = "Wire Cube";
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.colors = colors;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        if (material != null)
        {
            meshRenderer.sharedMaterial = material;
        }
    }

    void AddTriangle(int[] indices, ref int indicesCount, int a, int b, int c)
    {
        indices[indicesCount++] = a;
        indices[indicesCount++] = b;
        indices[indicesCount++] = c;
    }

    void AddSegmenent(Vector3[] vertices, int[] indices, ref int vertexCount, ref int indexCount, Vector3 start, Vector3 end)
    {

        Vector3 direction = start - end;

        Vector3 normal = direction.normalized;

        //Cap points are end +normal   and start - normal
        Vector3 capPoint1 = end - normal * thickness;
        Vector3 capPoint2 = start + normal * thickness;

        //We need two vectors perpendicular to the start - end line to place our body points
        Vector3 perp1 = Vector3.Cross(direction, Vector3.up).normalized;
        Vector3 perp2 = Vector3.Cross(direction, perp1).normalized;

        //Fix them if up is too close
        if (perp1.magnitude < 0.1f)
        {
            perp1 = Vector3.Cross(direction, Vector3.right).normalized;
            perp2 = Vector3.Cross(direction, perp1).normalized;
        }

        //Each endcap is 5 points, capPoint, and start + 4 points around the circle defined by +/- perp1 and perp2
        int startIndex = vertexCount;

        vertices[vertexCount++] = capPoint2;
        vertices[vertexCount++] = capPoint1;

        vertices[vertexCount++] = start + perp1 * thickness;
        vertices[vertexCount++] = start + perp2 * thickness;
        vertices[vertexCount++] = start - perp1 * thickness;
        vertices[vertexCount++] = start - perp2 * thickness;

        vertices[vertexCount++] = end + perp1 * thickness;
        vertices[vertexCount++] = end + perp2 * thickness;
        vertices[vertexCount++] = end - perp1 * thickness;
        vertices[vertexCount++] = end - perp2 * thickness;

        //Add 4 triangles to make each cap
        AddTriangle(indices, ref indexCount, startIndex, startIndex + 2, startIndex + 3);
        AddTriangle(indices, ref indexCount, startIndex, startIndex + 3, startIndex + 4);
        AddTriangle(indices, ref indexCount, startIndex, startIndex + 4, startIndex + 5);
        AddTriangle(indices, ref indexCount, startIndex, startIndex + 5, startIndex + 2);

        AddTriangle(indices, ref indexCount, startIndex + 1, startIndex + 7, startIndex + 6);
        AddTriangle(indices, ref indexCount, startIndex + 1, startIndex + 8, startIndex + 7);
        AddTriangle(indices, ref indexCount, startIndex + 1, startIndex + 9, startIndex + 8);
        AddTriangle(indices, ref indexCount, startIndex + 1, startIndex + 6, startIndex + 9);

        //Add 8 triangles to make the body
        AddTriangle(indices, ref indexCount, startIndex + 2, startIndex + 6, startIndex + 3);
        AddTriangle(indices, ref indexCount, startIndex + 3, startIndex + 6, startIndex + 7);
        AddTriangle(indices, ref indexCount, startIndex + 3, startIndex + 7, startIndex + 4);
        AddTriangle(indices, ref indexCount, startIndex + 4, startIndex + 7, startIndex + 8);
        AddTriangle(indices, ref indexCount, startIndex + 4, startIndex + 8, startIndex + 5);
        AddTriangle(indices, ref indexCount, startIndex + 5, startIndex + 8, startIndex + 9);
        AddTriangle(indices, ref indexCount, startIndex + 5, startIndex + 9, startIndex + 2);
        AddTriangle(indices, ref indexCount, startIndex + 2, startIndex + 9, startIndex + 6);
    }
}


#if UNITY_EDITOR
 
[CustomEditor(typeof(SelectionZone))]
public class SelectionZoneEditor : UnityEditor.Editor {
    private const float handleSize = 0.3f;
 
    private bool mouseDown = false;

    static bool haveCopiedData = false;
    static UInt16[,,] copiedData;
    static Vector3Int copiedSize;
    
    // Define local handle positions based on the cube's size
    Vector3[] localHandleVectors = new Vector3[6] {
           new Vector3(1, 0, 0), // Right
            new Vector3(-1, 0, 0), // Left
            new Vector3(0, 1, 0), // Top
            new Vector3(0, -1, 0), // Bottom
            new Vector3(0, 0, 1), // Front
            new Vector3(0, 0, -1) // Back
    };

    Color[] axisColors = new Color[6] {
        Color.red,
        Color.red,
        Color.green,
        Color.green,
        Color.blue,
        Color.blue
    };

    float[] handleOffset = new float[6] {
        1,1,1,1,1,1
    };

    float[] trueHandleOffset = new float[6] {
        1,1,1,1,1,1
    };
 
    //Gui
    public override void OnInspectorGUI() {
        //draw default
        //DrawDefaultInspector();

        //Add typeins for size x y and z
        SelectionZone cube = (SelectionZone)target;
        Vector3Int oldSize = new Vector3Int((int)cube.size.x, (int)cube.size.y, (int)cube.size.z);
        Vector3Int newSize = EditorGUILayout.Vector3IntField("Size", oldSize);

        GUI.enabled = true;

        if (newSize != oldSize) {
            cube.size = newSize;
            SnapToGrid();
            cube.BuildCube();
            ResetHandles();
        }
         
        //Draw a reset button
        if (GUILayout.Button("Reset")) {
            handleOffset = new float[6] {
              1,1,1,1,1,1
            };
            trueHandleOffset = new float[6] {
              1,1,1,1,1,1
            };
            
            cube.size = new Vector3(1, 1, 1);
            cube.BuildCube();
        }
        if (cube.voxelWorld == null) {
            return;
        }
        if (cube.voxelWorld.voxelBlocks == null) {
            return;
        }
        VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

        //Add Copy Button
        if (GUILayout.Button("Fill")) {
            //walk the bounds 
            float dx = cube.size.x / 2;
            float dy = cube.size.y / 2;
            float dz = cube.size.z / 2;
            float px = cube.transform.localPosition.x;
            float py = cube.transform.localPosition.y;
            float pz = cube.transform.localPosition.z;
                        
            if (cube.voxelWorld) {

                List<VoxelEditAction.EditInfo> edits = new();

                int selectedIndex = cube.voxelWorld.selectedBlockIndex;
                //Walk the current selection zone
                for (int x = Mathf.FloorToInt(px - dx); x < Mathf.CeilToInt(px + dx); x++) {
                    for (int y = Mathf.FloorToInt(py - dy); y < Mathf.CeilToInt(py + dy); y++) {
                        for (int z = Mathf.FloorToInt(pz - dz); z < Mathf.CeilToInt(pz + dz); z++) {
                            UInt16 prevData = cube.voxelWorld.ReadVoxelAt(new Vector3Int(x, y, z));
                            edits.Add(new VoxelEditAction.EditInfo(new Vector3Int(x, y, z), prevData, (UInt16)selectedIndex));
                        }
                    }
                }
                voxelEditManager.AddEdits(cube.voxelWorld, edits, "Fill Voxels");
            }
        }

        if (GUILayout.Button("Replace")) {
            //walk the bounds
            float dx = cube.size.x / 2;
            float dy = cube.size.y / 2;
            float dz = cube.size.z / 2;
            float px = cube.transform.localPosition.x;
            float py = cube.transform.localPosition.y;
            float pz = cube.transform.localPosition.z;

            if (cube.voxelWorld) {

                List<VoxelEditAction.EditInfo> edits = new();

                int selectedIndex = cube.voxelWorld.selectedBlockIndex;
                //Walk the current selection zone
                for (int x = Mathf.FloorToInt(px - dx); x < Mathf.CeilToInt(px + dx); x++) {
                    for (int y = Mathf.FloorToInt(py - dy); y < Mathf.CeilToInt(py + dy); y++) {
                        for (int z = Mathf.FloorToInt(pz - dz); z < Mathf.CeilToInt(pz + dz); z++) {
                            UInt16 prevData = cube.voxelWorld.ReadVoxelAt(new Vector3Int(x, y, z));
                            if (prevData != 0) {
                                edits.Add(new VoxelEditAction.EditInfo(new Vector3Int(x, y, z), prevData, (UInt16)selectedIndex));
                            }
                        }
                    }
                }
                voxelEditManager.AddEdits(cube.voxelWorld, edits, "Replace Voxels");
            }
        }

        void Copy(bool cut) {
            //walk the bounds
            float dx = cube.size.x / 2;
            float dy = cube.size.y / 2;
            float dz = cube.size.z / 2;
            float px = cube.transform.localPosition.x;
            float py = cube.transform.localPosition.y;
            float pz = cube.transform.localPosition.z;

            haveCopiedData = true;
            copiedSize = new Vector3Int((int)cube.size.x, (int)cube.size.y, (int)cube.size.z);

            copiedData = new UInt16[(int)cube.size.x , (int)cube.size.y , (int)cube.size.z];

            if (cube.voxelWorld) {

                int index = 0;
                //Walk the current selection zone
                for (int x = Mathf.FloorToInt(px - dx); x < Mathf.CeilToInt(px + dx); x++) {
                    for (int y = Mathf.FloorToInt(py - dy); y < Mathf.CeilToInt(py + dy); y++) {
                        for (int z = Mathf.FloorToInt(pz - dz); z < Mathf.CeilToInt(pz + dz); z++) {

                            int xx = x - Mathf.FloorToInt(px - dx);
                            int yy = y - Mathf.FloorToInt(py - dy);
                            int zz = z - Mathf.FloorToInt(pz - dz);

                            ushort data = cube.voxelWorld.ReadVoxelAt(new Vector3Int(x, y, z));
                            copiedData[xx, yy, zz] = data;
                            if (cut) {
                                List<VoxelEditAction.EditInfo> edits = new();
                                edits.Add(new VoxelEditAction.EditInfo(new Vector3Int(x, y, z), data, 0));
                                voxelEditManager.AddEdits(cube.voxelWorld, edits, "Cut Voxels");
                            }
                        }
                    }
                }

            }
        }

        if (GUILayout.Button("Copy")) {
            Copy(false);
        }

        if (GUILayout.Button("Cut")) {
            Copy(true);
        }

        if (haveCopiedData == false) {
            //Disable ui
            GUI.enabled = false;
            //Make fake paste button
            if (GUILayout.Button("Paste")) {
                //Do nothing
            }

            if (GUILayout.Button("Paste (Ignore Air)")) {
                // Do nothing
            }

            GUI.enabled = true;
        } else {

            void Paste(bool ignoreAir) {
                //walk the bouns
                float dx = copiedSize.x / 2;
                float dy = copiedSize.y / 2;
                float dz = copiedSize.z / 2;
                float px = cube.transform.localPosition.x;
                float py = cube.transform.localPosition.y;
                float pz = cube.transform.localPosition.z;

                if (cube.voxelWorld) {
                    List<VoxelEditAction.EditInfo> edits = new();

                    //Walk the current selection zone
                    for (int x = Mathf.FloorToInt(px - dx); x < Mathf.CeilToInt(px + dx); x++) {
                        for (int y = Mathf.FloorToInt(py - dy); y < Mathf.CeilToInt(py + dy); y++) {
                            for (int z = Mathf.FloorToInt(pz - dz); z < Mathf.CeilToInt(pz + dz); z++) {
                                //cube.voxelWorld.WriteVoxelAt(new Vector3Int(x, y, z), copiedData[index++], false);
                                UInt16 prevData = cube.voxelWorld.ReadVoxelAt(new Vector3Int(x, y, z));

                                int xx = x - Mathf.FloorToInt(px - dx);
                                int yy = y - Mathf.FloorToInt(py - dy);
                                int zz = z - Mathf.FloorToInt(pz - dz);
                                var newData = copiedData[xx, yy, zz];

                                if (ignoreAir) {
                                    var newBlockId = VoxelWorld.VoxelDataToBlockId(newData);
                                    if (newBlockId == 0) {
                                        continue;
                                    }
                                }

                                edits.Add(new VoxelEditAction.EditInfo(new Vector3Int(x, y, z), prevData, newData));
                            }
                        }
                    }
                    voxelEditManager.AddEdits(cube.voxelWorld, edits, "Paste Voxels");
                }

                //resize the box to whatever we pasted
                cube.size = new Vector3(copiedSize.x, copiedSize.y, copiedSize.z);
                SnapToGrid();
                cube.BuildCube();
                ResetHandles();
            }

            //Actual paste
            if (GUILayout.Button("Paste")) {
                Paste(false);
            }

            if (GUILayout.Button("Paste (Ignore Air)")) {
                Paste(true);
            }
        }


        if (haveCopiedData == false) {
            //Disable ui
            GUI.enabled = false;
            //Make fake paste button
            if (GUILayout.Button("Rotate Selection 90")) {
                //Do nothing
            }

            GUI.enabled = true;
        }
        else {
            //Actual paste
            if (GUILayout.Button("Rotate Selection 90")) {
                

                if (cube.voxelWorld) {
                    // Initialize newCopiedData with the correct size
               
                    Vector3Int newCopiedSize = new Vector3Int(copiedSize.z, copiedSize.y, copiedSize.x);
                    UInt16[,,] newCopiedData = new UInt16[newCopiedSize.x, newCopiedSize.y, newCopiedSize.z];

                    // Rotate voxel data by 90 degrees around Y-axis
                    for (int x = 0; x < copiedSize.x; x++) {
                        for (int y = 0; y < copiedSize.y; y++) {
                            for (int z = 0; z < copiedSize.z; z++) {
                              
                                int newX = z;
                                int newY = y;
                                int newZ = (newCopiedSize.z - 1) - x;

                                newCopiedData[newX,newY,newZ] = copiedData[x,y,z];
                            }
                        }
                    }

                    // Update the copiedData to the newly rotated data
                    copiedData = newCopiedData;

                    // Rotate the copiedSize to reflect the new dimensions
                    copiedSize = newCopiedSize;

                    // Resize the cube and rebuild it
                    cube.size = new Vector3(newCopiedSize.x, newCopiedSize.y, newCopiedSize.z);

                    SnapToGrid();
                    cube.BuildCube();
                    ResetHandles();

                }


            }
        }
    }

    void Awake() {
        // Add a handler for the gizmo refresh event
        SceneView.duringSceneGui += GizmoRefreshEvent;

        SnapToGrid();
    }

    private void ResetHandles() {
        SelectionZone cube = (SelectionZone)target;
        trueHandleOffset[0] = (cube.size.x / 2) + 0.5f;
        trueHandleOffset[1] = (cube.size.x / 2) + 0.5f;
        trueHandleOffset[2] = (cube.size.y / 2) + 0.5f;
        trueHandleOffset[3] = (cube.size.y / 2) + 0.5f;
        trueHandleOffset[4] = (cube.size.z / 2) + 0.5f;
        trueHandleOffset[5] = (cube.size.z / 2) + 0.5f;
        for (int j = 0; j < 6; j++) {
            handleOffset[j] = trueHandleOffset[j];
        }
    }
    private void OnDestroy() {

        SceneView.duringSceneGui -= GizmoRefreshEvent;
    }

    public void SnapToGrid() {
        SelectionZone cube = (SelectionZone)target;
        Transform cubeTransform = cube.transform;

        // Snap the position to the nearest 0.5 unit grid
        float x = Mathf.Floor(cubeTransform.localPosition.x);
        float y = Mathf.Floor(cubeTransform.localPosition.y);
        float z = Mathf.Floor(cubeTransform.localPosition.z);

        // Adjust snapping if the size is even
        if (Mathf.Round(cube.size.x) % 2 == 1) {
            x += 0.5f;
        }
        if (Mathf.Round(cube.size.y) % 2 == 1) {
            y += 0.5f;
        }
        if (Mathf.Round(cube.size.z) % 2 == 1) {
            z += 0.5f;
        }

        // Set the snapped position
        cubeTransform.localPosition = new Vector3(x, y, z);
        cubeTransform.localRotation = Quaternion.identity;
        cubeTransform.localScale = Vector3.one;
    }

    void GizmoRefreshEvent(SceneView obj) {
        SelectionZone cube = (SelectionZone)target;
        if (target == null) {
            return;
        }

        if (cube.transform.hasChanged) {
            //Debug.Log("Has changed");
            SnapToGrid();
            cube.transform.hasChanged = false;
            EditorUtility.SetDirty(target);
        }
        


        //capture mouse up and mouse down
        if (Event.current.type == EventType.MouseUp) {
            mouseDown = false;
           
        }
        if (Event.current.type == EventType.MouseDown) {
            mouseDown = true;
        }

        Transform cubeTransform = cube.transform;
        
        EditorGUI.BeginChangeCheck();

        Vector3 motion = Vector3.zero;

        // Move handles with constraints
        for (int i = 0; i < localHandleVectors.Length; i++) {
            Vector3 localHandleStartPos = localHandleVectors[i] * handleOffset[i];
            Vector3 worldHandleStartPos = cubeTransform.TransformPoint(localHandleStartPos);
            Vector3 axis = Vector3.zero;
            float isNegativeHandle = 1;

            switch (i) {
                case 0: axis = Vector3.right; break;
                case 1: axis = Vector3.right; isNegativeHandle = -1; break;
                case 2: axis = Vector3.up; break;
                case 3: axis = Vector3.up; isNegativeHandle = -1; break;
                case 4: axis = Vector3.forward; break;
                case 5: axis = Vector3.forward; isNegativeHandle = -1; break;
            }

         
            // Draw spheres as handles and constrain movement
            //Set the color
            Handles.color = axisColors[i];
            Vector3 newWorldPos = Handles.Slider(worldHandleStartPos, cubeTransform.TransformDirection(axis), handleSize, Handles.SphereHandleCap,0);
            
            Handles.color = Color.white;

            Vector3 localHandlePos = cubeTransform.InverseTransformPoint(newWorldPos);

            bool moved = false;
            if ((newWorldPos - worldHandleStartPos).magnitude > 0)  {
                moved = true;
            }

            if (mouseDown == false && Mathf.Abs(trueHandleOffset[i] - handleOffset[i]) > Mathf.Epsilon) {
                handleOffset[i] = trueHandleOffset[i]; //Reset it
            }
            
            if (moved == true) {
                
                float distance =  localHandlePos.magnitude - trueHandleOffset[i];
                float steps = Mathf.Floor(distance);

                handleOffset[i] = localHandlePos.magnitude;
                
                if (steps != 0) {
              
                    cube.size += steps * axis;
                    cube.transform.localPosition += (steps * axis / 2) * isNegativeHandle;
                    SnapToGrid();
                    
                    //Recalc handle pos
                    trueHandleOffset[0] = (cube.size.x / 2) + 0.5f; 
                    trueHandleOffset[1] = (cube.size.x / 2) + 0.5f; 
                    trueHandleOffset[2] = (cube.size.y / 2) + 0.5f; 
                    trueHandleOffset[3] = (cube.size.y / 2) + 0.5f; 
                    trueHandleOffset[4] = (cube.size.z / 2) + 0.5f; 
                    trueHandleOffset[5] = (cube.size.z / 2) + 0.5f;
                    for (int j = 0; j < 6; j++) {
                        //Reset all handles except the one being dragged
                        if (j==i) {
                            continue;
                        }
                        handleOffset[j] = trueHandleOffset[j];
                    }

                    handleOffset[i] -= (steps * 0.5f);

                }
                
            }
        }
        
        if (EditorGUI.EndChangeCheck()) {
     
            cube.BuildCube();
            EditorUtility.SetDirty(cube);
        }
    }
}
#endif

