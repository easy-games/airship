using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[FilePath("Assets/Editor/AuthConfigData.confg", FilePathAttribute.Location.ProjectFolder)]
public class AuthConfig : ScriptableSingleton<AuthConfig>
{

    [SerializeField]
    public string deployKey;

    [SerializeField] public string stagingApiKey;

    public void Modify()
    {
        Save(true);
    }
}