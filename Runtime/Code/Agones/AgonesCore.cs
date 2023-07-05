using System;
using UnityEngine;

[LuauAPI]
public class AgonesCore {
    public static AgonesProxy Agones;

    public static void SetAgonesProxy(AgonesProxy agones)
    {
        Agones = agones;
    }
}