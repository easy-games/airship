#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NativePlugins {
	public static class NativeLibUtil {
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
		[DllImport("UnityInterfacePlugin")]
		private static extern int GetRtldLazy();

		[DllImport("__Internal")]
		private static extern IntPtr dlopen(string path, int flag);

		[DllImport("__Internal")]
		private static extern IntPtr dlsym(IntPtr handle, string symbolName);

		[DllImport("__Internal")]
		private static extern void dlclose(IntPtr handle);

		[DllImport("__Internal")]
		private static extern IntPtr dlerror();

		public static void CloseLibrary(IntPtr handle) {
			dlclose(handle);
		}

		private static bool TryOpenLibrary(string path, out IntPtr handle) {
			handle = dlopen(path, GetRtldLazy());
			return handle != IntPtr.Zero;
		}

		private static bool TryGetSymbol(IntPtr handle, string symbolName, out IntPtr symbolHandle) {
			symbolHandle = dlsym(handle, symbolName);
			return symbolHandle != IntPtr.Zero;
		}

		private static bool TryGetError(out string error) {
			var errPtr = dlerror();
			if (errPtr == IntPtr.Zero) {
				error = null;
				return false;
			}
			error = Marshal.PtrToStringUTF8(errPtr);
			return true;
		}
		
#elif UNITY_EDITOR_WIN
		private const uint FormatMessageAllocateBuffer = 0x00000100;
		private const uint FormatMessageArgumentArray = 0x00002000;
		private const uint FormatMessageFromHModule = 0x00000800;
		private const uint FormatMessageFromString = 0x00000400;
		private const uint FormatMessageFromSystem = 0x00001000;
		private const uint FormatMessageIgnoreInserts = 0x00000200;
		
		[DllImport("kernel32.dll")]
		private static extern IntPtr LoadLibrary(string path);
		
		[DllImport("kernel32.dll")]
		private static extern IntPtr GetProcAddress(IntPtr handle, string symbolName);
		
		[DllImport("kernel32.dll")]
		private static extern void FreeLibrary(IntPtr handle);

		[DllImport("kernel32.dll")]
		private static extern uint GetLastError();

		[DllImport("kernel32.dll")]
		private static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, uint dwExtraInfo, out IntPtr lpBuffer, uint nSize);

		[DllImport("kernel32.dll")]
		private static extern uint LocalFree(IntPtr ptr);
		
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

		private static bool TryGetError(out string error) {
			var errCode = GetLastError();
			if (errCode != 0) {
				var size = FormatMessage(
					FormatMessageAllocateBuffer | FormatMessageFromSystem | FormatMessageIgnoreInserts,
					IntPtr.Zero,
					errCode,
					0,
					0,
					out var errPtr,
					0
				);
				if (size != 0) {
					error = Marshal.PtrToStringUni(errPtr, (int)size);
					// Need to tell Windows to free the buffer containing the error string:
					LocalFree(errPtr);
				} else {
					// FormatMessage failed, but we still want some info on the error,
					// so we'll just give back the error code:
					error = $"[Windows error code: {errCode}]";
				}
				return true;
			}
			error = null;
			return false;
		}
#endif

		public static IntPtr OpenLibrary(string path) {
			if (TryOpenLibrary(path, out var handle)) {
				return handle;
			}

			if (TryGetError(out var error)) {
				throw new Exception($"Error when opening library '{path}' - {error}");
			}
			throw new Exception($"Unknown error when opening library '{path}'");
		}

		public static T GetDelegate<T>(IntPtr handle, string fnName) where T : class {
			if (TryGetSymbol(handle, fnName, out var symbol)) {
				return Marshal.GetDelegateForFunctionPointer<T>(symbol);
			}

			if (TryGetError(out var error)) {
				throw new Exception($"Error when getting symbol '{fnName}' - {error}");
			}
			throw new Exception($"Unknown error when getting symbol '{fnName}'");
		}

		private static object GetDelegate(IntPtr handle, string fnName, Type delegateType) {
			if (TryGetSymbol(handle, fnName, out var symbol)) {
				return Marshal.GetDelegateForFunctionPointer(symbol, delegateType);
			}

			if (TryGetError(out var error)) {
				throw new Exception($"Error when getting symbol '{fnName}' - {error}");
			}
			throw new Exception($"Unknown error when getting symbol '{fnName}'");
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
	}
}

#endif
