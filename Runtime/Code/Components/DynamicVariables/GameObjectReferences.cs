using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[LuauAPI]
public class GameObjectReferences : MonoBehaviour {
    private static readonly Dictionary<string, GameObjectReferences> AllReferences = new ();
    [SerializeField]
    private bool isStaticInstance = false;
    [SerializeField]
    private string staticBundleId = "UniqueID";
    [SerializeField]
    private GameObjectArray[] bundledReferences;
    
    private Dictionary<string, Dictionary<string, Object>> bundles = new ();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnStartup()
    {
        AllReferences.Clear();
    }

#region STATIC
    /// <summary>
    /// Get a stored reference. This must be a single instance of an object
    /// </summary>
    /// <param name="bundleKey"></param>
    /// <returns></returns>
    public static GameObjectReferences GetReferences(string bundleKey) {
        if(AllReferences.TryGetValue(bundleKey, out GameObjectReferences reference)) {
            return reference;
        }
        return null;
    }

    /// <summary>
    /// Get a value from a stored reference. This must be a single instance of an object
    /// </summary>
    /// <param name="bundleKey"></param>
    /// <returns></returns>
    public static Object GetValue(string staticKey, string bundleKey, string itemKey) {
        if(AllReferences.TryGetValue(staticKey, out GameObjectReferences reference)){
            return reference?.GetValue(bundleKey, itemKey);
        }
        return null;
    }
#endregion

    private void Awake() {
        if (isStaticInstance) {
            AllReferences.Add(staticBundleId, this);
        }

        PackBundles();
    }

    //Pack all the references into dictionaries
    private void PackBundles() {
        if (bundles.Count > 0) {
            return;
        }
        // Debug.Log("Packing Game Object Reference Bundles");
        foreach (var gameObjectArray in bundledReferences) {
            var bundle = new Dictionary<string, Object>();
            foreach (var reference in gameObjectArray.keyValuePairs) {
                bundle.Add(reference.key, reference.value);
            }
            bundles.Add(gameObjectArray.key, bundle);
        }
    }

    private void OnDestroy() {
        if (isStaticInstance) {
            AllReferences.Remove(staticBundleId);
        }
    }

    public Object GetValue(string bundleKey, string itemKey) {
        PackBundles();
        if (bundles.TryGetValue(bundleKey, out Dictionary<string, Object> kvp)) {
            if (kvp.TryGetValue(itemKey, out Object value)) {
                return value;
            }
        }
        return null;
    }
    
    public T GetValueTyped<T>(string bundleKey, string itemKey) where T:Object {
        return GetValue(bundleKey, itemKey) as T;
    }

    public Object[] GetAllValues(string bundleKey) {
        PackBundles();
        if (bundles.TryGetValue(bundleKey, out Dictionary<string, Object> bundle)) {
            var values = new Object[bundle.Count];
            int i = 0;
            foreach (var kvp in bundle) {
                values[i] = kvp.Value;
                i++;
            }
            return values;
        }
        return Array.Empty<Object>();
    }
    
    public T[] GetAllValuesTyped<T>(string bundleKey, string itemKey) where T:Object {
        PackBundles();
        if (bundles.TryGetValue(bundleKey, out Dictionary<string, Object> bundle)) {
            var values = new T[bundle.Count];
            int i = 0;
            foreach (var kvp in bundle) {
                values[i] = kvp.Value as T;
                i++;
            }
            return values;
        }
        return Array.Empty<T>();
    }
}

[LuauAPI]
[Serializable]
public class GameObjectArray {
    public string key = "";
    public KeyValueReference<Object>[] keyValuePairs;
}

[LuauAPI]
[Serializable]
public class KeyValueReference<T>{
    public string key;
    public T value;
}