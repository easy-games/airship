using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Code.Airship.Resources.Scripts.Editor {
    [Serializable]
    public class MaterialColorReferences : ScriptableObject {
        [SerializeField] public SerializableDictionary<Material, ReferenceList> materialReferences;

        public void Reference(Material material, string referencedByGlobalId) {
            if (!materialReferences.TryGetValue(material, out var refs)) {
                refs = new ReferenceList();
                materialReferences[material] = refs;
            }
            refs.list.Add(referencedByGlobalId);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
        
        public void Dereference(Material material, string referencedByGlobalId) {
            if (materialReferences.TryGetValue(material, out var refs)) {
                var index = refs.list.IndexOf(referencedByGlobalId);
                if (index >= 0) refs.list.RemoveAt(index);
                
                if (refs.list.Count == 0) {
                    materialReferences.Remove(material);
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(material));
                    DestroyImmediate(material);
                }
            }
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }
    }

    [Serializable]
    public class ReferenceList {
        [SerializeField] public List<string> list = new List<string>();
    }
    
    // Yoink https://discussions.unity.com/t/solved-how-to-serialize-dictionary-with-unity-serialization-system/71474/4
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver {
        [SerializeField] private List<TKey> keys = new List<TKey>();

        [SerializeField] private List<TValue> values = new List<TValue>();

        // save the dictionary to lists
        public void OnBeforeSerialize() {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this) {
                if (pair.Key == null) continue;
                
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        // load dictionary from lists
        public void OnAfterDeserialize() {
            this.Clear();

            if (keys.Count != values.Count)
                throw new System.Exception(string.Format(
                    "there are {0} keys and {1} values after deserialization. Make sure that both key and value types are serializable.", keys.Count, values.Count));

            for (int i = 0; i < keys.Count; i++) {
                if (keys[i] == null) continue;
                
                this.Add(keys[i], values[i]);
            }
        }
    }
}