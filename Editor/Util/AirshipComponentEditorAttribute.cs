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

[AttributeUsage(AttributeTargets.Class)]
public class AirshipComponentDecoratorAttribute : Attribute {
    public string DecoratorName { get; }

    public AirshipComponentDecoratorAttribute(string name) {
        DecoratorName = name;
    }
}