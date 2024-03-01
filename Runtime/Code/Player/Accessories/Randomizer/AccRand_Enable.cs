using UnityEngine;

public class AccRand_Enable : AccRandComponent
{
    public GameObject[] targetGos;
    public float enableThreshold = .5f;
    public bool flip = false;

    public override void Apply(float rarityValue, int seed, float randomValue){
        bool enabled = flip? rarityValue <= enableThreshold : rarityValue >= enableThreshold;
        foreach(var targetGo in targetGos){
            targetGo.SetActive(enabled);
        }
    }
}
