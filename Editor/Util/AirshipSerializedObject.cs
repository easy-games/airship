using System.Collections.Generic;
using Luau;
using UnityEditor;

public class AirshipSerializedObject {
    internal SerializedObject serializedObject;
    internal SerializedProperty serializedMetadata;
    internal SerializedProperty serializedName;
    internal SerializedProperty serializedProperties;
    internal LuauMetadata metadata;
    internal AirshipEditor editor;
    
    internal void UpdateObject(AirshipEditor editor, SerializedObject @object, LuauMetadata metadata) {
        this.serializedObject = @object;
        this.editor = editor;
        this.metadata = metadata;
        this.serializedMetadata = @object.FindProperty("metadata");
        this.serializedName = @object.FindProperty("name");
        this.serializedProperties = this.serializedMetadata.FindPropertyRelative("properties");
    }

    internal Dictionary<string, AirshipSerializedProperty> _propertyCache = new();
    
    /// <summary>
    /// Finds the airship property with the given name
    /// </summary>
    /// <param name="targetPropertyName">The name of the property (should match the variable in TypeScript)</param>
    /// <returns>The AirshipProperty, if it exists</returns>
    public AirshipSerializedProperty FindAirshipProperty(string targetPropertyName) {
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            var propertyName = property.FindPropertyRelative("name").stringValue;
            if (propertyName == targetPropertyName) {
                var propertyMetadata = metadata.FindProperty(targetPropertyName);
                return new AirshipSerializedProperty(property, propertyMetadata, this.editor);
            }
        }
        
        return default;
    }
    
    public AirshipSerializedProperty[] GetProperties() {
        var properties = new AirshipSerializedProperty[serializedProperties.arraySize];
        
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            properties[i] = new AirshipSerializedProperty(property, metadata.properties[i], this.editor);
        }
    
        return properties;
    }
}