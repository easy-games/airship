using System.Collections.Generic;
using UnityEngine;

public class CustomAccSetter_Color : MonoBehaviour {
    public Renderer[] colorMaskRens;
    public ColorGroup[] colorGroups;

    private Dictionary<string, Color> currentColors = new Dictionary<string, Color>();

    public void SetColor(string key, Color newColor) {
        currentColors[key] = newColor;
        SetColorInternal(key, newColor);
    }
    
    private void SetColorInternal(string key, Color newColor) {
        int groupI = 0;
        foreach (var group in colorGroups) {
            if (group.key == key) {
                group.SetColor(newColor);
                if (groupI < 3) {
                    foreach (var ren in colorMaskRens) {
                        ren.material.SetColor(GetColorKey(groupI), newColor);
                    }
                }
            }

            groupI++;
        }
    }

    public void Refresh() {
        foreach (var kvp in currentColors) {
            SetColorInternal(kvp.Key, kvp.Value);
        }
    }

    private string GetColorKey(int index) {
        switch (index) {
            case 0:
                return "_BaseColor";
            case 1:
                return "_BaseColor2";
            case 2:
                return "_BaseColor3";
            default:
                return "_BaseColor";
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