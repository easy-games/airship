using UnityEngine;

[LuauAPI]
[CreateAssetMenu(fileName = "FaceAccessory", menuName = "Airship/Accessories/Face Accessory")]
public class AccessoryFace : ScriptableObject {
    [HideFromTS]
    public string serverClassId;

    [HideFromTS]
    public string serverClassIdStaging;

    [HideInInspector] public string serverInstanceId;
    
    public Texture2D decalTexture;

    public string GetServerClassId() {
#if AIRSHIP_STAGING
        return this.serverClassIdStaging;
#else
        return this.serverClassId;
#endif
    }
}
