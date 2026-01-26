using System;
using Luau;
using NUnit.Framework.Internal;

/// <summary>
/// Create a custom editor for the given class type (e.g. component or serializable object)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class CustomAirshipEditorAttribute : Attribute {
    public string TypeName { get; }
    public string AssetPath { get; set; }
    public bool EditorForChildClasses { get; set; }
    public int Priority { get; set; } = 0;

    public CustomAirshipEditorAttribute(string className) {
        TypeName = className;
    }
    
    public CustomAirshipEditorAttribute(string className, bool editorForChildClasses) {
        TypeName = className;
        EditorForChildClasses = editorForChildClasses;
    }

    public CustomAirshipEditorAttribute(string className, string assetPath) {
        TypeName = className;
        AssetPath = assetPath;
    }
    
    public CustomAirshipEditorAttribute(string className, string assetPath, bool editorForChildClasses) {
        TypeName = className;
        AssetPath = assetPath;
        EditorForChildClasses = editorForChildClasses;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal class CustomAirshipCoreEditorAttribute : CustomAirshipEditorAttribute {
    public CustomAirshipCoreEditorAttribute(string className): base(className) {}
}

