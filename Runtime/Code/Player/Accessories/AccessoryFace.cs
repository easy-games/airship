using UnityEngine;

[CreateAssetMenu(fileName = "FaceAccessory", menuName = "Airship/Accessories/Face Accessory")]
public class AccessoryFace : ScriptableObject
{
    public string serverClassId;

    [HideInInspector] public string serverInstanceId;
    
    public Texture2D decalTexture;

}
