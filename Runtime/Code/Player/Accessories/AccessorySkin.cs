using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Skin Accessory", menuName = "Airship/Accessories/Entity Skin Accessory", order = 1)]
public class AccessorySkin : ScriptableObject
{
    public string DisplayName;
    public Texture2D skinTextureDiffuse;
    public Texture2D skinTextureNormal;
    public Texture2D skinTextureORM;
}
