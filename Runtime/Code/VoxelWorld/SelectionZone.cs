using System;
using UnityEngine;

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
public class SelectionZoneEditor : Editor {
    private const float handleSize = 0.1f;
    private const float snapValue = 1.0f;

    void Awake() {

        //Add a handler for the gizmo refresh event
        SceneView.duringSceneGui += GizmoRefreshEvent;
    }
    
    void GizmoRefreshEvent(SceneView obj) {
        SelectionZone cube = (SelectionZone)target;

        // Define handle positions based on the cube's size
        Vector3[] handles = new Vector3[]
        {
            cube.transform.position + new Vector3(cube.size.x / 2, 0, 0), // Right
            cube.transform.position + new Vector3(-cube.size.x / 2, 0, 0), // Left
            cube.transform.position + new Vector3(0, cube.size.y / 2, 0), // Top
            cube.transform.position + new Vector3(0, -cube.size.y / 2, 0), // Bottom
            cube.transform.position + new Vector3(0, 0, cube.size.z / 2), // Front
            cube.transform.position + new Vector3(0, 0, -cube.size.z / 2) // Back
        };

        EditorGUI.BeginChangeCheck();

        // Move handles with constraints
        for (int i = 0; i < handles.Length; i++) {
            Vector3 axis = Vector3.zero;
            bool isNegativeHandle = false;
            switch (i) {
                case 0: axis = Vector3.right; break;
                case 1: axis = Vector3.right; isNegativeHandle = true; break;
                case 2: axis = Vector3.up; break;
                case 3: axis = Vector3.up; isNegativeHandle = true; break;
                case 4: axis = Vector3.forward; break;
                case 5: axis = Vector3.forward; isNegativeHandle = true; break;
            }

            // Draw spheres as handles and constrain movement
            Vector3 newPos = Handles.Slider(handles[i], axis, handleSize, Handles.SphereHandleCap, 0);

            // Calculate the snapped movement
            float movement = Mathf.Floor((newPos - handles[i]).magnitude / snapValue) * snapValue * Mathf.Sign(Vector3.Dot(newPos - handles[i], axis));
            
            float resize = movement;

            if (movement != 0) {
                // Invert the movement for negative handles
                if (isNegativeHandle) {
                    resize = -resize;
                }

                switch (i) {
                    case 0:
                    case 1:
                    cube.size.x += resize;
                    cube.transform.position += axis * (movement / 2);
                    break;
                    case 2:
                    case 3:
                    cube.size.y += resize;
                    cube.transform.position += axis * (movement / 2);
                    break;
                    case 4:
                    case 5:
                    cube.size.z += resize;
                    cube.transform.position += axis * (movement / 2);
                    break;
                }
            }
        }

        if (EditorGUI.EndChangeCheck()) {
            Undo.RecordObject(cube, "Resize Cube");
            cube.size = new Vector3(Mathf.Max(1, Mathf.Round(cube.size.x)), Mathf.Max(1, Mathf.Round(cube.size.y)), Mathf.Max(1, Mathf.Round(cube.size.z)));

            //Snap the position
            //cube.transform.position = new Vector3(Mathf.Floor(cube.transform.position.x), Mathf.Floor(cube.transform.position.y), Mathf.Floor(cube.transform.position.z));

            //Snap each axis of the position separately, depending on if that size is odd or even
            float x = Mathf.Floor(cube.transform.position.x);
            float y = Mathf.Floor(cube.transform.position.y);
            float z = Mathf.Floor(cube.transform.position.z);

            if (Mathf.Round(cube.size.x) % 2 == 0) {
                x += 0.5f;
            }
            if (Mathf.Round(cube.size.y) % 2 == 0) {
                y += 0.5f;
            }
            if (Mathf.Round(cube.size.z) % 2 == 0) {
                z += 0.5f;
            }
            //Set it
            cube.transform.position = new Vector3(x, y, z);


            cube.BuildCube();
            EditorUtility.SetDirty(cube);
        }
    }
}
#endif

