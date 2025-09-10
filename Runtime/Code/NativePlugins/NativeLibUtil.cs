using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NativePlugins {
	public static class NativeLibUtil {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
		[DllImport("__Internal")]
		private static extern IntPtr dlopen(string path, int flag);

		[DllImport("__Internal")]
		private static extern IntPtr dlsym(IntPtr handle, string symbolName);

		[DllImport("__Internal")]
		private static extern void dlclose(IntPtr handle);

		public static void CloseLibrary(IntPtr handle) {
			dlclose(handle);
		}

		private static bool TryOpenLibrary(string path, out IntPtr handle) {
			handle = dlopen(path, 0);
			return handle != IntPtr.Zero;
		}

		private static bool TryGetSymbol(IntPtr handle, string symbolName, out IntPtr symbolHandle) {
			symbolHandle = dlsym(handle, symbolName);
			return symbolHandle != IntPtr.Zero;
		}
		
#elif UNITY_EDITOR_WIN
		[DllImport("kernel32.dll")]
		private static extern IntPtr LoadLibrary(string path);
		
		[DllImport("kernel32.dll")]
		private static extern IntPtr GetProcAddress(IntPtr handle, string symbolName);
		
		[DllImport("kernel32.dll")]
		private static extern void FreeLibrary(IntPtr handle);

		public static void CloseLibrary(IntPtr handle) {
			FreeLibrary(handle);
		}

		private static bool TryOpenLibrary(string path, out IntPtr handle) {
			handle = LoadLibrary(path);
			return handle != IntPtr.Zero;
		}

		private static bool TryGetSymbol(IntPtr handle, string symbolName, out IntPtr symbolHandle) {
			symbolHandle = GetProcAddress(handle, symbolName);
			return symbolHandle != IntPtr.Zero;
		}
#endif

#if UNITY_EDITOR
		public static IntPtr OpenLibrary(string path) {
			if (TryOpenLibrary(path, out var handle)) {
				return handle;
			}
			throw new Exception($"Failed to open library: {path}");
		}

		public static T GetDelegate<T>(IntPtr handle, string fnName) where T : class {
			if (TryGetSymbol(handle, fnName, out var symbol)) {
				return Marshal.GetDelegateForFunctionPointer<T>(symbol);
			}
			throw new Exception($"Failed to find function delegate: {fnName}");
		}

		private static object GetDelegate(IntPtr handle, string fnName, Type delegateType) {
			if (TryGetSymbol(handle, fnName, out var symbol)) {
				return Marshal.GetDelegateForFunctionPointer(symbol, delegateType);
			}
			throw new Exception($"Failed to find function delegate: {fnName}");
		}

		public static bool TryGetDelegate<T>(IntPtr handle, string fnName, out T del) where T : class {
			if (TryGetSymbol(handle, fnName, out var symbol)) {
				del = Marshal.GetDelegateForFunctionPointer<T>(symbol);
				return true;
			}
			del = null;
			return false;
		}

		public static void BindDelegates(Type cls, IntPtr libHandle) {
			var fields = cls.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (var field in fields) {
				var nativeDelegateAttr = field.GetCustomAttribute<NativeDelegateAttribute>();
				if (nativeDelegateAttr == null) {
					continue;
				}

				var fnName = nativeDelegateAttr.SymbolName ?? field.Name;
				var fnHandle = GetDelegate(libHandle, fnName, field.FieldType);
				field.SetValue(null, fnHandle);
			}
		}
#endif
	}

#if UNITY_EDITOR
	[AttributeUsage(AttributeTargets.Field)]
	public class NativeDelegateAttribute : Attribute {
		public readonly string SymbolName;
		public NativeDelegateAttribute() {
			SymbolName = null;
		}
		public NativeDelegateAttribute(string symbolName) {
			SymbolName = symbolName;
		}
	}
#endif

}
