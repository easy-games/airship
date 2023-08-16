using System;
using Code.Components.DynamicVariables;
using FishNet;
using UnityEngine;

[CreateAssetMenu(fileName = "New Dynamic Variables", menuName = "Airship/Dynamic Variables", order = 0)]
public class DynamicVariables : ScriptableObject {
    public string collectionId;
    public KeyValueReference<string>[] strings;
    public KeyValueReference<float>[] numbers;
    public KeyValueReference<Vector3>[] vectors;

    public string GetString(string key) {
        GetValue(key, strings, out var foundValue);
        return foundValue;
    }

    public void SetString(string key, string val)
    {
        this.SetValue(key, this.strings, val);
        this.ReplicateString(key);
    }

    public float GetNumber(string key) {
        GetValue(key, numbers, out var foundValue);
        return foundValue;
    }

    public void SetNumber(string key, float val)
    {
        this.SetValue(key, this.numbers, val);
        this.ReplicateNumber(key);
    }

    public Vector3 GetVector3(string key) {
        GetValue(key, vectors, out var foundValue);
        return foundValue;
    }

    public void SetVector3(string key, Vector3 val)
    {
        this.SetValue(key, this.vectors, val);
        this.ReplicateVector3(key);
    }

    private bool GetValue<T>(string key, KeyValueReference<T>[] values, out T foundValue){
        foreach (var kvp in values) {
            if (kvp.key == key) {
                foundValue = kvp.value;
                return true;
            }
        }

        foundValue = default(T);
        return false;
    }

    private void SetValue<T>(string key, KeyValueReference<T>[] values, T value){
        foreach (var kvp in values) {
            if (kvp.key == key)
            {
                kvp.value = value;
                break;
            }
        }
    }

    public void ReplicateString(string key)
    {
        if (!InstanceFinder.IsServer) return;

        this.GetValue(key, this.strings, out var value);

        var broadcast = new DynamicVariablesUpdateStringBroadcast()
        {
            collectionKey = this.collectionId,
            key = key,
            valueString = value
        };
        InstanceFinder.ServerManager.Broadcast(broadcast);
    }

    public void ReplicateNumber(string key)
    {
        if (!InstanceFinder.IsServer) return;

        this.GetValue(key, this.numbers, out var value);

        var broadcast = new DynamicVariablesUpdateNumberBroadcast()
        {
            collectionKey = this.collectionId,
            key = key,
            valueNumber = value
        };
        InstanceFinder.ServerManager.Broadcast(broadcast);
    }

    public void ReplicateVector3(string key)
    {
        if (!InstanceFinder.IsServer) return;

        this.GetValue(key, this.vectors, out var value);

        var broadcast = new DynamicVariablesUpdateVector3Broadcast()
        {
            collectionKey = this.collectionId,
            key = key,
            valueVector3 = value
        };
        InstanceFinder.ServerManager.Broadcast(broadcast);
    }

    public void ReplicateAll()
    {
        foreach (var kvp in this.numbers)
        {
            this.ReplicateNumber(kvp.key);
        }
        foreach (var kvp in this.strings)
        {
            this.ReplicateString(kvp.key);
        }
        foreach (var kvp in this.vectors)
        {
            this.ReplicateVector3(kvp.key);
        }
    }

    private void OnEnable() {
        Debug.Log("Registering vars " + this.collectionId);
        DynamicVariablesManager.Instance.RegisterVars(this.collectionId, this);
    }

    private void OnValidate()
    {
        DynamicVariablesManager.Instance.RegisterVars(this.collectionId, this);
        if (Application.isPlaying && InstanceFinder.IsServer)
        {
            ReplicateAll();
        }
    }
}


