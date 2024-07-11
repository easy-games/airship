using System.IO;
using UnityEditor;
using UnityEngine;

public class PlayerConfig : ScriptableObject
{
    public int playerVersion = 100;
    
    private void OnValidate()
    {
        File.WriteAllText(".player_version", playerVersion + "\n");
    }
}
