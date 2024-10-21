using UnityEngine;

[LuauAPI]
[CreateAssetMenu(fileName = "New Skin Accessory", menuName = "Airship/Accessories/Entity Skin Accessory", order = 1)]
public class AccessorySkin : ScriptableObject {
    public Texture2D skinTextureDiffuse;
    public Texture2D skinTextureNormal;
    public Texture2D skinTextureORM;
    public Texture2D faceTextureDiffuse;

    public override string ToString() {
        return skinTextureDiffuse.name.Replace("_", " ");
    }
}
