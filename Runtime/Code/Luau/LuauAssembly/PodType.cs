namespace Code.Luau.LuauAssembly {
    // NOTE: This must match both LuauPlugin & DirectCallbackGenerator
    public enum PODTYPE : int {
        POD_DOUBLE = 0,
        POD_OBJECT = 1,
        POD_STRING = 2,
        POD_INT32 = 3,
        POD_VECTOR3 = 4,
        POD_BOOL = 5,
        POD_NULL = 6,
        POD_RAY = 7,
        POD_MATRIX = 8,
        POD_QUATERNION = 9,
        POD_PLANE = 10,
        POD_COLOR = 11,
        POD_LUAFUNCTION = 12,
        POD_BINARYBLOB = 13,
        POD_VECTOR2 = 14,
        POD_VECTOR4 = 15,
        POD_FLOAT = 16,
        POD_AIRSHIP_COMPONENT = 17,
        POD_BUFFER = 18,
        POD_RECT = 19,
    };
}
