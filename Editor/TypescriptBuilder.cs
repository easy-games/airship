using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System;

public class TypeScriptBuilder
{

    [MenuItem("EasyGG/Typescript/Rebuild All (pause)")]
    public static void BuildTypeScript()
    {
        string path = Application.dataPath + "/Game/Bedwars/Typescript~/";


        if (true)
        {
            Process process = new Process();
            process.StartInfo.FileName = "npm"; // or your command
            process.StartInfo.Arguments = "i && pause";
            process.StartInfo.WorkingDirectory = path;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.RedirectStandardOutput = false;
            process.Start();
            process.WaitForExit();

        }
        if (true)
        {
            Process process = new Process();
            process.StartInfo.FileName = "npm"; // or your command
            process.StartInfo.Arguments = "run build && pause";
            process.StartInfo.WorkingDirectory = path;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.RedirectStandardOutput = false;
            process.Start();
            process.WaitForExit();

        }

    }

    [MenuItem("EasyGG/Typescript/Rebuild All (no pause)")]
    public static void BuildTypeScriptNoPause()
    {
        string path = Application.dataPath + "/Game/Bedwars/Typescript~/";
        DateTime startTime = DateTime.Now;

        if (true)
        {
            Process process = new Process();
            process.StartInfo.FileName = "npm"; // or your command
            process.StartInfo.Arguments = "i";
            process.StartInfo.WorkingDirectory = path;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = false;
            process.Start();
            process.WaitForExit();

        }
        if (true)
        {
            Process process = new Process();
            process.StartInfo.FileName = "npm"; // or your command
            process.StartInfo.Arguments = "run build";
            process.StartInfo.WorkingDirectory = path;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = false;
            process.Start();
            process.WaitForExit();

        }
        UnityEngine.Debug.Log("Finished rebuilding TS in " + (DateTime.Now - startTime).TotalSeconds + " seconds");
    }

}