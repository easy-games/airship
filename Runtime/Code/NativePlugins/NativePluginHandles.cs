#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;
using System.Collections;
using UnityEditor;
using Unity.EditorCoroutines.Editor;

namespace NativePlugins {
	internal class PluginHandles {
		internal IntPtr Handle;
		internal List<FieldInfo> FieldInfos;
	}
	
	public static class NativePluginHandles {
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
				loadFn(UnityInterfacesPointerStore.GetUnityInterfacesPointer());
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
	}
	
	[InitializeOnLoad]
	internal class UnityInterfacesPointerStore : IDisposable {
		private const string EditorPrefAirshipUnityInterfacePointer = "AirshipUnityInterfacePointer";
		
		[DllImport("UnityInterfacePlugin", EntryPoint = "GetUnityInterfacesPointer")]
		private static extern IntPtr GetUnityInterfacesPointerNative();
		
		private static IntPtr _unityInterfacesPointerCache;

		static UnityInterfacesPointerStore() {
			// When Unity is closing, remove the pointer reference for this editor session:
			EditorApplication.quitting += () => {
				using var store = new UnityInterfacesPointerStore();
				var currentEditorId = EditorAnalyticsSessionInfo.id;
				store.Remove(currentEditorId);
			};
		}

		/// <summary>
		/// Attempts to get the unity interface pointer through various means.
		/// </summary>
		/// <returns>Unity interface pointer.</returns>
		/// <exception cref="Exception">Throws an exception if the pointer cannot be retrieved.</exception>
		internal static IntPtr GetUnityInterfacesPointer() {
			// Attempt to get cached version (gets cleared during domain refreshes):
			if (_unityInterfacesPointerCache != IntPtr.Zero) {
				return _unityInterfacesPointerCache;
			}
			
			using var store = new UnityInterfacesPointerStore();
			var currentEditorId = EditorAnalyticsSessionInfo.id;
			
			// Attempt to get pointer from plugin. Returns null after domain refreshes because plugin's UnityPluginLoad
			// method doesn't get called again:
			var ptrFromPlugin = GetUnityInterfacesPointerNative();
			if (ptrFromPlugin != IntPtr.Zero) {
				_unityInterfacesPointerCache = ptrFromPlugin;
				if (currentEditorId == 0) {
					EditorCoroutineUtility.StartCoroutineOwnerless(AddAfterSessionIdReady(ptrFromPlugin));
				} else {
					store.Add(currentEditorId, ptrFromPlugin.ToInt64());
				}

				return ptrFromPlugin;
			}
			
			if (currentEditorId == 0) {
				// Editor session ID is only zero right at editor launch. The native plugin should always succeed
				// at this time. Throw an exception if that fails.
				throw new UnityInterfacesException("Failed to get unity interface pointer from native plugin");
			}
			
			if (store.TryGet(currentEditorId, out var ptr64)) {
				return new IntPtr(ptr64);
			}

			throw new UnityInterfacesException("Failed to get unity interface pointer from editor prefs");
		}

		private static IEnumerator AddAfterSessionIdReady(IntPtr ptr) {
			while (EditorAnalyticsSessionInfo.id == 0) {
				yield return null;
			}
			
			using var store = new UnityInterfacesPointerStore();
			var currentEditorId = EditorAnalyticsSessionInfo.id;
			store.Add(currentEditorId, ptr.ToInt64());
		}
		
		private readonly Dictionary<long, long> _editorIdToPtr = new();
		private bool _modified;

		internal UnityInterfacesPointerStore() {
			Refresh();
		}

		private void Refresh() {
			_editorIdToPtr.Clear();
			
			// Values are stored like URL parameters: editorId1=abc&editorId2=xyz ...
			var editorPref = EditorPrefs.GetString(EditorPrefAirshipUnityInterfacePointer);
			var editorPtrPairs = new List<string>(editorPref.Split("&"));
			foreach (var pair in editorPtrPairs) {
				var editorAndPtr = pair.Split("=", 2);
				if (editorAndPtr.Length != 2) {
					continue;
				}
				var editorId = long.Parse(editorAndPtr[0], NumberStyles.HexNumber);
				var ptr64 = long.Parse(editorAndPtr[1], NumberStyles.HexNumber);
				if (!_editorIdToPtr.TryAdd(editorId, ptr64)) {
					// If a duplicate was found, simply set the store as modified, so that it will save
					// without any duplicates once disposed.
					_modified = true;
				}
			}
		}

		private void Save() {
			if (!_modified) return;
			
			var editorPref = Serialize();
			
			EditorPrefs.SetString(EditorPrefAirshipUnityInterfacePointer, editorPref);
			
			_modified = false;
		}

		private bool TryGet(long editorId, out long ptr64) {
			return _editorIdToPtr.TryGetValue(editorId, out ptr64);
		}

		private void Add(long editorId, long ptr64) {
			if (_editorIdToPtr.TryGetValue(editorId, out var existingPtr64) && existingPtr64 == ptr64) {
				return;
			}
			
			_editorIdToPtr[editorId] = ptr64;
			_modified = true;
		}

		private void Remove(long editorId) {
			if (_editorIdToPtr.Remove(editorId)) {
				_modified = true;
			}
		}

		private string Serialize() {
			// Values are stored like URL parameters: editorId1=abc&editorId2=xyz ...
			var pairs = new List<string>(_editorIdToPtr.Count);
			foreach (var (editorId, ptr64) in _editorIdToPtr) {
				pairs.Add($"{editorId:x}={ptr64:x}");
			}
			return string.Join("&", pairs);
		}

		public void Dispose() {
			Save();
		}
	}

	public class UnityInterfacesException : Exception {
		public UnityInterfacesException(string message) : base(message) {}
	}
}

#endif
