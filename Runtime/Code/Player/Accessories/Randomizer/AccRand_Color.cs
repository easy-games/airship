using UnityEngine;

public class AccRand_Color : AccRandComponent{
    public MaterialColor[] materialColors;
    public int[] materialIndexes;
    public Gradient possibleColors;
    
    public override void Apply(float rarityValue, int seed, float randomValue)
    {
        Color newColor = possibleColors.Evaluate(rarityValue);
        foreach(var mat in materialColors){
            foreach(var index in materialIndexes){
                mat.SetMaterialColor(index, newColor);
            }
        }
    }
}
