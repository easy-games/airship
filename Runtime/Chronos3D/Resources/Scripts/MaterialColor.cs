using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class MaterialColor : MonoBehaviour
{
    // The color to apply to the vertex colors

    //Use the HDR color picker
    [ColorUsage(true, true)]
    public Color materialColor = Color.white;
    [ColorUsage(true, true)]
    public Color emissiveColor = Color.white;

    //Float slider from 0..1 for emissive blend
    [Range(0, 1)]
    public float emissiveMix = 1.0f;

    //Turn emissive on even if there is no map
    //Unity bug: can't do this, because its a shader variant and property blocks dont roll that way
    //public bool forceEmissive = false;
    
    MaterialPropertyBlock[] propertyBlocks;
    

    private void Start()
    {
        DoUpdate();
    }

    // Called when the color is changed in the inspector
    private void OnValidate()
    {
        DoUpdate();
    }

    public void DoUpdate()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (propertyBlocks == null || propertyBlocks.Length != renderers.Length) 
        {
            propertyBlocks = new MaterialPropertyBlock[renderers.Length];
            //allocate them
            for (int i = 0; i < propertyBlocks.Length; i++)
            {
                propertyBlocks[i] = new MaterialPropertyBlock();
            }
        }
                
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].GetPropertyBlock(propertyBlocks[i]);
        }

        //Set the _Color property
        for (int i = 0; i < propertyBlocks.Length; i++)
        {
            propertyBlocks[i].SetColor("_Color", materialColor);
            propertyBlocks[i].SetColor("_EmissiveColor", emissiveColor);
            propertyBlocks[i].SetFloat("_EmissiveMix", emissiveMix);
            //propertyBlocks[i].SetFloat("EMISSIVE", forceEmissive ? 1 : 0);
            renderers[i].SetPropertyBlock(propertyBlocks[i]);
        }
    }

}