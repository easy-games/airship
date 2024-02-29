using System;
using UnityEngine;

public class AccessoryRandomizer : MonoBehaviour{
    public AnimationCurve rarityCurve = AnimationCurve.Linear(0,0,1,1);
    public AccRandComponent[] randomComponents;

    protected float rarityValue = 0;
    protected int seed = 0;
    protected float randomValue = 0;

    public float value => rarityValue;

    public void Apply(string randomSeed){
        if(randomComponents.Length <= 0){
            randomComponents = gameObject.GetComponents<AccRandComponent>();
        }
        seed =randomSeed.GetHashCode();
        //print("SEED: " + seed);
        UnityEngine.Random.InitState(seed); 
        randomValue = UnityEngine.Random.Range(0,1f);
        rarityValue = rarityCurve.Evaluate(randomValue);
        foreach(var ran in randomComponents){
            ran.Apply(rarityValue, seed, randomValue);
        }
    }
}
