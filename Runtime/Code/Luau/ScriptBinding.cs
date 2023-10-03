using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Luau;
using UnityEditor;
using UnityEngine.Profiling;
using UnityEngine;

public class ScriptBinding : MonoBehaviour
{
    //public TextAsset m_luaScript;

    public string m_fileFullPath;
    public bool m_error = false;
    public bool m_yielded = false;

#if UNITY_EDITOR
    public string m_assetPath;
    public BinaryFile m_binaryFile;
#endif

    [HideInInspector] private bool started = false;

    [HideInInspector]
    public bool m_canResume = false;
    [HideInInspector]
    public bool m_asyncYield = false;
    [HideInInspector]
    public IntPtr m_thread = IntPtr.Zero;
    [HideInInspector]
    public int m_onUpdateHandle = -1;
    [HideInInspector]
    public int m_onLateUpdateHandle = -1;

    [HideInInspector]
    public string m_shortFileName;
    private byte[] m_fileContents;

    private List<IntPtr> m_pendingCoroutineResumes = new List<IntPtr>();
    
    [HideInInspector]
    public LuauMetadata m_metadata = new();
    
    // Injected from LuauHelper
    public static IAssetBridge AssetBridge;

    public void Error()
    {
        m_error = true;
        m_canResume = false;
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (AssetBridge == null) return;
        var binaryFile = AssetDatabase.LoadAssetAtPath<BinaryFile>(m_assetPath);
        if (binaryFile == null) return;
        m_binaryFile = binaryFile;
        ReconcileMetadata();
    }

    private void ReconcileMetadata()
    {
        Debug.Log("Reconciling metadata");
        if (m_binaryFile == null)
        {
            m_metadata.properties.Clear();
            return;
        }
        
        // Add missing properties:
        foreach (var property in m_binaryFile.m_metadata.properties)
        {
            var serializedProperty = m_metadata.FindProperty<object>(property.name);
            if (serializedProperty == null)
            {
                m_metadata.properties.Add(property.Clone());
            }
        }

        // Remove properties that are no longer used:
        List<LuauMetadataProperty> propertiesToRemove = null;
        foreach (var serializedProperty in m_metadata.properties)
        {
            var property = m_binaryFile.m_metadata.FindProperty<object>(serializedProperty.name);
            if (property == null)
            {
                if (propertiesToRemove == null)
                {
                    propertiesToRemove = new List<LuauMetadataProperty>();
                }
                propertiesToRemove.Add(serializedProperty);
            }
        }
        if (propertiesToRemove != null)
        {
            foreach (var serializedProperty in propertiesToRemove)
            {
                m_metadata.properties.Remove(serializedProperty);
            }
        }
    }
#endif
    
    private void Start() {
        StartCoroutine(this.LateStart());
    }

    private IEnumerator LateStart() {
        yield return null;
        this.Init();
    }

    public void Init() {
        if (this.started) return;
        this.started = true;

        //Just dont do anything if empty
        if (m_fileFullPath == "")
        {
            return;
        }

        var binaryFile = AssetBridge.LoadAssetInternal<BinaryFile>(m_fileFullPath);

        Profiler.BeginSample("LuauBinding.Start");
        bool res = CreateThread(m_fileFullPath);
        Profiler.EndSample();
    }

    public string CleanupFilePath(string path)
    {

        string extension = System.IO.Path.GetExtension(path);

        if (extension == "")
        {
            return path + ".lua";
        }
        /*
         string noExtension = path.Substring(0, path.Length - extension.Length);

         if (noExtension.StartsWith("Assets/Resources/"))
         {
             noExtension = noExtension.Substring(new String("Assets/Resources/").Length);
         }

         if (noExtension.StartsWith("/"))
         {
             noExtension = noExtension.Substring(1);
         }

         return noExtension;*/
        return path;
    }


    public string CleanupFilePathForResourceSystem(string path)
    {

        string extension = System.IO.Path.GetExtension(path);

        string noExtension = path.Substring(0, path.Length - extension.Length);

        if (noExtension.StartsWith("Assets/Resources/"))
        {
            noExtension = noExtension.Substring(new string("Assets/Resources/").Length);
        }
        if (noExtension.StartsWith("Resources/"))
        {
            noExtension = noExtension.Substring(new string("Resources/").Length);
        }

        if (noExtension.StartsWith("/"))
        {
            noExtension = noExtension.Substring(1);
        }

        return noExtension;
    }

