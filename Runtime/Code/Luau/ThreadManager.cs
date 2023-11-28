
//Any time a thread is passed a C# 'object', 
using System;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace Luau
{
     public static class ThreadDataManager
    {
        private static Dictionary<IntPtr, ThreadData> m_threadData = new();

        private static int s_threadDataSize = 128;
        private static ThreadData[] s_workingList = new ThreadData[128];
        private static bool s_workingListDirty = true;

        private static List<IntPtr> s_removalList = new List<IntPtr>(8);


        public static ThreadData GetThreadDataByPointer(IntPtr thread)
        {
            m_threadData.TryGetValue(thread, out ThreadData threadData);
            return threadData;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup()
        {
            OnReset();
        }

        public static void OnReset()
        {
            s_workingListDirty = true;
            m_threadData.Clear();
            s_objectKeys.Clear();
            s_reverseObjectKeys.Clear();
            s_cleanUpKeys.Clear();
            s_debuggingKeys.Clear();
            s_keyGen = 0;
        }

        public class KeyHolder : System.Object
        {
            public KeyHolder(int p)
            {
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

        public static int GetOrCreateObjectId(System.Object obj)
        {
            bool found = s_reverseObjectKeys.TryGetValue(obj, out int key);
            if (found)
            {
                return key;
            }
            else
            {

                s_keyGen += 1;
                //Only place objects get added to the dictionary
                s_objectKeys.Add(s_keyGen, obj);
                s_reverseObjectKeys.Add(obj, s_keyGen);

                if (s_debugging == true)
                {
                    Debug.Log("GC add reference to " + obj + " id: " + s_keyGen);
                    s_debuggingKeys.Add(s_keyGen, obj);
                }


                return s_keyGen;
            }
        }

        private static ThreadData GetOrCreateThreadData(IntPtr thread, string debugString)
        {
            bool found = m_threadData.TryGetValue(thread, out ThreadData threadData);
            if (found == false)
            {
                threadData = new ThreadData();
                threadData.m_threadHandle = thread;
                threadData.m_debug = debugString;
                m_threadData.Add(thread, threadData);
                s_workingListDirty = true;
            }
            return threadData;
        }

        private static ThreadData GetThreadData(IntPtr thread)
        {
            bool found = m_threadData.TryGetValue(thread, out ThreadData threadData);
            return threadData;
        }

        public static int AddObjectReference(IntPtr thread, System.Object obj)
        {
            int id = GetOrCreateObjectId(obj);
            return id;
        }

        public static System.Object GetObjectReference(IntPtr thread, int instanceId)
        {
            bool res = s_objectKeys.TryGetValue(instanceId, out System.Object value);
            if (!res)
            {
                if (s_debugging == false)
                {    
                    Debug.LogError("Object reference not found: " + instanceId + " Object id was never assigned, where did you get this key from?! Currently highest assigned key is " + s_keyGen + " thread " + thread);
                }
                else
                if (s_debugging == true)
                {
                    bool found = s_debuggingKeys.TryGetValue(instanceId, out object debugObject);
                    
                    if (found == false)
                    {
                        Debug.LogError("Object reference not found: " + instanceId + " Object id was never assigned, where did you get this key from?! Currently highest assigned key is " + s_keyGen + " thread "+ thread);
                    }
                    else
                    {
                        Debug.LogError("Object reference not found: " + instanceId + " Object was created, and then signaled for garbage collection from luau." + debugObject.ToString() + " Currently highest assigned key is " + s_keyGen + " thread " + thread);
                    }
                   
                }
                LuauPlugin.LuauGetDebugTrace(thread);
                return null;
            }

            return value;
        }

        public static void DeleteObjectReference(int instanceId)
        {
            if (s_debugging == true)
            {
                Debug.Log("GC removed reference to " + instanceId);
            }
            //Wait til end of frame to clean it up
            s_cleanUpKeys.Add(instanceId);
        }

        public static Luau.CallbackWrapper RegisterCallback(IntPtr thread, System.Object host, int handle, string methodName)
        {
            ThreadData threadData = GetOrCreateThreadData(thread, "RegisterCallback");

            Luau.CallbackWrapper callback = new Luau.CallbackWrapper(thread, methodName, handle);
            threadData.m_callbacks.Add(callback);

            return callback;
        }


        public static void Error(IntPtr thread)
        {
            ThreadData threadData = GetThreadData(thread);
            if (threadData == null)
            {
                return;
            }
            threadData.m_error = true;
        }

        public static void SetOnUpdateHandle(IntPtr thread, int handle, GameObject gameObject)
        {
            ThreadData threadData = GetOrCreateThreadData(thread, "SetOnUpdateHandle");
            threadData.associatedGameObject = new WeakReference<GameObject>(gameObject);
            threadData.m_onUpdateHandle = handle;
        }
        
        public static void SetOnLateUpdateHandle(IntPtr thread, int handle, GameObject gameObject)
        {
            ThreadData threadData = GetOrCreateThreadData(thread, "SetOnLateUpdateHandle");
            threadData.associatedGameObject = new WeakReference<GameObject>(gameObject);
            threadData.m_onLateUpdateHandle = handle;
        }
        
        public static void SetOnFixedUpdateHandle(IntPtr thread, int handle, GameObject gameObject)
        {
            ThreadData threadData = GetOrCreateThreadData(thread, "SetOnFixedUpdateHandle");
            threadData.associatedGameObject = new WeakReference<GameObject>(gameObject);
            threadData.m_onFixedUpdateHandle = handle;
        }


        private static int UpdateWorkingList()
        {
            int count = m_threadData.Count;
            if (s_workingListDirty == false)
            {
                return count;
            }
            s_workingListDirty = false;

            if (count > s_threadDataSize)
            {
                s_threadDataSize = count * 2;
                s_workingList = new ThreadData[s_threadDataSize];
            }
          
            var enumerator = m_threadData.GetEnumerator();
            int i = 0;
            while (enumerator.MoveNext())
            {
                s_workingList[i++] = enumerator.Current.Value;
            }
            
            return count;
        }
        
        public unsafe static void InvokeUpdate()
        {

            int count = UpdateWorkingList();

            for (int i = 0; i < count; i++)
            {
                ThreadData threadData = s_workingList[i];
                
                if (threadData.m_onUpdateHandle > 0 && threadData.m_yielded == false && threadData.m_error == false)
                {
                    //Check the gameobject for enabled
                    if (threadData.associatedGameObject.TryGetTarget(out GameObject gameObject) == false)
                    {
                        threadData.m_onUpdateHandle = 0;
                        continue;
                    }
                    if (gameObject.activeInHierarchy == false)
                    {
                        continue;
                    }
                    
                    int numParameters = 0;
                    System.Int32 integer = (System.Int32)threadData.m_onUpdateHandle;
                    int retValue = LuauPlugin.LuauCallMethodOnThread(threadData.m_threadHandle, new IntPtr(value: &integer), 0, numParameters);
                    if (retValue < 0)
                    {
                        ThreadDataManager.Error(threadData.m_threadHandle);
                    }
                }
            }
        }

        public unsafe static void InvokeLateUpdate()
        {
            int count = UpdateWorkingList();

            for (int i = 0; i < count; i++)
            {
                ThreadData threadData = s_workingList[i];
                if (threadData.m_onLateUpdateHandle > 0 && threadData.m_yielded == false && threadData.m_error == false)
                {
                    //Check the gameobject for enabled
                    if (threadData.associatedGameObject.TryGetTarget(out GameObject gameObject) == false)
                    {
                        threadData.m_onUpdateHandle = 0;
                        continue;
                    }
                    if (gameObject.activeInHierarchy == false)
                    {
                        continue;
                    }
                    
                    int numParameters = 0;
                    System.Int32 integer = (System.Int32)threadData.m_onLateUpdateHandle;
                    int retValue = LuauPlugin.LuauCallMethodOnThread(threadData.m_threadHandle, new IntPtr(value: &integer), 0, numParameters);
                    if (retValue < 0)
                    {
                        ThreadDataManager.Error(threadData.m_threadHandle);
                    }
                }
            }
        }
        
        public unsafe static void InvokeFixedUpdate()
        {
            int count = UpdateWorkingList();

            for (int i = 0; i < count; i++)
            {
                ThreadData threadData = s_workingList[i];

                if (threadData.m_onFixedUpdateHandle > 0 && threadData.m_yielded == false && threadData.m_error == false)
                {
                    //Check the gameobject for enabled
                    if (threadData.associatedGameObject.TryGetTarget(out GameObject gameObject) == false)
                    {
                        threadData.m_onUpdateHandle = 0;
                        continue;
                    }
                    if (gameObject.activeInHierarchy == false)
                    {
                        continue;
                    }

                    int numParameters = 0;
                    System.Int32 integer = (System.Int32)threadData.m_onFixedUpdateHandle;
                    int retValue = LuauPlugin.LuauCallMethodOnThread(threadData.m_threadHandle, new IntPtr(value: &integer), 0, numParameters);
                    if (retValue < 0)
                    {
                        ThreadDataManager.Error(threadData.m_threadHandle);
                    }
                }
            }
        }

        public static void RunEndOfFrame()
        {
            //turn the list of s_objectKeys into a list of ints
            int numGameObjectIds = s_objectKeys.Count;
            int numDestoyedGameObjectIds = 0;
            if (numGameObjectIds > 0)   //Todo: Run this less frequently, or only run it on a section of the known objects - we're in no rush to do this every frame
            {
                int[] listOfGameObjectIds = new int[numGameObjectIds];
                int[] listOfDestroyedGameObjectIds = new int[numGameObjectIds];
                
                int index = 0;
                foreach (var kvp in s_objectKeys)
                {
                    listOfGameObjectIds[index++] = kvp.Key;

                    if (kvp.Value is UnityEngine.Object unityObj)
                    {
                        // Use UnityEngine.Object's override of the == operator
                        if (unityObj == null) // It's been destroyed!
                        {
                            if (s_debugging)
                            {
                                Debug.Log("Destroyed GameObject: " + kvp.Key);
                            }
                            listOfDestroyedGameObjectIds[numDestoyedGameObjectIds++] = kvp.Key;
                        }
                    }
                }

                // Pin the array of GameObject IDs so that it doesn't get moved by the GC
                GCHandle objectsHandle = GCHandle.Alloc(listOfGameObjectIds, GCHandleType.Pinned); //Ok
                GCHandle destroyedObjectsHandle = GCHandle.Alloc(listOfDestroyedGameObjectIds, GCHandleType.Pinned); //Ok

                try
                {
                    // Get a pointer to the first element of the array
                    IntPtr pointerToObjectsHandle = objectsHandle.AddrOfPinnedObject();
                    IntPtr pointerToDestoyedObjectsHandle = destroyedObjectsHandle.AddrOfPinnedObject();

                    // Now you can pass this pointer to the unmanaged code
                    //Debug.Log("Reporting " + numGameObjectIds + " game objects");
                    LuauPlugin.LuauRunEndFrameLogic(pointerToObjectsHandle, numGameObjectIds, pointerToDestoyedObjectsHandle, numDestoyedGameObjectIds);
                }
                finally
                {
                    // Make sure to free the handle to prevent memory leaks
                    if (objectsHandle.IsAllocated)
                        objectsHandle.Free();
                    if (destroyedObjectsHandle.IsAllocated) 
                        destroyedObjectsHandle.Free();
                }

                //All of the objects in the listOfDestroyedGameObjectIds have been reported as destroyed, clean up!
                for (int i = 0; i < numDestoyedGameObjectIds; i++)
                {
                    s_objectKeys.Remove(listOfDestroyedGameObjectIds[i]);
                }

                //Debug.Log("Num alive keys" + s_objectKeys.Count);
            }
            
            // Temporary removal process:
            s_removalList.Clear();
            
            foreach (var threadDataPair in m_threadData)
            {
                ThreadData threadData = threadDataPair.Value;
                bool zeroHandle = threadData.m_onUpdateHandle <= 0 && threadData.m_onLateUpdateHandle <= 0 && threadData.m_onFixedUpdateHandle <= 0;
                if (threadData.m_callbacks.Count == 0 && zeroHandle) {
                    s_removalList.Add(threadDataPair.Key);
                }
            }
            
            if (s_removalList.Count > 0)
            {
                Debug.Log($"Removing threads: {s_removalList.Count}");
                foreach (var threadKey in s_removalList)
                {
                    RemoveThreadData(threadKey);
                }
                s_workingListDirty = true;
            }

            //cleanup object references
            foreach(int key in s_cleanUpKeys)
            {
                bool found = s_objectKeys.TryGetValue(key, out object obj);
                if (obj != null)
                {
                    s_reverseObjectKeys.Remove(obj);
                }
                s_objectKeys.Remove(key);
            }
            s_cleanUpKeys.Clear();
           
        }

        public static void SetThreadYielded(IntPtr thread, bool value)
        {
            ThreadData threadData = GetThreadData(thread);
            if (threadData != null)
            {
                threadData.m_yielded = value;
            }
        }

        private static void RemoveThreadData(IntPtr thread)
        {
            if (m_threadData.TryGetValue(thread, out var threadData))
            {
                threadData.Destroy();
            }
            m_threadData.Remove(thread);
        }
    }
      
    public class ThreadData
    {
        public IntPtr m_threadHandle;
        public bool m_error = false;
        public bool m_yielded = false;
        public string m_debug;
        public List<Luau.CallbackWrapper> m_callbacks = new();

        public int m_onUpdateHandle = -1;
        public int m_onLateUpdateHandle = -1;
        public int m_onFixedUpdateHandle = -1;

        //Things like Update, LateUpdate, and FixedUpdate need to be associated with a gameobject to check the Disabled flag
        public WeakReference<GameObject> associatedGameObject = null;

        public void Destroy()
        {
            foreach (var callbackWrapper in m_callbacks)
            {
                callbackWrapper.Destroy();
            }
        }
    }

}