using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Struct)]
public class LuauAPIAttribute : Attribute {
    public readonly LuauContext AllowedContextsMask;
    /// <summary>
    /// List of method/member names that will ignore the AllowedContextsMask. By default
    /// these will be accessible to all contexts but that can be configured by changing the
    /// ContextOverrideMask parameter.
    /// </summary>
    public string[] ContextOverrideList;
    /// <summary>
    /// If ContextOverrideList is provided then all methods/members listed will use this context mask.
    /// </summary>
    public int ContextOverrideMask = ~0;
    
    /// <summary>
    /// Allow the given Luau contexts to access this type. For multiple types,
    /// use bit-masking.
    /// <p>
    /// Game and Protected contexts allowed:
    /// </p>
    /// <code>[LuauAPI(LuauContext.Game | LuauContext.Protected)]</code>
    /// <p>
    /// Only Protected context allowed:
    /// </p>
    /// <code>[LuauAPI(LuauContext.Protected)]</code>
    /// </summary>
    public LuauAPIAttribute(LuauContext allowedContextsMask) {
        AllowedContextsMask = allowedContextsMask;
    }

    /// <summary>
    /// Allow any Luau context to access this type.
    /// </summary>
    public LuauAPIAttribute() {
        LuauContext mask = 0;
        foreach (LuauContext context in Enum.GetValues(typeof(LuauContext))) {
            mask |= context;
        }

        AllowedContextsMask = mask;
    }
}