using UnityEngine;
using System;

public class AccessoryCustomization : MonoBehaviour {
    public AccCustomValue<Color>[] customColors = new AccCustomValue<Color>[0] { };
    public AccCustomValue<Texture2D>[] customTextures = new AccCustomValue<Texture2D>[0] { };

    public bool SetColor(int index, Color color) {
        if (index < 0 || index >= customColors.Length) {
            return false;
        }
        this.SetColorInternal(customColors[index], color);
        return true;
    }

    public bool SetColor(string key, Color color) {
        var custom = SetValueInternal(key, color, customColors); 
        if(custom != null){
            SetColorInternal(custom, color);
            return true;
        }
        return false;
    }

    private void SetColorInternal(AccCustomValue<Color> custom, Color color) {
        custom.currentValue = color;
        custom.ren.gameObject.GetComponent<MaterialColorURP>()?.SetColorOnAll(color);
    }

    private AccCustomValue<T> SetValueInternal<T>(string key, T newValue, AccCustomValue<T>[] array) {
        foreach (var item in array) {
            if (item.key == key) {
                return item;
            }
        }
        return null;
    }
}

[Serializable]
public class AccCustomValue<T> {
    public string key = "Primary";
    public T defaultValue;
    [HideInInspector]
    public T currentValue;

    public Renderer ren;
}
