using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Windows;

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

}
 
public class PointLightUtility {
  
    public static PointLightDto BuildDto(Light input) {
        
        return new PointLightDto() {
            color = input.color,
            position = input.transform.position,
            rotation = input.transform.rotation,
            intensity = input.intensity,
            range = input.range,
            castShadows = false,
        };
    }
    
}
