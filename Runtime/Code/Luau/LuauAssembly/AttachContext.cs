using System;

namespace Code.Luau {
    /// <summary>
    /// This will cause an event to only fire for the context provided as the first argument. If used on a method
    /// the generated type will exclude the context and it will be forced to the calling context in C#.
    ///
    /// In both cases the first parameter should be a LuauContext.
    /// </summary>
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Method)]
    public class AttachContext : Attribute {
        
    }
}