using System;
using Luau;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Class)]
public class AirshipComponentEditorAttribute : Attribute {
    /// <summary>
    /// The script path for this editor
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filePath">The relative file path for the component to edit</param>
    public AirshipComponentEditorAttribute(string filePath) {
        FilePath = filePath;
    }
}

/// <summary>
/// Create a custom editor for the given class type (e.g. component or serializable object)
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AirshipEditorAttribute : Attribute {
    public string TypeName { get; }
    public string FilePath { get; set; }

    public AirshipEditorAttribute(string className) {
        TypeName = className;
    }
}

[AttributeUsage(AttributeTargets.Class), Obsolete]
public class AirshipComponentDecoratorAttribute : Attribute {
    public string DecoratorName { get; }

    public AirshipComponentDecoratorAttribute(string name) {
        DecoratorName = name;
    }
}