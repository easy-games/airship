using System;

namespace Code.Luau {
    /// <summary>
    /// This will cause an event to only fire for the context provided as the first argument.
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Method)]
    public class AttachContext : Attribute {
        
    }
}