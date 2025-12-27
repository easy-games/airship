using System;

/// <summary>
/// Create a custom decorator drawer for the given decorator
/// </summary>
internal class CustomAirshipDecoratorDrawerAttribute : Attribute {
    public string DecoratorName { get; }

    public CustomAirshipDecoratorDrawerAttribute(string decoratorName) {
        DecoratorName = decoratorName;
    }
}