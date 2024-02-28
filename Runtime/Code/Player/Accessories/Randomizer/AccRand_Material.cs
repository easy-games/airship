using System;
using System.Collections.Generic;
using UnityEngine;

public class AccRand_Material : AccRandComponent {
    public Renderer renderer;
    public int[] materialIndexes;
    public Material[] possibleMaterials;
    
    public override void Apply(float rarityValue, int seed, float randomValue)    {
        int newIndex = Mathf.FloorToInt(rarityValue * possibleMaterials.Length);
       // Debug.Log(rarityValue + " Material index: " + newIndex);
        var newValue = possibleMaterials[Mathf.Clamp(newIndex, 0, possibleMaterials.Length-1)];

        var oldMaterials = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;

        List<Material> mats = new List<Material>();
        for (int i = 0; i < oldMaterials.Length; i++){
            bool overrideMat = false;
            foreach(var index in materialIndexes){
                if(i == index){
                    overrideMat = true;
                    break;
                }
            }
            if(overrideMat){
                mats.Add(newValue);
            }else{
                mats.Add(oldMaterials[i]);
            }
        }

        if(Application.isPlaying){
            renderer.SetMaterials(mats);
        }else{
            renderer.SetSharedMaterials(mats);
        }
    }
}
