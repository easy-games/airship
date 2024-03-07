using System;
using System.Collections.Generic;
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
    public Int64 IntValue;
}

[Serializable]
public class TypeScriptEnum {
    public string id;
    public List<TyperScriptEnumMember> members;
}

public class EditorMetadataJson {
    [JsonProperty("enum")] public Dictionary<string, Dictionary<string, object>> enumerations;
        
    public static EditorMetadata FromJsonData(string data) {
        var meta = JsonConvert.DeserializeObject<EditorMetadataJson>(data);
        return new EditorMetadata(meta);
    }
}

[Serializable]
public class EditorMetadata {
    public List<TypeScriptEnum> typescriptEnums = new();
    
    public EditorMetadata(EditorMetadataJson json) {
        foreach (var enumeration in json.enumerations) {
            List<TyperScriptEnumMember> members = new();
            foreach (var member in enumeration.Value) {
                members.Add(new TyperScriptEnumMember() {
                    Name = member.Key,
                    IntValue = member.Value is Int64 intValue ? intValue : Int64.MinValue,
                    StringValue = member.Value as string ?? "",
                });
            }
            
            typescriptEnums.Add(new TypeScriptEnum() {
                id = enumeration.Key,
                members = members,
            });
        }
    }
}

public class AirshipEditorInfo : ScriptableObject {
    private const string BundlePath = "AirshipEditor.aseditorinfo";

    public EditorMetadata editorMetadata;
    
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

    private void Init() {}
}

