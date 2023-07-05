using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicVariables : MonoBehaviour {
    public string key => gameObject.name;
    public KeyValueReference<string>[] strings;
    public KeyValueReference<float>[] numbers;
    public KeyValueReference<Vector3>[] vectors;

    public string GetString(string key) {
        string foundValue = "";
        GetValue(key, strings, ref foundValue);
        return foundValue;
    }

    public float GetNumber(string key) {
        float foundValue = 0;
        GetValue(key, numbers, ref foundValue);
        return foundValue;
    }

    public Vector3 GetVector(string key) {
        Vector3 foundValue = Vector3.zero;
        GetValue(key, vectors, ref foundValue);
        return foundValue;
    }

    public bool GetValue<T>(string key, KeyValueReference<T>[] values, ref T foundValue){
        foreach (var kvp in values) {
            if (kvp.key == key) {
                foundValue = kvp.value;
                return true;
            }
        }
        return false;
    }
}


