using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
public class LuauAPI : Attribute {
    public readonly LuauContext AllowedContextsMask;
    
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
    public LuauAPI(LuauContext allowedContextsMask) {
        AllowedContextsMask = allowedContextsMask;
    }

    /// <summary>
    /// Allow any Luau context to access this type.
    /// </summary>
    public LuauAPI() {
        LuauContext mask = 0;
        foreach (LuauContext context in Enum.GetValues(typeof(LuauContext))) {
            mask |= context;
        }

        AllowedContextsMask = mask;
    }
}
