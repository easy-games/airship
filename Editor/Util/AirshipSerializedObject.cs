using Luau;
using UnityEditor;

public class AirshipSerializedObject {
    internal SerializedObject serializedObject;
    internal SerializedProperty serializedMetadata;
    internal SerializedProperty serializedName;
    internal SerializedProperty serializedProperties;
    internal LuauMetadata metadata;
    
    internal void UpdateObject(SerializedObject @object, LuauMetadata metadata) {
        this.serializedObject = @object;
        this.metadata = metadata;
        this.serializedMetadata = @object.FindProperty("metadata");
        this.serializedName = @object.FindProperty("name");
        this.serializedProperties = this.serializedMetadata.FindPropertyRelative("properties");
    }
    
    
    /// <summary>
    /// Finds the airship property with the given name
    /// </summary>
    /// <param name="targetPropertyName">The name of the property (should match the variable in TypeScript)</param>
    /// <returns>The AirshipProperty, if it exists</returns>
    public AirshipProperty FindAirshipProperty(string targetPropertyName) {
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            var propertyName = property.FindPropertyRelative("name").stringValue;
            if (propertyName == targetPropertyName) {
                var propertyMetadata = metadata.FindProperty(targetPropertyName);
                return new AirshipProperty(property, propertyMetadata);
            }
        }
        
        return default;
    }
    
    public AirshipProperty[] GetProperties() {
        var properties = new AirshipProperty[serializedProperties.arraySize];
        
        for (var i = 0; i < serializedProperties.arraySize; i++) {
            var property = serializedProperties.GetArrayElementAtIndex(i);
            properties[i] = new AirshipProperty(property, metadata.properties[i]);
        }

        return properties;
    }
}