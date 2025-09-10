using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NativePlugins {
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

		public static object GetDelegate(IntPtr handle, string fnName, Type delegateType) {
			var symbol = dlsym(handle, fnName);
			if (symbol == IntPtr.Zero) {
				throw new Exception($"Failed to get function delegate: {fnName}");
			}
			return Marshal.GetDelegateForFunctionPointer(symbol, delegateType);
		}

		public static bool TryGetDelegate<T>(IntPtr handle, string fnName, out T del) where T : class {
			var symbol = dlsym(handle, fnName);
			if (symbol == IntPtr.Zero) {
				del = null;
				return false;
			}
			del = Marshal.GetDelegateForFunctionPointer<T>(symbol);
			return true;
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

		public static object GetDelegate(IntPtr handle, string fnName, Type delegateType) {
			var symbol = GetProcAddress(handle, fnName);
			if (symbol == IntPtr.Zero) {
				throw new Exception($"Failed to get function delegate: {fnName}");
			}
			return Marshal.GetDelegateForFunctionPointer(symbol, delegateType);
		}

		public static bool TryGetDelegate<T>(IntPtr handle, string fnName, out T del) where T : class {
			var symbol = GetProcAddress(handle, fnName);
			if (symbol == IntPtr.Zero) {
				del = null;
				return false;
			}
			del = Marshal.GetDelegateForFunctionPointer<T>(symbol);
			return true;
		}
#endif

#if UNITY_EDITOR
		public static void BindDelegates(Type cls, IntPtr libHandle) {
			var fields = cls.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (var field in fields) {
				var nativeDelegateAttr = field.GetCustomAttribute<NativeDelegateAttribute>();
				if (nativeDelegateAttr == null) {
					continue;
				}

				var fnName = nativeDelegateAttr.FnName ?? field.Name;
				var fnHandle = GetDelegate(libHandle, fnName, field.FieldType);
				field.SetValue(null, fnHandle);
			}
		}
#endif
	}

#if UNITY_EDITOR
	[AttributeUsage(AttributeTargets.Field)]
	public class NativeDelegateAttribute : Attribute {
		public readonly string FnName;
		public NativeDelegateAttribute() {
			FnName = null;
		}
		public NativeDelegateAttribute(string fnName) {
			FnName = fnName;
		}
	}
#endif

}
