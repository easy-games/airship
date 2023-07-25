using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Luau;
using UnityEngine.Profiling;
using UnityEngine;

public class LuauBinding : MonoBehaviour
{
    //public TextAsset m_luaScript;

    public string m_fileFullPath;
    public bool m_error = false;
    public bool m_yielded = false;

    [HideInInspector]
    public bool m_canResume = false;
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

    public void Error()
    {
        m_error = true;
        m_canResume = false;
    }

    public void Init()
    {
        print($"INIT {m_fileFullPath}");
        //Just dont do anything if empty
        if (m_fileFullPath == "")
        {
            return;
        }

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
            noExtension = noExtension.Substring(new String("Assets/Resources/").Length);
        }
        if (noExtension.StartsWith("Resources/"))
        {
            noExtension = noExtension.Substring(new String("Resources/").Length);
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
        IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(m_shortFileName);
        Profiler.EndSample();
        //IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(m_fileContents);
        Profiler.BeginSample("GCHandle.Alloc");
        var gch = GCHandle.Alloc(script.m_bytes, GCHandleType.Pinned);
        Profiler.EndSample();

        //trickery, grab the id before we know the thread
        int id = ThreadDataManager.GetOrCreateObjectId(gameObject);

        Profiler.BeginSample("LuauCreateThread");
        m_thread = LuauPlugin.LuauCreateThread(gch.AddrOfPinnedObject(), script.m_bytes.Length, filenameStr, m_shortFileName.Length, id, true);
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
        if (m_canResume)
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

        double elapsed = (Time.realtimeSinceStartupAsDouble - time)*1000.0f;
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
