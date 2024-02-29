using UnityEngine;

public abstract class AccRandComponent : MonoBehaviour{
    /// <summary>
    /// Apply randomization to this accessory
    /// </summary>
    /// <param name="rarityValue">how rare is this from 0 - 1 based on rarity curve</param>
    /// <param name="seed">random seed based on instance id</param>
    /// <param name="randomValue">the raw random 0 - 1 value not using the rarity curve</param>
    public abstract void Apply(float rarityValue, int seed, float randomValue);
}
