using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine;

public class AirshipRuntimeScript : MonoBehaviour {
    // asset-related fields
    public AirshipScript scriptFile;
    [SerializeField][Obsolete("Do not use for referencing the script - use 'scriptFile'")]
    public string m_fileFullPath;
    [HideInInspector]
    public string m_shortFileName;

    // Thread specific fields
    [HideInInspector]
    public IntPtr m_thread = IntPtr.Zero;
    [NonSerialized] public LuauContext context = LuauContext.Game;
    
    // Thread states
    public bool m_error = false;
    public bool m_yielded = false;
    [HideInInspector]
    public bool m_canResume = false;
    [HideInInspector]
    public bool m_asyncYield = false;
    [HideInInspector]
    public int m_onUpdateHandle = -1;
    [HideInInspector]
    public int m_onLateUpdateHandle = -1;
    
    // coroutines
    protected List<IntPtr> m_pendingCoroutineResumes = new List<IntPtr>();
    internal void QueueCoroutineResume(IntPtr thread) {
        m_pendingCoroutineResumes.Add(thread);
    }
    
    protected static string CleanupFilePath(string path) {
        
        string extension = Path.GetExtension(path);

        if (extension == "") {
            // return path + ".lua";
            path += ".lua";
        }

        path = path.ToLower();
        if (path.StartsWith("assets/", StringComparison.Ordinal)) {
            path = path.Substring("assets/".Length);
        }
        
        return path;
    }

    protected bool cacheThread { get; }
    
    protected virtual IntPtr GetCachedThread(string path, int gameObjectId) {
        return IntPtr.Zero;
    }

    protected virtual void OnThreadCreated(IntPtr thread, string path) {
    }
    
    protected bool CreateThread() {
        if (m_thread != IntPtr.Zero) {
            return false;
        }
        
        if (!this.scriptFile.m_compiled) {
            if (string.IsNullOrEmpty(scriptFile.m_compilationError)) {
                throw new Exception($"{scriptFile.assetPath} cannot be executed due to not being compiled");
            }
            else {
                throw new Exception($"Cannot start script at {this.scriptFile.assetPath} with compilation errors: {this.scriptFile.m_compilationError}");
            }
        }
        
        var cleanPath = CleanupFilePath(this.scriptFile.m_path);
        m_shortFileName = Path.GetFileName(this.scriptFile.m_path);
        m_fileFullPath = this.scriptFile.m_path;
        
#if !UNITY_EDITOR || AIRSHIP_PLAYER
        var runtimeCompiledScriptFile = AssetBridge.GetBinaryFileFromLuaPath<AirshipScript>(this.scriptFile.m_path.ToLower());
        if (runtimeCompiledScriptFile) {
            this.scriptFile = runtimeCompiledScriptFile;
        } else {
            Debug.LogError($"Failed to find code.zip compiled script. Path: {this.scriptFile.m_path.ToLower()}, GameObject: {this.gameObject.name}", this.gameObject);
            return false;
        }
#endif

        LuauCore.CoreInstance.CheckSetup();
        int gameObjectId = ThreadDataManager.GetOrCreateObjectId(gameObject);

        // Use a cached thread if applicable
        var cachedThread = GetCachedThread(cleanPath, gameObjectId);
        if (cachedThread != IntPtr.Zero) {
            m_thread = cachedThread;
            return true;
        }
        
        
        // Create a new thread
        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(cleanPath); //Ok
        
        var gch = GCHandle.Alloc(this.scriptFile.m_bytes, GCHandleType.Pinned); //Ok
        m_thread = LuauPlugin.LuauCreateThread(context, gch.AddrOfPinnedObject(), this.scriptFile.m_bytes.Length, filenameStr, cleanPath.Length, gameObjectId, true);

        Marshal.FreeCoTaskMem(filenameStr);
        gch.Free();
        
        if (m_thread == IntPtr.Zero) {
            Debug.LogError("Script failed to compile" + m_shortFileName);
            m_canResume = false;
            m_error = true;

            return false;
        } else {
            LuauState.FromContext(context).AddThread(m_thread, this); //@@//@@ hmm is this even used anymore?
            m_canResume = true;
        }
        
        if (m_canResume) {
            var retValue = LuauCore.CoreInstance.ResumeScript(context, this);
            if (retValue == 1) {
                //We yielded
                m_canResume = true;
            } else {
                m_canResume = false;
                if (retValue == -1) {
                    m_error = true;
                } else {
                    // Handle thread creation callbacks
                    OnThreadCreated(m_thread, cleanPath);
                }
            }

        }
        return true;
    }
    
    
    #region Script Lifecycles
    public void Update() {
        if (m_error) {
            return;
        }

        //Run any pending coroutines that waiting last frame
     
        foreach (IntPtr coroutinePtr in m_pendingCoroutineResumes) {
            if (coroutinePtr == m_thread) {
                //This is us, we dont need to resume ourselves here,
                //just set the flag to do it.
                m_canResume = true;
                continue;
            }
            
            ThreadDataManager.SetThreadYielded(m_thread, false);
            int retValue = LuauPlugin.LuauRunThread(coroutinePtr);
          
            if (retValue == -1) {
                m_canResume = false;
                m_error = true;
                break;
            }
        }
        m_pendingCoroutineResumes.Clear();


        double time = Time.realtimeSinceStartupAsDouble;
        if (m_canResume && !m_asyncYield) {
            ThreadDataManager.SetThreadYielded(m_thread, false);
            int retValue = LuauCore.CoreInstance.ResumeScript(context, this);
            if (retValue != 1) {
                //we hit an error
                Debug.LogError("ResumeScript hit an error.", gameObject);
                m_canResume = false;
            }
            if (retValue == -1) {
                m_error = true;
            }

        }

        // double elapsed = (Time.realtimeSinceStartupAsDouble - time)*1000.0f;
    }
    #endregion
}