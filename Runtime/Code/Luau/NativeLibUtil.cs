using System;
using System.Runtime.InteropServices;

public static class NativeLibUtil {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
	[DllImport("__Internal")]
	public static extern IntPtr dlopen(string path, int flag);

	[DllImport("__Internal")]
	public static extern IntPtr dlsym(IntPtr handle, string symbolName);

	[DllImport("__Internal")]
	public static extern void dlclose(IntPtr handle);

	public static void CloseLibrary(IntPtr handle) {
		dlclose(handle);
	}

	public static IntPtr OpenLibrary(string path) {
		var handle = dlopen(path, 0);
		if (handle == IntPtr.Zero) {
			throw new Exception($"Failed to load library: {path}");
		}
		return handle;
	}

	public static T GetDelegate<T>(IntPtr handle, string fnName) where T : class {
		var symbol = dlsym(handle, fnName);
		if (symbol == IntPtr.Zero) {
			throw new Exception($"Failed to get function delegate: {fnName}");
		}
		return Marshal.GetDelegateForFunctionPointer<T>(symbol);
	}
	
#elif UNITY_EDITOR_WIN
	[DllImport("kernel32.dll")]
	public static extern IntPtr LoadLibrary(string path);
	
	[DllImport("kernel32.dll")]
	public static extern IntPtr GetProcAddress(IntPtr handle, string symbolName);
	
	[DllImport("kernel32.dll")]
	public static extern void FreeLibrary(IntPtr handle);

	public static void CloseLibrary(IntPtr handle) {
		FreeLibrary(handle);
	}

	public static IntPtr OpenLibrary(string path) {
		var handle = LoadLibrary(path);
		if (handle == IntPtr.Zero) {
			throw new Exception($"Failed to load library: {path}");
		}
		return handle;
	}

	public static T GetDelegate<T>(IntPtr handle, string fnName) where T : class {
		var symbol = GetProcAddress(handle, fnName);
		if (symbol == IntPtr.Zero) {
			throw new Exception($"Failed to get function delegate: {fnName}");
		}
		return Marshal.GetDelegateForFunctionPointer<T>(symbol);
	}
#endif
}
