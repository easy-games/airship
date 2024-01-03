using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class LeafMaker : EditorWindow
{
    [MenuItem("Airship/Misc/Leaf Maker")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(LeafMaker));
    }

    private int subdivision = 3; // Default value
    private float size = 1f; // Default value
    private GameObject previewObject; // GameObject to display the mesh in the scene
    private float sphericalness = 0.3f;
    private float positionNoiseScale = 0.2f;
    private float rotationNoiseScale = 0.5f;
    private float quadSize = 0.6f;

    private float roundNormals = 0.5f;

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Leaf Maker", EditorStyles.boldLabel);
        

        subdivision = EditorGUILayout.IntField("Subdivision", subdivision);
        size = EditorGUILayout.FloatField("Size", size);

        //Add a slider 0..1 for sphericalness
        sphericalness = EditorGUILayout.Slider("Sphericalness", sphericalness, 0f, 1f);

        //Add sliders for noise
        positionNoiseScale = EditorGUILayout.Slider("Position Noise", positionNoiseScale, 0f, 2f);
        rotationNoiseScale = EditorGUILayout.Slider("Rotation Noise", rotationNoiseScale, 0f, 2f);

        //Quadsize
        quadSize = EditorGUILayout.Slider("Quad Size", quadSize, 0f, 1f);

        //Rounded Normals
        roundNormals = EditorGUILayout.Slider("Rounded Normals", roundNormals, 0f, 1f);

        //Save Prefab Button
        if (GUILayout.Button("Save Prefab"))
        {
            SavePrefab();
        }

        //If anything changes, generate a new cube
        if (GUI.changed)
        {
            GenerateCubeMesh();
        }
        
    }

    void SavePrefab()
    {
        if (previewObject == null)
        {
            return;

        }

        //prompt for a location
        string prefabPath = EditorUtility.SaveFilePanelInProject("Save Leaf Prefab", "Leaf", "prefab", "Please enter a name and location to save the leaf.", "D:\\EasyGG\\bedwars - airship\\Assets\\Bundles\\Shared\\Resources\\VoxelWorld\\Meshes\\Tilesets");

        if (prefabPath != "")
        {
            //Create an asset for the mesh
            Mesh mesh = previewObject.GetComponent<MeshFilter>().sharedMesh;

            //Grab the prefabPath filename without ext
            string ext = Path.GetExtension(prefabPath);

            //Delete the ext off the end
            string pathWithoutExt = prefabPath.Substring(0, prefabPath.Length - ext.Length);

            AssetDatabase.CreateAsset(mesh, pathWithoutExt + ".asset");

            Mesh loadedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(pathWithoutExt + ".asset");

            //Create a brand new gameobject with the mesh and meshfilter to save as the prefab
            GameObject newObject = new GameObject();
            newObject.AddComponent<MeshFilter>().sharedMesh = loadedMesh;
            newObject.AddComponent<MeshRenderer>().sharedMaterial = previewObject.GetComponent<MeshRenderer>().sharedMaterial;
                        
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(newObject, prefabPath);

            GameObject.DestroyImmediate(newObject);
            //Assign the current material to it

            //save the prefab again
            //PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }

    }


    //On open generate cube mesh
    private void OnEnable()
    {
        GenerateCubeMesh();
    }


    class Triangle
    {
        public Vertex vertex1;
        public Vertex vertex2;
        public Vertex vertex3;

        public Triangle(Vertex v1, Vertex v2, Vertex v3)
        {
            vertex1 = v1;
            vertex2 = v2;
            vertex3 = v3;

            vertex1.barycentrics = new Vector2(0, 1);
            vertex2.barycentrics = new Vector2(1, 0);
            vertex3.barycentrics = new Vector2(0, 0);
        }
    }
    
    struct Vertex
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 uv;
        public Vector2 barycentrics;
        public Color32 color;
        public Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            this.position = position;
            this.normal = normal;
            this.uv = uv;
            this.barycentrics = Vector2.zero;
            this.color = Color.white;
                
        }
        
        
    }

    void ProcessVertex(Vertex vertex, Vector3 position, Dictionary<Vector3, List<int>> dict, List<Vertex> vertexList)
    {
        Vector3 roundedPos = new Vector3(
            Mathf.Round(position.x * 1000f) / 1000f,
            Mathf.Round(position.y * 1000f) / 1000f,
            Mathf.Round(position.z * 1000f) / 1000f
        );

        if (!dict.ContainsKey(roundedPos))
        {
            dict[roundedPos] = new List<int>();
        }

        vertexList.Add(vertex);
        dict[roundedPos].Add(vertexList.Count - 1);
    }

    void SmoothNormals(List<Triangle> triangleList)
    {
        Dictionary<Vector3, List<int>> sharedVertices = new Dictionary<Vector3, List<int>>();
        List<Vertex> vertexList = new List<Vertex>();

        foreach (var triangle in triangleList)
        {
            ProcessVertex(triangle.vertex1, triangle.vertex1.position, sharedVertices, vertexList);
            ProcessVertex(triangle.vertex2, triangle.vertex2.position, sharedVertices, vertexList);
            ProcessVertex(triangle.vertex3, triangle.vertex3.position, sharedVertices, vertexList);
        }

        foreach (var pair in sharedVertices)
        {
            Vector3 averageNormal = Vector3.zero;

            foreach (var index in pair.Value)
            {
                averageNormal += vertexList[index].normal;
            }

            averageNormal.Normalize();

            foreach (var index in pair.Value)
            {
                Vertex v = vertexList[index];
                v.normal = averageNormal;
                vertexList[index] = v;
            }
        }

        // Return the updated vertices to the triangle list
        int i = 0;
        foreach (var triangle in triangleList)
        {
            triangle.vertex1 = vertexList[i++];
            triangle.vertex2 = vertexList[i++];
            triangle.vertex3 = vertexList[i++];
        }
    }

    List<Triangle> CreateQuads(List<Triangle> input)
    {
        Dictionary<Vector3, List<int>> sharedVertices = new Dictionary<Vector3, List<int>>();
        List<Vertex> vertexList = new List<Vertex>();

        foreach (var triangle in input)
        {
            ProcessVertex(triangle.vertex1, triangle.vertex1.position, sharedVertices, vertexList);
            ProcessVertex(triangle.vertex2, triangle.vertex2.position, sharedVertices, vertexList);
            ProcessVertex(triangle.vertex3, triangle.vertex3.position, sharedVertices, vertexList);
        }

        //Create a quad aligned to each normal in vertexList
        List<Triangle> output = new List<Triangle>();
        
        foreach (var ver in sharedVertices)
        {
            Vertex v = vertexList[ver.Value[0]];

            Vector3 normal = v.normal;
            //peturb the normal a bit by randomness
            normal += Random.insideUnitSphere * rotationNoiseScale;


            //Offset the v.position by the normal plus some randomness
            Vector3 offset = normal * Random.Range(0, positionNoiseScale);
            Vector3 pos = v.position + offset;

            //create a quad and rotate it to face v.normal
            
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            Quaternion rotation = Quaternion.LookRotation(normal, bitangent);

            //Roll them randomly
            float roll = Random.Range(0f, 360f);
            rotation = rotation * Quaternion.Euler(0f, 0f, roll);
            

            //Create a quad by creating two triangles
            Vector3[] quadVertices = new Vector3[4];
            quadVertices[0] = pos + rotation * new Vector3(-0.5f, -0.5f, 0f) * quadSize;
            quadVertices[1] = pos + rotation * new Vector3(0.5f, -0.5f, 0f) * quadSize;
            quadVertices[2] = pos + rotation * new Vector3(-0.5f, 0.5f, 0f) * quadSize;
            quadVertices[3] = pos + rotation * new Vector3(0.5f, 0.5f, 0f) * quadSize;

            Vector3[] newNormal = new Vector3[4];
            newNormal[0] = Vector3.Lerp(normal, quadVertices[0].normalized, roundNormals);
            newNormal[1] = Vector3.Lerp(normal, quadVertices[1].normalized, roundNormals);
            newNormal[2] = Vector3.Lerp(normal, quadVertices[2].normalized, roundNormals);
            newNormal[3] = Vector3.Lerp(normal, quadVertices[3].normalized, roundNormals);


            //Create two triangles
            Triangle t1 = new Triangle(
                new Vertex(quadVertices[0], newNormal[0], new Vector2(0, 0)),
                new Vertex(quadVertices[1], newNormal[1], new Vector2(1, 0)),
                new Vertex(quadVertices[2], newNormal[2], new Vector2(0, 1))
            );

            Triangle t2 = new Triangle(
                new Vertex(quadVertices[1], newNormal[1], new Vector2(1, 0)),
                new Vertex(quadVertices[3], newNormal[3], new Vector2(1, 1)),
                new Vertex(quadVertices[2], newNormal[2], new Vector2(0, 1))
            );
            output.Add(t1);
            output.Add(t2);

        }
        return output;
    }
    

    void GenerateCubeMesh()
    {
        float halfSize = size / 2f;
        float stepSize = size / (float)subdivision;

        List<Triangle> triangleList = new List<Triangle>();

        for (int face = 0; face < 6; face++)
        {
            Vector3 faceNormal = GetFaceNormal(face);

            for (int y = 0; y <= subdivision; y++)
            {
                for (int x = 0; x <= subdivision; x++)
                {
                    Vector3 vertexPosition = GetVertexPosition(face, x * stepSize - halfSize, y * stepSize - halfSize);
                    Vector2 uvCoord = new Vector2((float)x / subdivision, (float)y / subdivision);

                    Vertex currentVertex = new Vertex(vertexPosition, faceNormal, uvCoord);

                    if (x < subdivision && y < subdivision)
                    {
                        Vertex topRight = new Vertex(GetVertexPosition(face, (x + 1) * stepSize - halfSize, y * stepSize - halfSize), faceNormal, new Vector2((float)(x + 1) / subdivision, (float)y / subdivision));
                        Vertex bottomLeft = new Vertex(GetVertexPosition(face, x * stepSize - halfSize, (y + 1) * stepSize - halfSize), faceNormal, new Vector2((float)x / subdivision, (float)(y + 1) / subdivision));
                        Vertex bottomRight = new Vertex(GetVertexPosition(face, (x + 1) * stepSize - halfSize, (y + 1) * stepSize - halfSize), faceNormal, new Vector2((float)(x + 1) / subdivision, (float)(y + 1) / subdivision));

                        if (face == 0 || face == 3 || face == 5)
                        {
                            triangleList.Add(new Triangle(currentVertex, topRight, bottomLeft));
                            triangleList.Add(new Triangle(topRight, bottomRight, bottomLeft));
                        }
                        else
                        {
                            triangleList.Add(new Triangle(currentVertex, bottomLeft, topRight));
                            triangleList.Add(new Triangle(topRight, bottomLeft, bottomRight));
                        }
                    }
                }
            }
        }

        //deform the cube into a sphere by 30%
        foreach (Triangle triangle in triangleList)
        {
            triangle.vertex1.position = Vector3.Lerp(triangle.vertex1.position, triangle.vertex1.position.normalized * size, sphericalness);
            triangle.vertex2.position = Vector3.Lerp(triangle.vertex2.position, triangle.vertex2.position.normalized * size, sphericalness);
            triangle.vertex3.position = Vector3.Lerp(triangle.vertex3.position, triangle.vertex3.position.normalized * size, sphericalness);

            //Blend the normals too
            triangle.vertex1.normal = Vector3.Lerp(triangle.vertex1.normal, triangle.vertex1.position.normalized, sphericalness).normalized;
            triangle.vertex2.normal = Vector3.Lerp(triangle.vertex2.normal, triangle.vertex2.position.normalized, sphericalness).normalized;
            triangle.vertex3.normal = Vector3.Lerp(triangle.vertex3.normal, triangle.vertex3.position.normalized, sphericalness).normalized;
        }

      

        //SmoothShade the triangles by searching for shared positions etc etc
        SmoothNormals(triangleList);

        //Create a quad aligned to each unique vertex in the triangle list
        triangleList = CreateQuads(triangleList);

        


        // Convert the triangle list into vertex and index arrays for the mesh
        List<Vertex> vertexList = new List<Vertex>();

        List<int> indexList = new List<int>();

        foreach (var triangle in triangleList)
        {
            vertexList.Add(triangle.vertex1);
            vertexList.Add(triangle.vertex2);
            vertexList.Add(triangle.vertex3);
            
            indexList.Add(vertexList.Count - 3);
            indexList.Add(vertexList.Count - 2);
            indexList.Add(vertexList.Count - 1);
        }

        Vector3[] vertices = vertexList.Select(v => v.position).ToArray();
        Vector3[] normals = vertexList.Select(v => v.normal).ToArray();
        Vector2[] uv = vertexList.Select(v => v.uv).ToArray();
      //  Vector3[] colors = vertexList.Select(v => v.color).ToArray();
        //Vector2[] barycentrics = vertexList.Select(v => v.barycentrics).ToArray();
        //Vector2[] distances = vertexList.Select(v => v.distances).ToArray();
        int[] triangles = indexList.ToArray();

        

        //Create a Airship material
        Material material = new Material(Shader.Find("Airship/Construction"));
        
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.normals = normals;
        //mesh.colors = colors;
        mesh.uv = uv;
        //mesh.uv2 = barycentrics;
        //mesh.uv2 = distances;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.name = "CubeMesh";
                
        if (previewObject == null)
        {
            //See if theres already a previewObject in the scene?
            previewObject = GameObject.Find("Cube Preview");
            if (previewObject == null)
            {
                previewObject = new GameObject("Cube Preview");
               // previewObject.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                previewObject.AddComponent<MeshFilter>();
                previewObject.AddComponent<MeshRenderer>();
                //Set the material
                previewObject.GetComponent<MeshRenderer>().material = material;
            }
        }

        previewObject.GetComponent<MeshFilter>().mesh = mesh;

       
    }

    Vector3 GetVertexPosition(int face, float x, float y)
    {
        Vector3 vertex;

        switch (face)
        {
            case 0: vertex = new Vector3(x, y, size / 2f); break;
            case 1: vertex = new Vector3(x, y, -size / 2f); break;
            case 2: vertex = new Vector3(size / 2f, y, x); break;
            case 3: vertex = new Vector3(-size / 2f, y, x); break;
            case 4: vertex = new Vector3(x, size / 2f, y); break;
            case 5: vertex = new Vector3(x, -size / 2f, y); break;
            default: vertex = Vector3.zero; break;
        }

        return vertex;
    }

    Vector3 GetFaceNormal(int face)
    {
        switch (face)
        {
            case 0: return Vector3.forward;
            case 1: return Vector3.back;
            case 2: return Vector3.right;
            case 3: return Vector3.left;
            case 4: return Vector3.up;
            case 5: return Vector3.down;
            default: return Vector3.zero;
        }
    }
}
