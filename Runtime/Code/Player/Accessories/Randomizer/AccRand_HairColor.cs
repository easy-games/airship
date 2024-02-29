using UnityEngine;

public class AccRand_HairColor : AccRandComponent{
    public Renderer renderer;
    public int[] materialIndexes;
    public Gradient possibleColorsA;
    public Gradient possibleColorsB;
    public Gradient possibleColorsC;
    
    public override void Apply(float rarityValue, int seed, float randomValue)
    {
        Color newColorA = possibleColorsA.Evaluate(rarityValue);
        Color newColorB = possibleColorsB.Evaluate(rarityValue);
        Color newColorC = possibleColorsC.Evaluate(rarityValue);
        var renRef = AirshipRendererManager.Instance.GetRendererReference(renderer);
        renRef.EnableEngineShaderVariants();
        var mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
        foreach(var index in materialIndexes){
            var block = renRef.GetPropertyBlock(mats[index], index);
            block.SetColor("_ColorTop", newColorA);
            block.SetColor("_ColorMid", newColorB);
            block.SetColor("_ColorBot", newColorC);
        }
    }
}