    public bool CreateThread(string fullFilePath)
    {
        if (m_thread != IntPtr.Zero)
        {
            return false;
        }

        Profiler.BeginSample("CleanupFilePath");
        string cleanPath = CleanupFilePath(fullFilePath);
        Profiler.EndSample();
        //print("clean path: " + cleanPath);

        Profiler.BeginSample("LoadScriptAsset");
        Luau.BinaryFile script = null;
        if (AssetBridge.IsLoaded() == true)
        {
            try
            {
                script = AssetBridge.LoadAssetInternal<Luau.BinaryFile>(cleanPath);
            } catch (Exception e)
            {
                Debug.LogError($"Failed to load asset for script. fullFilePath: {fullFilePath}, thread: {m_fileFullPath}, message: " + e.Message);
                Profiler.EndSample();
                return false;
            }
        }
        else
        {
            //fallback for editor mode
            string path = CleanupFilePathForResourceSystem(fullFilePath);

            script = Resources.Load<Luau.BinaryFile>(path);
        }
        Profiler.EndSample();

        if (script == null)
        {
            Debug.LogError("Asset " + fullFilePath + " not found");
            return false;
        }
        m_shortFileName = System.IO.Path.GetFileName(fullFilePath);
        m_fileFullPath = fullFilePath;
        //m_fileContents = script.text + "\r\n" + "\r\n";
        //m_fileContents = script.m_bytes;

        LuauCore core = LuauCore.Instance;
        core.CheckSetup();


        Profiler.BeginSample("Marshal");
        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(cleanPath);
        Profiler.EndSample();
        //IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(m_fileContents);
        Profiler.BeginSample("GCHandle.Alloc");
        var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned);
        Profiler.EndSample();

        //trickery, grab the id before we know the thread
        int id = ThreadDataManager.GetOrCreateObjectId(gameObject);

        Profiler.BeginSample("LuauCreateThread");
        m_thread = LuauPlugin.LuauCreateThread(gch.AddrOfPinnedObject(), script.m_bytes.Length, filenameStr, cleanPath.Length, id, true);
        Profiler.EndSample();
        //Debug.Log("Thread created " + m_thread.ToString("X") + " :" + fullFilePath);

        Profiler.BeginSample("MarshalFree");
        Marshal.FreeCoTaskMem(filenameStr);
        //Marshal.FreeCoTaskMem(dataStr);
        gch.Free();
        Profiler.EndSample();

        if (m_thread == IntPtr.Zero)
        {
            Debug.LogError("Script failed to compile" + m_shortFileName);
            m_canResume = false;
            m_error = true;

            return false;
        }
        else
        {
            Profiler.BeginSample("ThreadDataManager.AddObjectReference");
            ThreadDataManager.AddObjectReference(m_thread, gameObject);
            core.AddThread(m_thread, this); //@@//@@ hmm is this even used anymore?
            m_canResume = true;
            Profiler.EndSample();
        }

        if (m_canResume)
        {
            Profiler.BeginSample("ResumeScript");
            int retValue = LuauCore.Instance.ResumeScript(this);
            //Debug.Log("Thread result:" + retValue);
            if (retValue == 1)
            {
                //We yielded
                m_canResume = true;
            }
            else
            {
                m_canResume = false;
                if (retValue == -1)
                {
                    m_error = true;
                }
            }
            Profiler.EndSample();
            

        }
        return true;
    }

    unsafe public void Update()
    {

        if (m_error == true)
        {
            return;
        }

        //Run any pending coroutines that waiting last frame
     
        foreach (IntPtr coroutinePtr in m_pendingCoroutineResumes)
        {
            if (coroutinePtr == m_thread)
            {
                //This is us, we dont need to resume ourselves here,
                //just set the flag to do it.
                m_canResume = true;
                continue;
            }
            
            ThreadDataManager.SetThreadYielded(m_thread, false);
            int retValue = LuauPlugin.LuauRunThread(coroutinePtr);
          
            if (retValue == -1)
            {
                m_canResume = false;
                m_error = true;
                break;
            }
        }
        m_pendingCoroutineResumes.Clear();


        double time = Time.realtimeSinceStartupAsDouble;
        if (m_canResume && !m_asyncYield)
        {
            ThreadDataManager.SetThreadYielded(m_thread, false);
            int retValue = LuauCore.Instance.ResumeScript(this);
            if (retValue != 1)
            {
                //we hit an error
                m_canResume = false;
            }
            if (retValue == -1)
            {
                m_error = true;
            }

        }

        // double elapsed = (Time.realtimeSinceStartupAsDouble - time)*1000.0f;
        //Debug.Log("execution: " + elapsed  + "ms");
    }

 
    public void QueueCoroutineResume(IntPtr thread)
    {
        m_pendingCoroutineResumes.Add(thread);
    }

    private void OnDestroy()
    {
       
        LuauCore core = LuauCore.Instance;
      
        if (m_thread != IntPtr.Zero)
        {
          //  LuauPlugin.LuauDestroyThread(m_thread); //TODO FIXME - Crashes on app shutdown? (Is already fixed I think)
            m_thread = IntPtr.Zero;
        }

    }
  
}
