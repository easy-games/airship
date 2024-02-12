using System;
using System.Collections.Generic;

[LuauAPI]
public class StateManager : Singleton<StateManager> {
    private Dictionary<string, string> stateDict = new();

    private void Awake() {
        DontDestroyOnLoad(gameObject);
        this.stateDict.Clear();
        print("StateManager.Awake");
    }

    public static void SetString(string key, string value) {
        StateManager.Instance.stateDict[key] = value;
    }

    public static string GetString(string key) {
        if (StateManager.Instance.stateDict.TryGetValue(key, out string value))
        {
            return value;
        }

        return null;
    }

    public static void RemoveString(string key) {
        Instance.stateDict.Remove(key);
    }

    public static void Clear() {
        Instance.stateDict.Clear();
    }
}