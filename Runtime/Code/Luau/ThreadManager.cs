
//Any time a thread is passed a C# 'object', 
using System;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace Luau {
    public static class ThreadDataManager {
        private static Dictionary<IntPtr, ThreadData> m_threadData = new();
        
        private static int s_threadDataSize = 128;
        private static ThreadData[] s_workingList = new ThreadData[128];
        private static bool s_workingListDirty = true;

        private static List<IntPtr> s_removalList = new List<IntPtr>(8);

        //For passing across the barrier to the dll
        private static PinnedArray<int> listOfGameObjectIds = new PinnedArray<int>(512);
        private static PinnedArray<int> listOfDestroyedGameObjectIds = new PinnedArray<int>(512);
        
        public static ThreadData GetThreadDataByPointer(IntPtr thread) {
            m_threadData.TryGetValue(thread, out ThreadData threadData);
            return threadData;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup() {
            OnReset();
        }

        public static void OnReset() {
            s_workingListDirty = true;
            m_threadData.Clear();
            s_objectKeys.Clear();
            s_reverseObjectKeys.Clear();
            s_cleanUpKeys.Clear();
            s_debuggingKeys.Clear();
            s_keyGen = 0;
        }

        public static void ResetContext(LuauContext context) {
            var removal = new List<IntPtr>();
            foreach (var (thread, threadData) in m_threadData) {
                if (context != threadData.m_context) continue;
                removal.Add(thread);
            }

            foreach (var thread in removal) {
                m_threadData.Remove(thread);
            }
        }

        public class KeyHolder : System.Object {
            public KeyHolder(int p) {
                key = p;
            }
            public int key = 0;
        }

        public static int s_keyGen = 0;
        
        //Keep strong references to all objects created
        private static Dictionary<int, object> s_objectKeys = new();
        private static Dictionary<object, int> s_reverseObjectKeys = new();
        private static HashSet<int> s_cleanUpKeys = new HashSet<int>();

        //Debugging
        private static Dictionary<int, object> s_debuggingKeys = new();
        private static bool s_debugging = false;

        public static int GetOrCreateObjectId(System.Object obj) {
            bool found = s_reverseObjectKeys.TryGetValue(obj, out int key);
            if (found) {
                return key;
            }

            s_keyGen += 1;
            //Only place objects get added to the dictionary
            s_objectKeys.Add(s_keyGen, obj);
            s_reverseObjectKeys.Add(obj, s_keyGen);

            if (s_debugging) {
                Debug.Log("GC add reference to " + obj + " id: " + s_keyGen);
                s_debuggingKeys.Add(s_keyGen, obj);
            }

            return s_keyGen;
        }

        private static ThreadData GetOrCreateThreadData(LuauContext context, IntPtr thread, string debugString) {
            bool found = m_threadData.TryGetValue(thread, out ThreadData threadData);
            if (found == false) {
                threadData = new ThreadData();
                threadData.m_context = context;
                threadData.m_threadHandle = thread;
                threadData.m_debug = debugString;
                m_threadData.Add(thread, threadData);
                s_workingListDirty = true;
            }
            return threadData;
        }

        private static ThreadData GetThreadData(IntPtr thread) {
            bool found = m_threadData.TryGetValue(thread, out ThreadData threadData);
            return threadData;
        }

        public static int AddObjectReference(IntPtr thread, System.Object obj) {
            int id = GetOrCreateObjectId(obj);
            return id;
        }

        public static object GetObjectReference(IntPtr thread, int instanceId, bool preventTrace = false) {
            if (instanceId == -1) {
                return null;
            }
            
            bool res = s_objectKeys.TryGetValue(instanceId, out object value);
            if (!res) {
                if (s_debugging == false) {    
                    Debug.LogError("Object reference not found: " + instanceId + " Object id was never assigned, where did you get this key from?! Currently highest assigned key is " + s_keyGen + " " + LuauCore.LuaThreadToString(thread));
                } else if (s_debugging == true) {
                    bool found = s_debuggingKeys.TryGetValue(instanceId, out object debugObject);
                    
                    if (found == false) {
                        Debug.LogError("Object reference not found: " + instanceId + " Object id was never assigned, where did you get this key from?! Currently highest assigned key is " + s_keyGen + " " + LuauCore.LuaThreadToString(thread));
                    } else {
                        Debug.LogError("Object reference not found: " + instanceId + " Object was created, and then signaled for garbage collection from luau." + debugObject.ToString() + " Currently highest assigned key is " + s_keyGen + " " + LuauCore.LuaThreadToString(thread));
                    }
                   
                }

                if (!preventTrace) {
                    LuauPlugin.LuauGetDebugTrace(thread);
                }

                return null;
            }
            return value;
        }

        public static void DeleteObjectReference(int instanceId) {
            if (s_debugging) {
                Debug.Log("GC removed reference to " + instanceId);
            }
            //Wait til end of frame to clean it up
            s_cleanUpKeys.Add(instanceId);
        }

        public static Luau.CallbackWrapper RegisterCallback(LuauContext context, IntPtr thread, int handle, string methodName) {
            ThreadData threadData = GetOrCreateThreadData(context, thread, "RegisterCallback");

            Luau.CallbackWrapper callback = new Luau.CallbackWrapper(context, thread, methodName, handle);
            threadData.m_callbacks.Add(callback);

            return callback;
        }

        public static LuauSignalWrapper RegisterSignalWrapper(LuauContext context, IntPtr thread, int instanceId, ulong signalNameHash) {
            var threadData = GetOrCreateThreadData(context, thread, "RegisterSignalWrapper");
            var wrapper = new LuauSignalWrapper(context, thread, instanceId, signalNameHash);
            threadData.m_signalWrappers.Add(wrapper);
            
            return wrapper;
        }
        
        public static void Error(IntPtr thread) {
            ThreadData threadData = GetThreadData(thread);
            if (threadData == null) {
                return;
            }
            threadData.m_error = true;
        }

        public static void SetOnUpdateHandle(LuauContext context, IntPtr thread, int handle, GameObject gameObject) {
            ThreadData threadData = GetOrCreateThreadData(context, thread, "SetOnUpdateHandle");
            threadData.associatedGameObject = new WeakReference<GameObject>(gameObject);
            threadData.m_onUpdateHandle = handle;
        }
        
        public static void SetOnLateUpdateHandle(LuauContext context, IntPtr thread, int handle, GameObject gameObject) {
            ThreadData threadData = GetOrCreateThreadData(context, thread, "SetOnLateUpdateHandle");
            threadData.associatedGameObject = new WeakReference<GameObject>(gameObject);
            threadData.m_onLateUpdateHandle = handle;
        }
        
        public static void SetOnFixedUpdateHandle(LuauContext context, IntPtr thread, int handle, GameObject gameObject) {
            ThreadData threadData = GetOrCreateThreadData(context, thread, "SetOnFixedUpdateHandle");
            threadData.associatedGameObject = new WeakReference<GameObject>(gameObject);
            threadData.m_onFixedUpdateHandle = handle;
        }


        private static int UpdateWorkingList() {
            int count = m_threadData.Count;
            if (s_workingListDirty == false) {
                return count;
            }
            s_workingListDirty = false;

            if (count > s_threadDataSize) {
                s_threadDataSize = count * 2;
                s_workingList = new ThreadData[s_threadDataSize];
            }
          
            var enumerator = m_threadData.GetEnumerator();
            int i = 0;
            while (enumerator.MoveNext()) {
                s_workingList[i++] = enumerator.Current.Value;
            }
            
            return count;
        }
        
        public unsafe static void InvokeUpdate() {

            int count = UpdateWorkingList();

            for (int i = 0; i < count; i++) {
                ThreadData threadData = s_workingList[i];
                
                if (threadData.m_onUpdateHandle > 0 && threadData.m_yielded == false && threadData.m_error == false) {
                    //Check the gameobject for enabled
                    if (threadData.associatedGameObject.TryGetTarget(out GameObject gameObject) == false) {
                        threadData.m_onUpdateHandle = 0;
                        continue;
                    }
                    if (!gameObject || gameObject.activeInHierarchy == false) {
                        continue;
                    }
                    
                    int numParameters = 0;
                    System.Int32 integer = (System.Int32)threadData.m_onUpdateHandle;
                    int retValue = LuauPlugin.LuauCallMethodOnThread(threadData.m_threadHandle, new IntPtr(value: &integer), 0, numParameters);
                    if (retValue < 0) {
                        ThreadDataManager.Error(threadData.m_threadHandle);
                    }
                }
            }
        }

        public unsafe static void InvokeLateUpdate() {
            int count = UpdateWorkingList();

            for (int i = 0; i < count; i++) {
                ThreadData threadData = s_workingList[i];
                if (threadData.m_onLateUpdateHandle > 0 && threadData.m_yielded == false && threadData.m_error == false) {
                    //Check the gameobject for enabled
                    if (threadData.associatedGameObject.TryGetTarget(out GameObject gameObject) == false) {
                        threadData.m_onUpdateHandle = 0;
                        continue;
                    }
                    if (!gameObject || gameObject.activeInHierarchy == false) {
                        continue;
                    }
                    
                    int numParameters = 0;
                    System.Int32 integer = (System.Int32)threadData.m_onLateUpdateHandle;
                    int retValue = LuauPlugin.LuauCallMethodOnThread(threadData.m_threadHandle, new IntPtr(value: &integer), 0, numParameters);
                    if (retValue < 0) {
                        ThreadDataManager.Error(threadData.m_threadHandle);
                    }
                }
            }
        }
        
        public unsafe static void InvokeFixedUpdate() {
            int count = UpdateWorkingList();

            for (int i = 0; i < count; i++) {
                ThreadData threadData = s_workingList[i];

                if (threadData.m_onFixedUpdateHandle > 0 && threadData.m_yielded == false && threadData.m_error == false) {
                    //Check the gameobject for enabled
                    if (threadData.associatedGameObject.TryGetTarget(out GameObject gameObject) == false) {
                        threadData.m_onUpdateHandle = 0;
                        continue;
                    }
                    if (!gameObject || gameObject.activeInHierarchy == false) {
                        continue;
                    }

                    int numParameters = 0;
                    System.Int32 integer = (System.Int32)threadData.m_onFixedUpdateHandle;
                    int retValue = LuauPlugin.LuauCallMethodOnThread(threadData.m_threadHandle, new IntPtr(value: &integer), 0, numParameters);
                    if (retValue < 0) {
                        ThreadDataManager.Error(threadData.m_threadHandle);
                    }
                }
            }
        }

        public static void RunEndOfFrame() {
            //turn the list of s_objectKeys into a list of ints
            int numGameObjectIds = s_objectKeys.Count;
            int numDestroyedGameObjectIds = 0;

            // Resize arrays if necessary
            if (numGameObjectIds > listOfGameObjectIds.Array.Length) {
                listOfGameObjectIds.Resize(numGameObjectIds);
                listOfDestroyedGameObjectIds.Resize(numGameObjectIds);
            }
            int index = 0;
            Profiler.BeginSample("ListDestroyedObjects");
            foreach (var kvp in s_objectKeys) {
                listOfGameObjectIds.Array[index] = kvp.Key;
                if (kvp.Value is UnityEngine.Object unityObj && unityObj == null) {
                    if (s_debugging) {
                        Debug.Log("Destroyed GameObject: " + kvp.Key);
                    }
                    listOfDestroyedGameObjectIds.Array[numDestroyedGameObjectIds++] = kvp.Key;
                }
                index++;
            }
            Profiler.EndSample();

            try {
                IntPtr pointerToObjectsHandle = listOfGameObjectIds.AddrOfPinnedObject();
                IntPtr pointerToDestroyedObjectsHandle = listOfDestroyedGameObjectIds.AddrOfPinnedObject();
                
                Profiler.BeginSample("LuauRunEndFrameLogic");
                LuauPlugin.LuauRunEndFrameLogic(pointerToObjectsHandle, numGameObjectIds, pointerToDestroyedObjectsHandle, numDestroyedGameObjectIds);
            } finally {
                // No need to free handles here, as they are managed within the PinnedArray class.
                Profiler.EndSample();
            }

            Profiler.BeginSample("RemoveObjectKeys");
            for (int i = 0; i < numDestroyedGameObjectIds; i++) {
                var key = listOfDestroyedGameObjectIds.Array[i];
                s_objectKeys.Remove(key);
            }
            Profiler.EndSample();

            // Temporary removal process:
            s_removalList.Clear();
            
            Profiler.BeginSample("ClearThreads");
            foreach (var threadDataPair in m_threadData) {
                ThreadData threadData = threadDataPair.Value;
                bool zeroHandle = threadData.m_onUpdateHandle <= 0 && threadData.m_onLateUpdateHandle <= 0 && threadData.m_onFixedUpdateHandle <= 0;
                if (threadData.m_callbacks.Count == 0 && threadData.m_signalWrappers.Count == 0 && zeroHandle) {
                    s_removalList.Add(threadDataPair.Key);
                }
            }
            
            if (s_removalList.Count > 0) {
                Debug.Log($"Removing threads: {s_removalList.Count}");
                foreach (var threadKey in s_removalList) {
                    RemoveThreadData(threadKey);
                }
                s_workingListDirty = true;
            }
            Profiler.EndSample();

            //cleanup object references
            Profiler.BeginSample("CleanObjectReferences");
            foreach(int key in s_cleanUpKeys) {
                bool found = s_objectKeys.TryGetValue(key, out object obj);
                if (obj != null) {
                    s_reverseObjectKeys.Remove(obj);
                }
                s_objectKeys.Remove(key);
            }
            s_cleanUpKeys.Clear();
            Profiler.EndSample();
        }

        public static void SetThreadYielded(IntPtr thread, bool value) {
            ThreadData threadData = GetThreadData(thread);
            if (threadData != null) {
                threadData.m_yielded = value;
            }
        }

        private static void RemoveThreadData(IntPtr thread) {
            if (m_threadData.TryGetValue(thread, out var threadData)) {
                threadData.Destroy();
            }
            m_threadData.Remove(thread);
        }
    }
      
    public class ThreadData {
        public LuauContext m_context;
        public IntPtr m_threadHandle;
        public bool m_error = false;
        public bool m_yielded = false;
        public string m_debug;
        public List<Luau.CallbackWrapper> m_callbacks = new();
        public List<LuauSignalWrapper> m_signalWrappers = new();

        public int m_onUpdateHandle = -1;
        public int m_onLateUpdateHandle = -1;
        public int m_onFixedUpdateHandle = -1;

        //Things like Update, LateUpdate, and FixedUpdate need to be associated with a gameobject to check the Disabled flag
        public WeakReference<GameObject> associatedGameObject = null;

        public void Destroy() {
            foreach (var callbackWrapper in m_callbacks) {
                callbackWrapper.Destroy();
            }

            foreach (var signalWrapper in m_signalWrappers) {
                signalWrapper.Destroy();
            }
        }
    }

    public class PinnedArray<T> {
        public T[] Array { get; private set; }
        public GCHandle Handle { get; private set; }
        public bool IsPinned => Handle.IsAllocated;

        public PinnedArray(int size) {
            Array = new T[size];
            Handle = GCHandle.Alloc(Array, GCHandleType.Pinned);
        }

        public void Resize(int newSize) {
            if (IsPinned)
                Handle.Free();

            Array = new T[newSize];
            Handle = GCHandle.Alloc(Array, GCHandleType.Pinned);
        }

        public IntPtr AddrOfPinnedObject() {
            return Handle.AddrOfPinnedObject();
        }

        public void Free() {
            if (IsPinned)
                Handle.Free();
        }
    }

}
