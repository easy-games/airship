using System;
using System.Linq;
using Editor.Typescript;

/// <summary>
/// Types supported by metadata decorators in Airship TS
/// </summary>
internal enum DecoratorParameterType {
    String,
    Number,
    Boolean,
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
internal class CustomAirshipDecoratorAttribute : Attribute {
    public string Name { get; }
    public CustomAirshipDecoratorAttribute(string name) {
        Name = name;
    }
}