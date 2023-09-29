using UnityEngine;

[CreateAssetMenu(fileName = "MaterialColorToolData", menuName = "Airship/Create Material Color Tool Data")]
public class MaterialColorToolData : ScriptableObject {
    public Texture2D toolIcon;
    public Texture2D cursorIcon;
    public Material[] standardMaterials;
}