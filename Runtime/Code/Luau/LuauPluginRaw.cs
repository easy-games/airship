using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Luau;
using UnityEngine;

/// <summary>
/// Provides raw Lua API methods.
/// </summary>
public static class LuauPluginRaw {
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ThrowIfNotNullPtr(IntPtr luauExceptionPtr) {
		if (luauExceptionPtr != IntPtr.Zero) {
			throw new LuauException(luauExceptionPtr);
		}
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaNewTable(IntPtr thread, int nArray, int nRecord);
	/// <summary>
	/// Pushes a new table to the Lua stack. Optional initial capacity arguments can be supplied.
	/// </summary>
	public static void NewTable(IntPtr thread, int nArray = 0, int nRecord = 0) {
		ThrowIfNotNullPtr(LuaNewTable(thread, nArray, nRecord));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPushNil(IntPtr thread);
	/// <summary>
	/// Pushes nil to the Lua stack.
	/// </summary>
	public static void PushNil(IntPtr thread) {
		ThrowIfNotNullPtr(LuaPushNil(thread));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPushInteger(IntPtr thread, int n);
	/// <summary>
	/// Pushes an integer to the Lua stack.
	/// </summary>
	public static void PushInteger(IntPtr thread, int n) {
		ThrowIfNotNullPtr(LuaPushInteger(thread, n));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPushUnsignedInteger(IntPtr thread, uint n);
	/// <summary>
	/// Pushes an unsigned integer to the Lua stack.
	/// </summary>
	public static void PushUnsignedInteger(IntPtr thread, uint n) {
		ThrowIfNotNullPtr(LuaPushUnsignedInteger(thread, n));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPushVector(IntPtr thread, float x, float y, float z);
	/// <summary>
	/// Pushes a vector to the Lua stack.
	/// </summary>
	public static void PushVector(IntPtr thread, float x, float y, float z) {
		ThrowIfNotNullPtr(LuaPushVector(thread, x, y, z));
	}
	/// <summary>
	/// Pushes a vector to the Lua stack.
	/// </summary>
	public static void PushVector(IntPtr thread, Vector3 vec) {
		ThrowIfNotNullPtr(LuaPushVector(thread, vec.x, vec.y, vec.z));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPushBoolean(IntPtr thread, int b);
	/// <summary>
	/// Pushes a boolean to the Lua stack.
	/// </summary>
	public static void PushBoolean(IntPtr thread, bool b) {
		ThrowIfNotNullPtr(LuaPushBoolean(thread, b ? 1 : 0));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPushString(IntPtr thread, IntPtr str, int len);
	/// <summary>
	/// Pushes a string to the Lua stack.
	/// </summary>
	public static void PushString(IntPtr thread, string str) {
		var strPtr = Marshal.StringToCoTaskMemUTF8(str);
		var len = Encoding.UTF8.GetByteCount(str);
		var res = LuaPushString(thread, strPtr, len);
		Marshal.FreeCoTaskMem(strPtr);
		ThrowIfNotNullPtr(res);
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaRawSetI(IntPtr thread, int idx, int n);
	/// <summary>
	/// Sets the nth table index to the value at the top of the stack. The table is located at "idx."
	/// </summary>
	public static void RawSetI(IntPtr thread, int idx, int n) {
		ThrowIfNotNullPtr(LuaRawSetI(thread, idx, n));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaPop(IntPtr thread, int n);
	/// <summary>
	/// Pops "n" values from the top of the stack.
	/// </summary>
	public static void Pop(IntPtr thread, int n) {
		ThrowIfNotNullPtr(LuaPop(thread, n));
	}
	
#if UNITY_IPHONE
    [DllImport("__Internal")]
#else
	[DllImport("LuauPlugin")]
#endif
	private static extern IntPtr LuaSetReadonly(IntPtr thread, int idx, int enabled);
	/// <summary>
	/// Sets the read-only flag on the table at index "idx."
	/// </summary>
	public static void SetReadonly(IntPtr thread, int idx, bool enabled) {
		ThrowIfNotNullPtr(LuaSetReadonly(thread, idx, enabled ? 1 : 0));
	}
}
