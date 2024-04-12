using System;

public class LuauAPI : System.Attribute {
    public readonly LuauContext AllowedContextsMask;
    
    public LuauAPI(params LuauContext[] allowedContexts) {
        LuauContext mask = 0;
        
        if (allowedContexts.Length > 0) {
            // Only allow the contexts provided
            foreach (var context in allowedContexts) {
                mask |= context;
            }
        } else {
            // Default behavior - allow all contexts if no contexts were provided
            foreach (LuauContext context in Enum.GetValues(typeof(LuauContext))) {
                mask |= context;
            }
        }
        
        AllowedContextsMask = mask;
    }
}
