using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EntityFaceData", menuName = "Airship/Accessories/Create Face Data", order = 2)]
public class EntityFaceData : ScriptableObject {
    public Sprite blinkEye;
    public Sprite[] eyes;
    public Sprite[] pupils;
    public Sprite[] noses;
    public Sprite[] mouths;

    public Sprite GetEye(int index) {
        return eyes[index >= 0 && index < eyes.Length ? index : 0];
    }
}
