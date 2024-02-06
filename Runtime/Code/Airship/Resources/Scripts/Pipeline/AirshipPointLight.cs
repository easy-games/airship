using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public struct PointLightDto {
    public Color color;
    public Vector3 position;
    public Quaternion rotation;
    public float intensity;
    public float range;
    public bool castShadows;
    public bool highQualityLight;
}

public class AirshipPointLight : MonoBehaviour
{
    public Color color = Color.white;
    [Range(0f, 4f)]
    public float intensity = 1f;
    [Range(0f, 64f)]
    public float range = 4f;
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

    private void Awake()
    {
        RegisterLight();
    }

    private void OnEnable()
    {
        RegisterLight();
        
    }
    
    private void OnDisable()
    {
        UnregisterLight();
    }
 
 

#if(UNITY_EDITOR)
    private void OnValidate()
    {
        RegisterLight();
    }
#endif

    private void OnDestroy()
    {
    
        UnregisterLight();
    }
    
    private void RegisterLight()
    {
        var manager = Airship.SingletonClassManager<AirshipPointLight>.Instance;
        manager.RegisterItem(this);
    }

    private void UnregisterLight()
    {
        var manager = Airship.SingletonClassManager<AirshipPointLight>.Instance;
        manager.UnregisterItem(this);
    }

    public static List<AirshipPointLight> GetAllPointLights()
    {
        var manager = Airship.SingletonClassManager<AirshipPointLight>.Instance;
        return manager.GetAllActiveItems();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AirshipPointLight)), CanEditMultipleObjects]
public class PointLightEditor : Editor
{
    public void OnSceneGUI()
    {
        AirshipPointLight t = (target as AirshipPointLight);

     
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