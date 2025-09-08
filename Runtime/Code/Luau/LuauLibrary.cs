using System;
using System.Runtime.InteropServices;

public static class LuauLibrary {
	private const string BasePluginsPath = "Packages/gg.easy.airship/Runtime/Plugins";
#if UNITY_EDITOR_OSX
	private const string LuauLibPath = BasePluginsPath + "/Mac/LuauPlugin.bundle/Contents/MacOS/LuauPlugin";
#elif UNITY_EDITOR_LINUX
	private const string LuauLibPath = BasePluginsPath + "/Linux/libLuauPlugin.so";
#elif UNITY_EDITOR_WIN
	private const string LuauLibPath = BasePluginsPath + "/Windows/x64/LuauPlugin.dll";
#endif
	
#if UNITY_EDITOR
	public static IntPtr LibHandle;

	public delegate void InitDelegate();
#endif
	
	
}
