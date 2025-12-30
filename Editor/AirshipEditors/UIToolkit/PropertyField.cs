using Unity.Properties;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Editor.UIElements {
    public static class AirshipBindingExtensions {
        public static void Bind(this VisualElement visualElement, AirshipSerializedObject serializedObject) {
            
        }
        
        public static void BindProperty(this VisualElement visualElement, AirshipSerializedProperty property) {
            
        }
    }
    
    public class AirshipPropertyField : VisualElement, IBindable {
        private string _label;
        
        public IBinding binding { get; set; }
        public string bindingPath { get; set; }
        internal AirshipSerializedObject serializedObject { get; set; }
        private AirshipSerializedProperty serializedProperty { get; set; }

        [CreateProperty]
        public string label {
            get => _label;
            set {
                _label = value;
                // TODO: Rebind?
            }
        }

        public AirshipPropertyField(AirshipSerializedProperty serializedProperty) {
            this.serializedProperty = serializedProperty;
            this.serializedObject = serializedProperty.serializedObject;
        }
    }
}