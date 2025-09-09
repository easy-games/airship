using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NativePlugins {
	public static class NativePluginHandles {
#if UNITY_EDITOR
		private delegate void UnityPluginLoadDelegate(IntPtr unityInterfaces);
		private delegate void UnityPluginUnloadDelegate();
		
		private const string BasePluginsPath = "/Packages/gg.easy.airship/Runtime/Plugins";
#if UNITY_EDITOR_OSX
		private const string LuauLibPath = BasePluginsPath + "/Mac/LuauPlugin.bundle/Contents/MacOS/LuauPlugin";
#elif UNITY_EDITOR_LINUX
		private const string LuauLibPath = BasePluginsPath + "/Linux/libLuauPlugin.so";
#elif UNITY_EDITOR_WIN
		private const string LuauLibPath = BasePluginsPath + "/Windows/x64/LuauPlugin.dll";
#endif

		private static IntPtr _libLuauPluginHandle;
		public static IntPtr LibLuauPluginHandle {
			get {
				if (_libLuauPluginHandle == IntPtr.Zero) {
					_libLuauPluginHandle = InitPlugin(LuauLibPath);
				}
				return _libLuauPluginHandle;
			}
		}
		
		private static readonly Dictionary<string, IntPtr> LoadedPluginHandles = new();

		private static IntPtr InitPlugin(string path) {
			var fullLibPath = Path.GetFullPath(Path.Join(Application.dataPath, "..", path));

			if (LoadedPluginHandles.TryGetValue(path, out var existingHandle)) {
				Debug.LogWarning($"attempted to load plugin more than once: {fullLibPath}");
				return existingHandle;
			}
			
			// Open the library:
			var handle = NativeLibUtil.OpenLibrary(fullLibPath);
			LoadedPluginHandles.Add(path, handle);

			// Call the UnityPluginLoad plugin function if it exists:
			if (NativeLibUtil.TryGetDelegate<UnityPluginLoadDelegate>(handle, "UnityPluginLoad", out var loadFn)) {
				loadFn(GetUnityInterfacesPointer());
			}
			
			return handle;
		}

		private static void DeinitPlugin(IntPtr handle) {
			if (NativeLibUtil.TryGetDelegate<UnityPluginUnloadDelegate>(handle, "UnityPluginUnload", out var unloadFn)) {
				unloadFn();
			}
			NativeLibUtil.CloseLibrary(handle);
		}
		
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void InitPlugins() {
			// Reload libraries:
			var libPaths = LoadedPluginHandles.Keys;
			foreach (var (path, handle) in LoadedPluginHandles) {
				try {
					DeinitPlugin(handle);
				} catch (Exception e) {
					Debug.LogException(e);
				}
			}
			LoadedPluginHandles.Clear();
			_libLuauPluginHandle = IntPtr.Zero;
			foreach (var path in libPaths) {
				InitPlugin(path);
			}

			EditorApplication.quitting -= DeinitPlugins;
			EditorApplication.quitting += DeinitPlugins;
		}

		private static void DeinitPlugins() {
			if (LoadedPluginHandles.Count == 0) {
				return;
			}

			foreach (var (path, handle) in LoadedPluginHandles) {
				try {
					DeinitPlugin(handle);
				} catch (Exception e) {
					Debug.LogException(e);
				}
			}
			LoadedPluginHandles.Clear();
			
			_libLuauPluginHandle = IntPtr.Zero;
		}
	
		[DllImport("UnityInterfacePlugin")]
		private static extern IntPtr GetUnityInterfacesPointer();
	}
#endif
}
