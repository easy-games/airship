using UnityEngine;

public class CustomAccSetter_Color : MonoBehaviour {
    public ColorGroup[] colorGroups;

    public void SetColor(int index, Color newColor) {
        colorGroups[index].SetColor(newColor);
    }
}

[System.Serializable]
public class ColorGroup {
    public int colorIndex = -1;
    public MaterialColorURP[] matColor;

    public void SetColor(Color newColor) {
        foreach (var matColor in matColor) {
            if (colorIndex < 0) {
                matColor.SetColorOnAll(newColor);
            } else {
                matColor.SetColor(colorIndex, newColor);
            }
        }
    }
}