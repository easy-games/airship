using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;

namespace NativePlugins {
	internal class PluginHandles {
		internal IntPtr Handle;
		internal List<FieldInfo> FieldInfos;
	}
	
	public static class NativePluginHandles {
#if UNITY_EDITOR
		private delegate void UnityPluginLoadDelegate(IntPtr unityInterfaces);
		private delegate void UnityPluginUnloadDelegate();
		
		private static readonly Dictionary<string, PluginHandles> LoadedPluginHandles = new();

		private static bool FindPluginHandle(Type cls, out FieldInfo fieldInfo, out NativePluginAttribute attr) {
			var fields = cls.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (var field in fields) {
				var nativePluginAttr = field.GetCustomAttribute<NativePluginAttribute>();
				if (nativePluginAttr != null && field.FieldType == typeof(IntPtr)) {
					fieldInfo = field;
					attr = nativePluginAttr;
					return true;
				}
			}
			fieldInfo = null;
			attr = null;
			return false;
		}

		public static void RegisterPlugin(Type cls) {
			if (!FindPluginHandle(cls, out var field, out var attr)) {
				throw new Exception($"Failed to register plugin for class {cls.Name}: Could not find static field with NativePluginAttribute");
			}

			IntPtr libHandle;
			if (LoadedPluginHandles.TryGetValue(attr.LibPath, out var handles)) {
				libHandle = handles.Handle;
				handles.FieldInfos.Add(field);
				field.SetValue(null, handles.Handle);
			} else {
				libHandle = InitPlugin(attr.LibPath);
				var newHandles = new PluginHandles() {
					Handle = libHandle,
					FieldInfos = new List<FieldInfo>() { field },
				};
				LoadedPluginHandles.Add(attr.LibPath, newHandles);
			}
			
			NativeLibUtil.BindDelegates(cls, libHandle);
		}

		private static IntPtr InitPlugin(string path) {
			var fullLibPath = Path.GetFullPath(Path.Join(Application.dataPath, "..", path));

			if (LoadedPluginHandles.TryGetValue(path, out var existingHandles)) {
				return existingHandles.Handle;
			}
			
			// Open the library:
			var handle = NativeLibUtil.OpenLibrary(fullLibPath);

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
			foreach (var (path, handles) in LoadedPluginHandles) {
				try {
					DeinitPlugin(handles.Handle);
					handles.Handle = InitPlugin(path);
				} catch (Exception e) {
					Debug.LogException(e);
				}
				foreach (var field in handles.FieldInfos) {
					field.SetValue(null, handles.Handle);
				}
			}
			LoadedPluginHandles.Clear();
			
			// Note: We don't bind to EditorApplication.quitting because that will prematurely
			// close the libraries before some finalizers might run (e.g. ZstdContext freeing
			// up its context object). Since loaded libraries are loaded into the address space
			// of the application, we'll just let the OS close out our libraries when Unity exits.
		}
	
		[DllImport("UnityInterfacePlugin")]
		private static extern IntPtr GetUnityInterfacesPointer();

#endif
	}
}
