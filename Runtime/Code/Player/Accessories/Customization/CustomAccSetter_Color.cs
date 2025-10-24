using UnityEngine;
using UnityEngine.Serialization;

public class CustomAccSetter_Color : MonoBehaviour {
    public ColorGroup[] colorGroups;

    public void SetColor(string key, Color newColor) {
        foreach (var group in colorGroups) {
            if (group.key == key) {
                group.SetColor(newColor);
            }
        }
    }
}

[System.Serializable]
public class ColorGroup {
    public string key = "Color";

    [Tooltip("Index for MaterialColorURP component")]
    public int materialColorIndex = -1;

    public MaterialColorURP[] matColor;

    public void SetColor(Color newColor) {
        foreach (var matColor in matColor) {
            if (materialColorIndex < 0) {
                matColor.SetColorOnAll(newColor);
            } else {
                matColor.SetColor(materialColorIndex, newColor);
            }
        }
    }
}