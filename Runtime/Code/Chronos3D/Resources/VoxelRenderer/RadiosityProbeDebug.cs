using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoxelWorldStuff;

public class RadiosityProbeDebug : MonoBehaviour
{

    public List<RadiosityProbeSample> samples = new();
    public RadiosityProbe parent = null;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(RadiosityProbeDebug))]
public class RadiosityProbeDebugEditor : Editor
{

 
    public void Load()
    {
        RadiosityProbeDebug world = (RadiosityProbeDebug)target;
       
    }

    public override void OnInspectorGUI()
    {
        RadiosityProbeDebug probe = (RadiosityProbeDebug)target;

        if (GUILayout.Button("Set Debug Color"))
        {

            probe.parent.debugging = true;
            
        }

        if (GUILayout.Button("Show Samples"))
        {
            //clear all the children objects off of the probe
            foreach (Transform child in probe.transform)
            {
                DestroyImmediate(child.gameObject);
            }

            foreach (RadiosityProbeSample sample in probe.samples)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = sample.position;
                sphere.transform.localScale = Vector3.one * 0.1f;
                sphere.transform.parent = probe.transform;

                Renderer renderer = sphere.GetComponent<Renderer>();
                renderer.sharedMaterial = new Material(Resources.Load<Material>("DebugSphere"));
                renderer.sharedMaterial.SetColor("_Color", sample.color);
            }

            
        }

        GUILayout.Label("Num samples: " + probe.samples.Count);
    }
}
#endif