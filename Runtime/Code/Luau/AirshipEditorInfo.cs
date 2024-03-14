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
public class TypeScriptEnum {
    public string id;
    public TypeScriptEnumMemberType memberType;
    public List<TyperScriptEnumMember> members;
}

public class EditorMetadataJson {
    public string id;
    [JsonProperty("enum")] public Dictionary<string, Dictionary<string, object>> enumerations;
        
    public static EditorMetadata FromJsonData(string data) {
        var meta = JsonConvert.DeserializeObject<EditorMetadataJson>(data);
        return new EditorMetadata(meta);
    }
}

[Serializable]
public class EditorMetadata {
    [FormerlySerializedAs("id")] public string typescriptPackageId;
    public List<TypeScriptEnum> typescriptEnums = new();

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
            
            string[] guids = AssetDatabase.FindAssets("t:AirshipEditorInfo");
            foreach (var guid in guids) {
                AirshipEditorInfo supplementalEditorInfo = AssetDatabase.LoadAssetAtPath<AirshipEditorInfo>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var enumItem in supplementalEditorInfo.editorMetadata.typescriptEnums) {
                    enums.Add(enumItem);
                }
            }
            
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
            if (_instance == null && !Application.isPlaying) {
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

