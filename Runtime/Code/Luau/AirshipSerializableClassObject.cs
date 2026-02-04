using System;
using System.Collections.Generic;
using Luau;
using UnityEngine;

#if AIRSHIPEX_CLASS_OBJECT
public class AirshipSerializableClassObject : ScriptableObject {
    public string fileRef;
    public string type;
    public LuauMetadata metadata;
}
#endif