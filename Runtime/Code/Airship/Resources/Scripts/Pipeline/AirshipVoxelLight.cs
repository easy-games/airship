using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

 
public class AirshipVoxelLight : MonoBehaviour
{
    public Color color = Color.white;
    [Range(0f, 4f)]
    public float intensity = 1f;
    [Range(0f, 64f)]
    public float range = 4f;
    public bool castShadows = true;

    private void OnDrawGizmos()
    {
        Gizmos.DrawIcon(transform.position, "Airship/pointlight.png", true);
        if (castShadows)
        {
            Gizmos.DrawIcon(transform.position, "Airship/shadow.png", true);
        }
        
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
        var manager = Airship.SingletonClassManager<AirshipVoxelLight>.Instance;
        manager.RegisterItem(this);
    }

    private void UnregisterLight()
    {
        var manager = Airship.SingletonClassManager<AirshipVoxelLight>.Instance;
        manager.UnregisterItem(this);
    }

    public static List<AirshipVoxelLight> GetAllVoxelLights()
    {
        var manager = Airship.SingletonClassManager<AirshipVoxelLight>.Instance;
        return manager.GetAllActiveItems();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AirshipVoxelLight)), CanEditMultipleObjects]
public class VoxelLightEditor : Editor
{
    public void OnSceneGUI()
    {
        AirshipVoxelLight t = (target as AirshipVoxelLight);

     
        Handles.color = Color.white;
 
        float areaOfEffect = Handles.RadiusHandle(Quaternion.identity, t.transform.position, t.range);
         
        if (GUI.changed)
        {
            t.range = areaOfEffect;
        }
    }
}
#endif