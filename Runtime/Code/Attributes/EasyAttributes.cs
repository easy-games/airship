

using System;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public class EasyAttributes : NetworkBehaviour
{
    [SyncObject] private readonly SyncDictionary<string, double> _numbers = new();
    [SyncObject] private readonly SyncDictionary<string, string> _strings = new();
    [SyncObject] private readonly SyncDictionary<string, bool> _booleans = new();
    [SyncObject] private readonly SyncDictionary<string, object> _objects = new();

    public override void OnStartServer()
    {
        base.OnStartServer();

        InstanceFinder.TimeManager.TicksToTime(InstanceFinder.TimeManager.Tick);
    }

    private void OnDisable() {
        this._numbers.Clear();
        this._strings.Clear();
        this._booleans.Clear();
        this._objects.Clear();
    }

    public void SetAttribute(string key, object value)
    {
        if (value is double)
        {
            _numbers[key] = (double)value;
        }
        else if (value is string)
        {
            _strings[key] = (string)value;
            
            _numbers.Remove(key);
            _objects.Remove(key);
            _booleans.Remove(key);
        }
        else if (value is bool)
        {
            _booleans[key] = (bool)value;
            
            _numbers.Remove(key);
            _strings.Remove(key);
            _objects.Remove(key);
        } else
        {
            _objects[key] = value;

            _numbers.Remove(key);
            _strings.Remove(key);
            _booleans.Remove(key);
        }
    }


    public double GetNumber(string key)
    {
        _numbers.TryGetValue(key, out double value);
        return (double)value;
    }
    
    public string GetString(string key)
    {
        _strings.TryGetValue(key, out string value);
        return (string)value;
    }
    
    public object GetObject(string key)
    {
        _objects.TryGetValue(key, out object value);
        return (object)value;
    }
    
    public bool GetBoolean(string key)
    {
        _booleans.TryGetValue(key, out bool value);
        return (bool)value;
    }
}