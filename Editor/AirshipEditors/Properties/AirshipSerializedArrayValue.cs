using UnityEditor;

public class AirshipSerializedArrayValue : AirshipSerializedValue {
    public AirshipSerializedArrayValue(AirshipSerializedProperty parentSerializedProperty, int index, SerializedProperty valueProperty, SerializedProperty objectValueProperty) {
        serializedModified = parentSerializedProperty.serializedModified;

        serializedType = parentSerializedProperty.serializedItems.FindPropertyRelative("type");
        serializedObjectType = parentSerializedProperty.serializedItems.FindPropertyRelative("objectType");
        
        serializedObjectValue = objectValueProperty;
        serializedValue = valueProperty;

        serializedFileRef = parentSerializedProperty.serializedFileRef;
        serializedRef = parentSerializedProperty.serializedRef;

        propertyMetadata = parentSerializedProperty.propertyMetadata;
        decorators = parentSerializedProperty.propertyMetadata.GetDecorators();

        this.editor = parentSerializedProperty.editor;
    }
}