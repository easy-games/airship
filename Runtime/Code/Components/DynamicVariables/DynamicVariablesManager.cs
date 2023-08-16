using System;
using System.Collections.Generic;
using Code.Components.DynamicVariables;
using FishNet;
using JetBrains.Annotations;

[LuauAPI]
public class DynamicVariablesManager : Singleton<DynamicVariablesManager>
{
    private Dictionary<string, DynamicVariables> runtimeList = new();
    [NonSerialized] private List<DynamicVariables> collections = new();

    [CanBeNull] public DynamicVariables GetVars(string collectionId)
    {
        if (this.runtimeList.TryGetValue(collectionId.ToLower(), out var vars))
        {
            return vars;
        }

        return null;
    }

    public void RegisterVars(string key, DynamicVariables collection)
    {
        this.runtimeList[key.ToLower()] = collection;
        if (!this.collections.Contains(collection))
        {
            this.collections.Add(collection);
        }
    }

    private void OnEnable()
    {
        InstanceFinder.ClientManager.RegisterBroadcast<DynamicVariablesUpdateNumberBroadcast>(this.OnDynamicVariableUpdateNumber);
        InstanceFinder.ClientManager.RegisterBroadcast<DynamicVariablesUpdateStringBroadcast>(this.OnDynamicVariableUpdateString);
        InstanceFinder.ClientManager.RegisterBroadcast<DynamicVariablesUpdateVector3Broadcast>(this.OnDynamicVariableUpdateVector3);
    }

    private void Start()
    {
        foreach (var collection in this.collections)
        {
            this.RegisterVars(collection.collectionId, collection);
        }
    }

    private void OnDynamicVariableUpdateNumber(DynamicVariablesUpdateNumberBroadcast broadcast)
    {
        var vars = this.GetVars(broadcast.collectionKey);
        if (vars == null) return;

        vars.SetNumber(broadcast.key, broadcast.valueNumber);
    }

    private void OnDynamicVariableUpdateString(DynamicVariablesUpdateStringBroadcast broadcast)
    {
        var vars = this.GetVars(broadcast.collectionKey);
        if (vars == null) return;

        vars.SetString(broadcast.key, broadcast.valueString);
    }

    private void OnDynamicVariableUpdateVector3(DynamicVariablesUpdateVector3Broadcast broadcast)
    {
        var vars = this.GetVars(broadcast.collectionKey);
        if (vars == null) return;

        vars.SetVector3(broadcast.key, broadcast.valueVector3);
    }
}