public class AccRandGroup : AccRandComponent
{
    public AccRandComponent[] groupedComponents;

    public override void Apply(float rarityValue, int seed, float randomValue)    {
        foreach(var rand in groupedComponents){
            rand.Apply(rarityValue, seed, randomValue);
        }
    }
}
