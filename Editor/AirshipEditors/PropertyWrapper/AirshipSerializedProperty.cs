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

        public AirshipArrayItem PushElement() {
            int index = this.serializedItems.arraySize;
            this.serializedItems.InsertArrayElementAtIndex(index);
            this.serializedObjects.InsertArrayElementAtIndex(index);
            return GetElementAtIndex(index);
        }
        
        public AirshipArrayItem GetElementAtIndex(int index) {
            var value = this.serializedItems.GetArrayElementAtIndex(index);
            var obj = this.serializedObjects.GetArrayElementAtIndex(index);
            return new AirshipArrayItem(property, index, value, obj);
        }

        public void PopElement() {
            var last = this.serializedItems.arraySize - 1;
            this.serializedItems.DeleteArrayElementAtIndex(last);
            this.serializedObjects.DeleteArrayElementAtIndex(last);
        }

        public void MoveArrayElement(int srcIndex, int dstIndex) {
            this.serializedItems.MoveArrayElement(srcIndex, dstIndex);
            this.serializedObjects.MoveArrayElement(srcIndex, dstIndex);
        }

        public void Resize(int newSize) {
            this.serializedItems.arraySize = newSize;
            this.serializedObjects.arraySize = newSize;
        }

        public static implicit operator SerializedProperty(AirshipArray array) {
            if (array.elementType is PropertyType.Object or PropertyType.AirshipBehaviour) {
                return array.serializedObjects;
            } else {
                return array.serializedItems;
            }
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
    
    internal SerializedProperty serializedProperty;
    internal SerializedProperty serializedItems { get; set; }
    internal LuauMetadataProperty propertyMetadata { get; set; }
    internal AirshipEditor editor { get; private set; }

    public bool isArray => serializedType.stringValue == "Array";

    public int arraySize {
        get {
            return serializedItems.FindPropertyRelative("serializedItems").arraySize;
        }
    }

    public AirshipArray array {
        get {
            UpdateProperty();
            return new AirshipArray(
                this, 
                serializedItems.FindPropertyRelative("serializedItems"), 
                serializedItems.FindPropertyRelative("objectRefs"));
        }
    }
    
    public AirshipSerializedProperty(SerializedProperty property, LuauMetadataProperty metadata, AirshipEditor editor) {
        serializedProperty = property;
        UpdateProperty();
        propertyMetadata = metadata;
        decorators = metadata.GetDecorators();
        this.editor = editor;
    }
    
    internal void UpdateProperty() {
        serializedName = serializedProperty.FindPropertyRelative("name");
        
        serializedType = serializedProperty.FindPropertyRelative("type");
        serializedValue = serializedProperty.FindPropertyRelative("serializedValue");
        serializedModified = serializedProperty.FindPropertyRelative("modified");
        
        serializedObjectType = serializedProperty.FindPropertyRelative("objectType");
        serializedObjectValue = serializedProperty.FindPropertyRelative("serializedObject");
        
        serializedItems = serializedProperty.FindPropertyRelative("items");
        
        serializedRef = serializedProperty.FindPropertyRelative("refPath");
        serializedFileRef = serializedProperty.FindPropertyRelative("fileRef");
    }

    internal void ResetToDefault() {
        var defaultValue = propertyMetadata.defaultValue;

        if (isArray) {
            return;
        }
        
        if (isObject) {
            serializedObjectValue.objectReferenceValue = null;
            return;
        }
        
        if (defaultValue == null) return;
        serializedValue.stringValue =
            LuauMetadataPropertySerializer.SerializeAirshipProperty(defaultValue, propertyType);
    }
}