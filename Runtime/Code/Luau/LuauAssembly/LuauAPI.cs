using System;

public class LuauAPI : Attribute {
    public readonly LuauContext AllowedContextsMask;
    
    public LuauAPI(LuauContext allowedContextsMask) {
        AllowedContextsMask = allowedContextsMask;
    }

    public LuauAPI() {
        LuauContext mask = 0;
        foreach (LuauContext context in Enum.GetValues(typeof(LuauContext))) {
            mask |= context;
        }

        AllowedContextsMask = mask;
    }
}
