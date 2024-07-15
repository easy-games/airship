using UnityEngine;

public class AccRand_Color : AccRandComponent{
    public MaterialColorURP[] materialColors;
    public int[] materialIndexes = {0};
    public Gradient possibleColors;
    public bool SetEmmisive = false;
    
    public override void Apply(float rarityValue, int seed, float randomValue)
    {
        // Color newColor = possibleColors.Evaluate(rarityValue);
        // foreach(var mat in materialColors){
        //     if(!mat){
        //         continue;
        //     }
        //     foreach(var index in materialIndexes){
        //         var color = mat.GetColorSettings(index);
        //         color.materialColor = newColor;
        //         if(SetEmmisive){
        //             color.emissiveColor = newColor;
        //         }
        //         mat.SetColorSettings(index, color);
        //     }
        // }
    }
}
