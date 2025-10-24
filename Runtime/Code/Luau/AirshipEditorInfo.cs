using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class TypeScriptEnumMember {
    [SerializeField] internal string Name;
    [SerializeField] internal string StringValue;
    [SerializeField] internal int IntValue;

    public string name => Name;
    public string stringValue => StringValue;
    public int intValue => IntValue;
}

public enum TypeScriptEnumMemberType {
    String,
    Integer,
}

[Serializable]
public class TypeScriptEnum : ISerializationCallbackReceiver {
    [FormerlySerializedAs("id")] [SerializeField] private string _id;
    [FormerlySerializedAs("memberType")] [SerializeField] private TypeScriptEnumMemberType _memberType;
    [FormerlySerializedAs("members")] [SerializeField] private List<TypeScriptEnumMember> _members;
    public string id => _id;
    public TypeScriptEnumMemberType memberType => _memberType;
    public IReadOnlyList<TypeScriptEnumMember> members => _members;

    internal TypeScriptEnum(string id, TypeScriptEnumMemberType memberType, List<TypeScriptEnumMember> members) {
        _id = id;
        _memberType = memberType;
        _members = members;
    }
    
    internal string Serialize(TypeScriptEnumMember value) {
        return memberType switch {
            TypeScriptEnumMemberType.Integer => value.intValue.ToString(CultureInfo.InvariantCulture),
            TypeScriptEnumMemberType.String => value.stringValue,
            _ => throw new ArgumentOutOfRangeException(nameof(memberType), memberType, null)
        };
    }

    internal TypeScriptEnumMember Deserialize(string value) {
        switch (memberType) {
            case TypeScriptEnumMemberType.Integer: {
                if (int.TryParse(value, out var intValue)) {
                    return GetMemberByValue(intValue) ?? defaultValue;
                }

                return defaultValue;
            }
            case TypeScriptEnumMemberType.String:
                return GetMemberByValueOrDefault(value);
        }

        return defaultValue;
    }
    
    public void OnAfterDeserialize() {
        keys = new string[_members.Count];
        for (var i = 0; i < _members.Count; i++) {
            var member = _members[i];
            keys[i] = member.name;
        }
        
#if UNITY_EDITOR
        keysNicified = new string[_members.Count];
        for (var i = 0; i < _members.Count; i++) {
            keysNicified[i] = ObjectNames.NicifyVariableName(keys[i]);
        }
#endif
        
        if (_memberType != TypeScriptEnumMemberType.Integer) return;
        
        isFlagLike = _members.Count > 0;
        foreach (var value in _members) {
            if (value.IntValue == 0 || value.IntValue == -1) continue;
            var log2 = Math.Log(Math.Abs(value.IntValue), 2);
            if (log2 % 1 != 0) {
                isFlagLike = false;
                break;
            }
        }
    }

    public void OnBeforeSerialize() {}

    public int IndexOf(string stringValue) {
        return this._members.FindIndex(item => item.StringValue == stringValue);
    }

    public string[] keys { get; private set; }
    public string[] keysNicified { get; private set; }

    public TypeScriptEnumMember this[int index] {
        get {
            return _members.Find(f => f.IntValue == index);
        }
    }
    
    public TypeScriptEnumMember this[string value] {
        get => _members.Find(f => f.Name == value || f.StringValue == value);
    }

    public bool isFlagLike { get; private set; }
    private string[] _flags;
    public string[] flagNames {
        get {
            if (_memberType != TypeScriptEnumMemberType.Integer ) return new string[] {};
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
    
    [CanBeNull]
    public TypeScriptEnumMember GetMemberByName(string name) {
        foreach (var member in members) {
            if (member.name == name) return member;
        }

        return null;
    }
    
    [CanBeNull]
    public TypeScriptEnumMember GetMemberByValue(int value) {
        foreach (var member in members) {
            if (member.intValue == value) return member;
        }

        return null;
    }
    
    [CanBeNull]
    public TypeScriptEnumMember GetMemberByValue(string value) {
        foreach (var member in members) {
            if (member.stringValue == value) return member;
        }

        return null;
    }

    public TypeScriptEnumMember GetMemberByValueOrDefault(int value) => GetMemberByValue(value) ?? defaultValue;
    public TypeScriptEnumMember GetMemberByValueOrDefault(string value) => GetMemberByValue(value) ?? defaultValue;

    public TypeScriptEnumMember defaultValue => members[0];
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
            List<TypeScriptEnumMember> members = new();
            TypeScriptEnumMemberType type = TypeScriptEnumMemberType.Integer;

            foreach (var member in enumeration.Value) {
                type = member.Value is Int64 ? TypeScriptEnumMemberType.Integer : TypeScriptEnumMemberType.String;

                members.Add(new TypeScriptEnumMember() {
                    Name = member.Key,
                    IntValue = member.Value is Int64 intValue ? (int)intValue : 0,
                    StringValue = member.Value as string ?? "",
                });
            }

            typescriptEnums.Add(new TypeScriptEnum(enumeration.Key, type, members));
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

    [Obsolete]
    public static int FindIndex(this IReadOnlyList<TypeScriptEnumMember> members,
        Func<TypeScriptEnumMember, bool> predicate) {

        for (var i = 0; i < members.Count; i++) {
            if (predicate(members[i])) return i;
        }

        return -1;
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

