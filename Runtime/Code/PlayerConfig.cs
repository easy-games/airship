using System.IO;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerConfig", menuName = "EasyGG/PlayerConfig", order = 1)]
public class PlayerConfig : ScriptableObject
{
    public int playerVersion = 100;
    
    private void OnValidate()
    {
        File.WriteAllText(".player_version", playerVersion + "\n");
    }
}
