using UnityEngine;
using UnityEditor;

public struct PointLightDto {
    public Color color;
    public Vector3 position;
    public Quaternion rotation;
    public float intensity;
    public float range;
    public bool castShadows;
    public bool highQualityLight;
}

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

    public PointLightDto BuildDto() {
        var t = this.transform;
        return new PointLightDto() {
            color = this.color,
            position = t.position,
            rotation = t.rotation,
            intensity = this.intensity,
            range = this.range,
            castShadows = this.castShadows,
            highQualityLight = this.highQualityLight,
        };
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