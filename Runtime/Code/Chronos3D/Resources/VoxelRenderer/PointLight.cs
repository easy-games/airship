using UnityEngine;
using UnityEditor;

public class PointLight : MonoBehaviour
{
    public Color color = Color.white;
    public float intensity = 1f;
    [Range(0f, 100f)]
    public float range = 10f;
    public bool castShadows = true;
    public bool highQualityLight = true;


    private void OnDrawGizmos()
    {
        //Gizmos.color = color;
        //Gizmos.DrawWireSphere(transform.position, range);

        if (highQualityLight)
        {
        
            Gizmos.DrawIcon(transform.position, "pointlightHQ.png", true, Color.yellow);
            if (castShadows)
            {
                Gizmos.DrawIcon(transform.position, "shadow.png", true, Color.yellow);
            }
        }
        else
        {
            
            Gizmos.DrawIcon(transform.position, "pointlight.png", true);
            if (castShadows)
            {
                Gizmos.DrawIcon(transform.position, "shadow.png", true);
            }
        }
       
    }

   

    private void Start()
    {
      
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PointLight)), CanEditMultipleObjects]
public class PointLightEditor : Editor
{
    public void OnSceneGUI()
    {
        PointLight t = (target as PointLight);

     
        Handles.color = Color.white;
        if (t.highQualityLight)
        {
            Handles.color = Color.yellow;
        }
        float areaOfEffect = Handles.RadiusHandle(Quaternion.identity, t.transform.position, t.range);
         
        
        if (GUI.changed)
        {
             
            t.range = areaOfEffect;
           

        }

        
    }
}
#endif