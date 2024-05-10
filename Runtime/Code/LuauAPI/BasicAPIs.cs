using System;
using System.Collections.Generic;
using FishNet.Managing.Timing;
using UnityEngine;

public class BasicAPIs {
    /// <summary>
    /// List of static types with no custom behavior (same as empty LuauAPI file)
    /// </summary>
    public static HashSet<Type> APIList = new() {
        typeof(PreciseTick),
        typeof(Gizmos),
    };
}