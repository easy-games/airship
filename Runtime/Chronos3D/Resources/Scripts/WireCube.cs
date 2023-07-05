using UnityEngine;

[ExecuteInEditMode]
public class WireCube : MonoBehaviour
{
    public Color color = Color.white;
    [Range(0f, 1f)]
    public float alpha = 1f;

    private Material material;
    private Mesh mesh;
    private Vector3 previousScale;
    private float previousAlpha;
    private Color previousColor;


    void OnEnable()
    {
        mesh = new Mesh();
        mesh.name = "Wire Cube";
        previousScale = transform.localScale;
        previousColor = color;
        previousAlpha = alpha;
        material = Resources.Load<Material>("WireFrame");
        BuildWireCube();
    }

    void Update()
    {
        if (previousScale != transform.localScale)
        {
            BuildWireCube();
            previousScale = transform.localScale;
        }
        //if color or alpha has changed, rebuild the mesh
        if (previousColor != color || previousAlpha != alpha)
        {
            BuildWireCube();
            previousColor = color;
            previousAlpha = alpha;
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
}
