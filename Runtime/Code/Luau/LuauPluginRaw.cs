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
	
	/// <summary>
	/// Pushes a new table to the Lua stack. Optional initial capacity arguments can be supplied.
	/// </summary>
	public static void NewTable(IntPtr thread, int nArray = 0, int nRecord = 0) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaNewTable(thread, nArray, nRecord));
	}
	
	/// <summary>
	/// Pushes nil to the Lua stack.
	/// </summary>
	public static void PushNil(IntPtr thread) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushNil(thread));
	}
	
	/// <summary>
	/// Pushes an integer to the Lua stack.
	/// </summary>
	public static void PushInteger(IntPtr thread, int n) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushInteger(thread, n));
	}
	
	/// <summary>
	/// Pushes an unsigned integer to the Lua stack.
	/// </summary>
	public static void PushUnsignedInteger(IntPtr thread, uint n) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushUnsignedInteger(thread, n));
	}
	
	/// <summary>
	/// Pushes a vector to the Lua stack.
	/// </summary>
	public static void PushVector(IntPtr thread, float x, float y, float z) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushVector(thread, x, y, z));
	}
	
	/// <summary>
	/// Pushes a vector to the Lua stack.
	/// </summary>
	public static void PushVector(IntPtr thread, Vector3 vec) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushVector(thread, vec.x, vec.y, vec.z));
	}
	
	/// <summary>
	/// Pushes a boolean to the Lua stack.
	/// </summary>
	public static void PushBoolean(IntPtr thread, bool b) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushBoolean(thread, b ? 1 : 0));
	}
	
	/// <summary>
	/// Pushes a string to the Lua stack.
	/// </summary>
	public static void PushString(IntPtr thread, string str) {
		var strPtr = Marshal.StringToCoTaskMemUTF8(str);
		var len = Encoding.UTF8.GetByteCount(str);
		var res = LuauPluginNative.LuaPushString(thread, strPtr, len);
		Marshal.FreeCoTaskMem(strPtr);
		ThrowIfNotNullPtr(res);
	}
	
	/// <summary>
	/// Pushes the thread to its own Lua stack.
	/// </summary>
	public static void PushThread(IntPtr thread) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPushThread(thread));
	}
	
	/// <summary>
	/// Sets the nth table index to the value at the top of the stack. The table is located at "idx."
	/// </summary>
	public static void RawSetI(IntPtr thread, int idx, int n) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaRawSetI(thread, idx, n));
	}
	
	/// <summary>
	/// Pops "n" values from the top of the stack.
	/// </summary>
	public static void Pop(IntPtr thread, int n) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaPop(thread, n));
	}
	
	/// <summary>
	/// Sets the read-only flag on the table at index "idx."
	/// </summary>
	public static void SetReadonly(IntPtr thread, int idx, bool enabled) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaSetReadonly(thread, idx, enabled ? 1 : 0));
	}
	
	/// <summary>
	/// Creates a reference to the value at index "idx."
	/// </summary>
	public static int Ref(IntPtr thread, int idx) {
		var refVal = 0;
		ThrowIfNotNullPtr(LuauPluginNative.LuaRef(thread, idx, ref refVal));
		return refVal;
	}
	
	/// <summary>
	/// Removes "refVal" reference.
	/// </summary>
	public static void Unref(IntPtr thread, int refVal) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaUnref(thread, refVal));
	}
	
	/// <summary>
	/// Pushes the value referenced by "refVal" to the top of the thread's stack.
	/// </summary>
	public static void GetRef(IntPtr thread, int refVal) {
		ThrowIfNotNullPtr(LuauPluginNative.LuaGetRef(thread, refVal));
	}
	
	/// <summary>
	/// Gets the top index of the thread's stack (which can also be seen as the stack size).
	/// </summary>
	public static int GetTop(IntPtr thread) {
		var top = 0;
		ThrowIfNotNullPtr(LuauPluginNative.LuaGetTop(thread, ref top));
		return top;
	}
}
