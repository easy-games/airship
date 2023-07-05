using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "EasyEditorConfig", menuName = "EasyGG/EasyEditorConfig", order = 0)]
public class EasyEditorConfig : ScriptableObject
{
    public bool useBundlesInEditor = false;
}