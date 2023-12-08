using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[ExecuteInEditMode]
public class EasyPrimitive_Trapezoid : MonoBehaviour {
    public EngineRunMode rebuildMode = EngineRunMode.EDITOR;
    
    [Header("References")]
    public MeshFilter meshFilter;
    
    [Header("Variables")]
    
    [Range(0f, 1f)]
    public float trapWidth = .5f;
    [Range(0f, 1f)]
    public float trapDepth = .5f;

    public Color topColor = Color.white;
    public Color bottomColor = Color.white;
    
    private Vector2 builtSize = new Vector2(0, 0);
    private Color builtTopColor = Color.white;
    private Color builtBottomColor = Color.white;
    
    // Update is called once per frame
    void Update()
    {
        if (rebuildMode == EngineRunMode.NONE) {
            return;
        }
        if (Application.isPlaying && rebuildMode == EngineRunMode.EDITOR) {
            return;
        }

        if (!builtSize.x.Equals(trapWidth) || !builtSize.y.Equals(trapDepth) ||
            builtTopColor != topColor || builtBottomColor != bottomColor) {
            Rebuild();
        }
    }

    public void Rebuild() {
        if (!meshFilter) {
            Debug.LogWarning("Please provide a mesh filter for Easy Trapezoid");
            return;
        }

        meshFilter.sharedMesh = new Mesh();
        GenerateMesh(meshFilter.sharedMesh);
        
        var collider = meshFilter.gameObject.GetComponent<MeshCollider>();
        if (collider) {
            collider.sharedMesh = meshFilter.sharedMesh;
        }
    }

    private void GenerateMesh(Mesh meshRef) {
        Vector3[] topVerts = new[] {
            new Vector3(-trapWidth, 1, -trapDepth),
            new Vector3(trapWidth, 1, -trapDepth),
            new Vector3(trapWidth, 1, trapDepth),
            new Vector3(-trapWidth, 1, trapDepth),
        };
        var bottomSize = .5f;
        Vector3[] bottomVerts = new[] {
            new Vector3(-bottomSize, 0, -bottomSize),
            new Vector3(bottomSize, 0, -bottomSize),
            new Vector3(bottomSize, 0, bottomSize),
            new Vector3(-bottomSize, 0, bottomSize),
        };

        MeshData meshData = new MeshData();
        meshData.rampColorA = bottomColor;
        meshData.rampColorB = topColor;

        // Generate Top Quad
        AddQuad(meshData, topVerts, Vector3.up);

        // Generate Bottom Quad
        AddQuad(meshData, bottomVerts, Vector3.down, true);

        // Generate Side Quads
        AddQuad(meshData, new Vector3[] { bottomVerts[0], topVerts[0], topVerts[3], bottomVerts[3] }, Vector3.left); // Left
        AddQuad(meshData, new Vector3[] { topVerts[1], bottomVerts[1], bottomVerts[2], topVerts[2] }, Vector3.right); // Right
        AddQuad(meshData, new Vector3[] { topVerts[3], topVerts[2], bottomVerts[2], bottomVerts[3] }, Vector3.forward); // Front
        AddQuad(meshData, new Vector3[] { bottomVerts[0], bottomVerts[1], topVerts[1], topVerts[0] }, Vector3.back); // Back

        meshRef.vertices = meshData.vertices.ToArray();
        meshRef.colors = meshData.colors.ToArray();
        meshRef.uv = meshData.uvs.ToArray();
        meshRef.normals = meshData.normals.ToArray();
        meshRef.triangles = meshData.tris.ToArray();

        builtSize.x = trapWidth;
        builtSize.y = trapDepth;
        builtTopColor = topColor;
        builtBottomColor = bottomColor;
    }

    private void AddQuad(MeshData meshData, Vector3[] verts, Vector3 normal, bool flip= false) {
        int vertCount = meshData.vertices.Count;
        foreach (var vert in verts) {
            meshData.AddVert(vert, normal, Vector2.zero); // Adjust UVs as needed
        }

        if (!flip) {
            meshData.AddTri(vertCount, vertCount + 2, vertCount + 1);
            meshData.AddTri(vertCount, vertCount + 3, vertCount + 2);
        } else {
            meshData.AddTri(vertCount, vertCount + 1, vertCount + 2);
            meshData.AddTri(vertCount, vertCount + 2, vertCount + 3);
        }
    }

    public class MeshData {
        public List<Vector3> vertices = new ();
        public List<int> tris = new ();
        public List<Vector3> normals = new ();
        public List<Vector2> uvs = new ();
        public List<Color> colors = new ();
        public Color rampColorA;
        public Color rampColorB;

        public void AddVert(Vector3 position, Vector3 normal, Vector2 uv) {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            colors.Add(Color.Lerp(rampColorA, rampColorB, position.y));
            print("posY: " + position.y);
        }

        public void AddTri(int a, int b, int c) {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }
    }

    private class Vert {
        public Vector3 position;
        public Vector3 normals;
        public Vector2 uvs;
        public Color color;

        public Vert(Vector3 position, Vector3 normals, Vector2 uvs, Color color) {
            this.position = position;
            this.normals = normals;
            this.uvs = uvs;
            this.color = color;
        }
    }
}
