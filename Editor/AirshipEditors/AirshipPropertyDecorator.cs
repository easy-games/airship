using System;
using TypescriptAst;
using Luau;
using UnityEngine;

internal abstract class AirshipPropertyDecorator : ScriptableObject {
    /// <summary>
    /// The attribute of this property decorator
    /// </summary>
    public CustomAirshipDecoratorAttribute attribute { get; internal set; }
    /// <summary>
    /// The property this property decorator is attached to
    /// </summary>
    public AirshipSerializedProperty property { get; internal set; }
    /// <summary>
    /// The arguments passed to this property decorator
    /// </summary>
    public LuauMetadataDecoratorValue[] arguments { get; internal set; }
    /// <summary>
    /// The serialized object this property decorator is attached to
    /// </summary>
    public AirshipSerializedObject serializedObject { get; internal set; }
    
    /// <summary>
    /// Should the property be drawn?
    /// </summary>
    /// <returns></returns>
    public virtual bool ShouldDrawProperty() {
        return true;
    }

    /// <summary>
    /// If set, will generate the types for this property decorator
    /// </summary>
    public virtual (string name, DecoratorParameterType type)[] parameters { get; } = null;

    internal TsFunctionDeclaration GetFunctionDeclaration() {
        var decoratorParams = this.parameters;
        if (decoratorParams == null) return null;

        var parameters = new TsParameter[this.parameters.Length];
        for (var i = 0; i < this.parameters.Length; i++) {
            var (parameterName, parameterType) = this.parameters[i];
            parameters[i] = new TsParameter(parameterName, parameterType switch {
                DecoratorParameterType.String => TsKeywordTypeNode.StringTypeNode(),
                DecoratorParameterType.Number => TsKeywordTypeNode.NumberTypeNode(),
                DecoratorParameterType.Boolean => TsKeywordTypeNode.BooleanTypeNode(),
                _ => throw new ArgumentOutOfRangeException()
            }, null);
        }

        return new TsFunctionDeclaration(
            attribute.Name, 
            new IModifier[] { TsKeywordModifier.DeclareModifier() }, 
            parameters, 
            new TsTypeReferenceNode("AirshipDecorator", new ITypeNode[] {
                new TsFunctionType(
                    new [] {
                        new TsParameter("behaviour", new TsTypeReferenceNode("AirshipBehaviour")),
                        new TsParameter("propertyName", TsKeywordTypeNode.StringTypeNode())
                    }, 
                    TsKeywordTypeNode.VoidTypeNode())
            }));
    }
    
    public virtual bool ShouldGenerateType() {
        return false;
    }
    
    
    /// <summary>
    /// Called before OnInspectorGUI() for the given property it's attached to
    /// </summary>
    public virtual void OnBeforeInspectorGUI() {}
}