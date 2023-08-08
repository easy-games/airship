using System.Collections.Generic;
using Code.GameBundle;

[System.Serializable]
public class GameConfigDto {
    public string gameId;
    public List<AirshipPackageDocument> packages;
}