using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Assets.Code.Luau;
using Luau;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[AddComponentMenu("Airship/Airship Runtime Script")]
public class AirshipRuntimeScript : MonoBehaviour {
    // Injected from LuauHelper
    public static IAssetBridge AssetBridge;
    
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
    
    // Lifecycle stuffs
    private bool _hasInitEarly = false;
    [HideInInspector] protected bool started = false;
    protected bool _isAirshipComponent;
    private const bool ElevateToProtectedWithinCoreScene = true;
    protected bool _airshipScheduledToStart = false;
    public bool contextOverwritten = false;
    protected bool _airshipWaitingForLuauCoreReady = false;
    protected bool _scriptBindingStarted = false;
    protected bool _isDestroyed;
    
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

    protected void Awake() {
        LuauCore.CoreInstance.CheckSetup();
        LuauCore.onResetInstance += OnLuauReset;

        ScriptingEntryPoint.InvokeOnLuauStartup();
        
        InitEarly();
    }

    protected void Start() {
        if (scriptFile == null) {
            return;
        }
        
        _scriptBindingStarted = true;
        OnScriptStart();
        
        InitWhenCoreReady();
    }
    
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
    }

    protected bool IsDestroyed() {
        return _isDestroyed || this == null;
    }
    
    #endregion
    
    #region Script Init
    
    protected static bool IsReadyToStart() {
        return LuauCore.IsReady && SceneManager.GetActiveScene().name != "CoreScene";
    }
    

    public void Init() {
        if (IsDestroyed()) {
            DisconnectUnityEvents(); // Ensure any connected events are cleaned up
            return;
        }
        
        if (started) return;
        started = true;
        
        // Assume protected context for bindings within CoreScene
        if (!this.contextOverwritten && ((gameObject.scene.name is "CoreScene" or "MainMenu") || (SceneManager.GetActiveScene().name is "CoreScene" or "MainMenu")) && ElevateToProtectedWithinCoreScene) {
            context = LuauContext.Protected;
        }

        if (scriptFile == null) {
            Debug.LogWarning($"No script attached to ScriptBinding {gameObject.name}");
            return;
        }
        
        bool res = CreateThread();
    }
    
    public void InitEarly() {
        if (_hasInitEarly) {
            if (!started && IsReadyToStart()) {
                Init();
            }
            return;
        }
        _hasInitEarly = true;

        if (scriptFile == null && !string.IsNullOrEmpty(m_fileFullPath)) {
            scriptFile = LoadBinaryFileFromPath(m_fileFullPath);
            if (scriptFile == null) {
                Debug.LogWarning($"Failed to reconcile script from path \"{m_fileFullPath}\" on {name}", this.gameObject);
            }
        } else if (scriptFile == null && string.IsNullOrEmpty(m_fileFullPath)) {
            // No script to run; stop here.
            _hasInitEarly = false;
            return;
        }
        
        _isAirshipComponent = this.scriptFile != null && this.scriptFile.airshipBehaviour;
        InitWhenCoreReady();
    }
    
    protected void InitWhenCoreReady() {
        if (IsDestroyed()) {
            DisconnectUnityEvents(); // Ensure any connected events are cleaned up
            return;
        }
        
        if (IsReadyToStart()) {
            Init();
        } else {
            if (_isAirshipComponent && isActiveAndEnabled) {
                _airshipScheduledToStart = true;
            }
            AwaitCoreThenInit();
        }
    }
    
    protected void AwaitCoreThenInit() {
        _airshipWaitingForLuauCoreReady = true;
        LuauCore.OnInitialized += OnCoreInitialized;
        if (LuauCore.IsReady) {
            OnCoreInitialized();
        }
    }

    protected void OnLuauReset(LuauContext ctx) {
        if (ctx == context) {
            m_thread = IntPtr.Zero;
        }
    }
    
    private void OnActiveSceneChanged(Scene current, Scene next) {
        if (IsReadyToStart()) {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _airshipWaitingForLuauCoreReady = false;
            Init();
        }
    }
    
    protected void DisconnectUnityEvents() {
        LuauCore.onResetInstance -= OnLuauReset;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
    
    protected void OnCoreInitialized() {
        if (IsDestroyed()) {
            DisconnectUnityEvents(); // Ensure any connected events are cleaned up
            return;
        }
        
        LuauCore.OnInitialized -= OnCoreInitialized;
        if (IsReadyToStart()) {
            _airshipWaitingForLuauCoreReady = false;
            Init();
        } else {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }
    }
    
    #endregion
    
    #region Script Loading
    
    public bool CreateThreadFromPath(string fullFilePath, LuauContext context) {
        SetScriptFromPath(fullFilePath, context);
        if (scriptFile == null) {
            return false;
        }

        return CreateThread();
    }
    
    public AirshipScript LoadBinaryFileFromPath(string fullFilePath) {
        var cleanPath = CleanupFilePath(fullFilePath);
#if UNITY_EDITOR && !AIRSHIP_PLAYER
        this.m_fileFullPath = fullFilePath;
        return AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + cleanPath.Replace(".lua", ".ts")) 
               ?? AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + cleanPath); // as we have Luau files in core as well
#endif
        AirshipScript script = null;
        if (AssetBridge != null && AssetBridge.IsLoaded()) {
            try {
                var luaPath = cleanPath.Replace(".ts", ".lua");
                script = AssetBridge.LoadAssetInternal<AirshipScript>(luaPath);
            } catch (Exception e) {
                Debug.LogError($"Failed to load asset for script on GameObject \"{this.gameObject.name}\". Path: {fullFilePath}. Message: {e.Message}", gameObject);
                return null;
            }
        }

        return script;
    }
    
    public void SetScript(AirshipScript script, bool attemptStartup = false) {
        scriptFile = script;
        
        if (!string.IsNullOrEmpty(script.m_path))
            m_fileFullPath = script.m_path;

        if (Application.isPlaying && attemptStartup) {
            OnSetScriptStartup();
        }
    }   

    public void SetScriptFromPath(string path, LuauContext context, bool attemptStartup = false) {
        var script = LoadBinaryFileFromPath(path);
        if (script != null) {
            this.context = context;
            SetScript(script, attemptStartup);
        } else {
            Debug.LogError($"Failed to load script: {path}", this.gameObject);
        }
    }

    protected virtual void OnSetScriptStartup() {
        
    }
    
    #endregion
    
    #region Virtual Thread Methods
    protected virtual IntPtr GetCachedThread(string path, int gameObjectId) {
        return IntPtr.Zero;
    }

    protected virtual void OnThreadCreated(IntPtr thread, string path) {
    }

    protected virtual void OnScriptStart() {
    }

    #endregion
}
