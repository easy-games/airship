using Luau;
using UnityEditor;

public class AirshipSerializedProperty : AirshipSerializedValue {
    public class AirshipArray {
        public AirshipSerializedProperty property { get; }
        public PropertyType elementType =>
            GetTypeFromTypeString(property.serializedItems.FindPropertyRelative("type").stringValue);
        
        private readonly SerializedProperty serializedItems;
        private readonly SerializedProperty serializedObjects;
        
        public AirshipArray(AirshipSerializedProperty parentProperty, SerializedProperty serializedItems,
            SerializedProperty objectRefs) {
            this.property = parentProperty;
            this.serializedItems = serializedItems;
            this.serializedObjects = objectRefs;
        }

        public int size {
            get {
                return this.serializedItems.arraySize;
            }
        }

        public AirshipArrayItem GetItemAtIndex(int index) {
            var value = this.serializedItems.GetArrayElementAtIndex(index);
            var obj = this.serializedObjects.GetArrayElementAtIndex(index);
            return new AirshipArrayItem(property, index, value, obj);
        }
    }
    
    public class AirshipArrayItem : AirshipSerializedValue {
        public AirshipArrayItem(AirshipSerializedProperty parentSerializedProperty, int index, SerializedProperty valueProperty, SerializedProperty objectValueProperty) {
            serializedModified = parentSerializedProperty.serializedModified;

            serializedType = parentSerializedProperty.serializedItems.FindPropertyRelative("type");
            serializedObjectType = parentSerializedProperty.serializedItems.FindPropertyRelative("objectType");
        
            serializedObjectValue = objectValueProperty;
            serializedValue = valueProperty;

            serializedFileRef = parentSerializedProperty.serializedFileRef;
            serializedRef = parentSerializedProperty.serializedRef;

            propertyMetadata = parentSerializedProperty.propertyMetadata;
            decorators = parentSerializedProperty.propertyMetadata.GetDecorators();
        }
    }
    
    internal SerializedProperty serializedItems { get; set; }
    internal LuauMetadataProperty propertyMetadata { get; set; }
    internal AirshipEditor editor { get; }

    public bool isArray => serializedType.stringValue == "Array";

    public int arraySize {
        get {
            if (isArray) {
                return serializedItems.FindPropertyRelative("serializedItems").arraySize;
            } else {
                return 0;
            }
        }
    }

    public AirshipArray array {
        get {
            if (isArray) return new AirshipArray(
                this, 
                serializedItems.FindPropertyRelative("serializedItems"), 
                serializedItems.FindPropertyRelative("objectRefs"));

            return null;
        }
    }
    
    public AirshipSerializedProperty(SerializedProperty property, LuauMetadataProperty metadata, AirshipEditor editor) {
        serializedName = property.FindPropertyRelative("name");
        
        serializedType = property.FindPropertyRelative("type");
        serializedValue = property.FindPropertyRelative("serializedValue");
        serializedModified = property.FindPropertyRelative("modified");
        
        serializedObjectType = property.FindPropertyRelative("objectType");
        serializedObjectValue = property.FindPropertyRelative("serializedObject");
        
        serializedItems = property.FindPropertyRelative("items");
        
        serializedRef = property.FindPropertyRelative("refPath");
        serializedFileRef = property.FindPropertyRelative("fileRef");

        propertyMetadata = metadata;
        decorators = metadata.GetDecorators();
        this.editor = editor;
    }
}