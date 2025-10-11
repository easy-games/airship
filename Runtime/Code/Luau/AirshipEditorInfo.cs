using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class TyperScriptEnumMember {
    public string Name;
    public string StringValue;
    public int IntValue;
}

public enum TypeScriptEnumMemberType {
    String,
    Integer,
}

[Serializable]
public class TypeScriptEnum : ISerializationCallbackReceiver {
    public string id;
    public TypeScriptEnumMemberType memberType;
    public List<TyperScriptEnumMember> members;

    public void OnAfterDeserialize() {
        if (memberType != TypeScriptEnumMemberType.Integer) return;
        
        isFlagLike = members.Count > 0;
        foreach (var value in members) {
            if (value.IntValue == 0 || value.IntValue == -1) continue;
            var log2 = Math.Log(Math.Abs(value.IntValue), 2);
            if (log2 % 1 != 0) {
                isFlagLike = false;
                break;
            }
        }
    }

    public void OnBeforeSerialize() {
        
    }

    public int IndexOf(string stringValue) {
        return this.members.FindIndex(item => item.StringValue == stringValue);
    }

    public string[] keys => members.Select(member => member.Name).ToArray();
    public string[] keysNicified => members.Select(member => ObjectNames.NicifyVariableName(member.Name)).ToArray();
    
    public TyperScriptEnumMember this[int index] {
        get {
            return members.Find(f => f.IntValue == index);
        }
    }

    public bool isFlagLike { get; private set; }
    private string[] _flags;
    public string[] flagNames {
        get {
            if (memberType != TypeScriptEnumMemberType.Integer ) return new string[] {};
            // if (_flags != null) return _flags;
            
            var maxIndex = 0;
            for (var i = 0; i < 32; i++) {
                var text = this[1 << i];
                if (text == null) continue;
                maxIndex = i;
            }

            var flagArray = new string[maxIndex + 1];
            for (var i = 0; i < maxIndex + 1; i++) {
                var text = this[1 << i];
                if (text == null) continue;
                flagArray[i] = text.Name;
            }

            _flags = flagArray;
            return flagArray;
        }
    }
}

public class EditorMetadataJson {
    public string id;
    [JsonProperty("enum")] public Dictionary<string, Dictionary<string, object>> enumerations;
        
    public static EditorMetadata FromJsonData(string data) {
        var meta = JsonConvert.DeserializeObject<EditorMetadataJson>(data);
        return new EditorMetadata(meta);
    }
}

public class EditorFileInfo {
    public string Hash { get; set; }
    public string LuauPath { get; set; }
}

[Serializable]
public class EditorMetadata {
    [FormerlySerializedAs("id")] public string typescriptPackageId;
    public List<TypeScriptEnum> typescriptEnums = new();

    public Dictionary<string, EditorFileInfo> FileHashes = new();

    public EditorMetadata(EditorMetadataJson json) {
        foreach (var enumeration in json.enumerations) {
            List<TyperScriptEnumMember> members = new();
            TypeScriptEnumMemberType type = TypeScriptEnumMemberType.Integer;

            foreach (var member in enumeration.Value) {
                type = member.Value is Int64 ? TypeScriptEnumMemberType.Integer : TypeScriptEnumMemberType.String;

                members.Add(new TyperScriptEnumMember() {
                    Name = member.Key,
                    IntValue = member.Value is Int64 intValue ? (int)intValue : 0,
                    StringValue = member.Value as string ?? "",
                });
            }

            typescriptEnums.Add(new TypeScriptEnum() {
                id = enumeration.Key,
                memberType = type,
                members = members,
            });
        }

        this.typescriptPackageId = json.id ?? "";
    }
    
    public TypeScriptEnum GetEnumById(string id) {
        return typescriptEnums.First(f => f.id == id);
    }
}

[Serializable]
public class EditorDependencyMetadata {
    public string id;
    public EditorMetadata editorMetadata;
}

public static class AirshipEditorInfoExtensions {
    [CanBeNull]
    public static TypeScriptEnum GetEnum(this IEnumerable<TypeScriptEnum> enums, string id) {
        foreach (var enumItem in enums) {
            if (enumItem.id == id) return enumItem;
        }

        return null;
    }
}

public class AirshipEditorInfo : ScriptableObject {
    private const string BundlePath = "TypeScriptEditorMetadata.aseditorinfo";

    public EditorMetadata editorMetadata;

    public static bool useEnumCache = false;
    private static IEnumerable<TypeScriptEnum> cachedEnums;
    
    public static IEnumerable<TypeScriptEnum> Enums {
        get {
            if (cachedEnums != null && useEnumCache) {
                return cachedEnums;
            }
            
            List<TypeScriptEnum> enums = new();

#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:AirshipEditorInfo");
            foreach (var guid in guids) {
                AirshipEditorInfo supplementalEditorInfo = AssetDatabase.LoadAssetAtPath<AirshipEditorInfo>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var enumItem in supplementalEditorInfo.editorMetadata.typescriptEnums) {
                    enums.Add(enumItem);
                }
            }
#endif
            
            useEnumCache = true;
            cachedEnums = enums;
            return enums;
        }
    }
    
    private static AirshipEditorInfo _instance = null;
    public static AirshipEditorInfo Instance {
        get {
            if (_instance != null) {
                return _instance;
            }
#if UNITY_EDITOR
            if (_instance == null) {
                _instance = AssetDatabase.LoadAssetAtPath<AirshipEditorInfo>($"Assets/{BundlePath}");
            }

            if (_instance != null) {
                _instance.Init();
            }

            return _instance;
#else
            return null;
#endif
        }
    }
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad() {
        _instance = null;
    }

    private void Init() {

    }
}

