#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AccessoryRandomizer))]
public class AccessoryRandomizerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if(GUILayout.Button("Test Generate")){
            var randomizer = target as AccessoryRandomizer;
            randomizer.Apply(GetRandomString());
            Debug.Log("Generated Random Value: " + randomizer.value);
        }
    }

    private string GetRandomString(){
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var stringChars = new char[16];
        var random = new System.Random();

        for (int i = 0; i < stringChars.Length; i++)
        {
            stringChars[i] = chars[random.Next(chars.Length)];
        }

        return new System.String(stringChars);
    }
}
#endif