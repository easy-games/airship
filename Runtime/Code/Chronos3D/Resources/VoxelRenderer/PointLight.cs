using UnityEngine;
using UnityEditor;

public class PointLight : MonoBehaviour
{
    public Color color = Color.white;
    [Range(0f, 4f)]
    public float intensity = 1f;
    [Range(0f, 64f)]
    public float range = 10f;
    public bool castShadows = true;
    public bool highQualityLight = true;

    
    

    private void OnDrawGizmos()
    {
        //Gizmos.color = color;
        //Gizmos.DrawWireSphere(transform.position, range);

        if (highQualityLight)
        {  
        
            Gizmos.DrawIcon(transform.position, "Airship/pointlightHQ.png", true, Color.yellow);
            if (castShadows)
            {
                Gizmos.DrawIcon(transform.position, "Airship/shadow.png", true, Color.yellow);
            }
        }
        else
        {
            
            Gizmos.DrawIcon(transform.position, "Airship/pointlight.png", true);
            if (castShadows)
            {
                Gizmos.DrawIcon(transform.position, "Airship/shadow.png", true);
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