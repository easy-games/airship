using UnityEngine;
using UnityEditor;

using System.IO;
using System;
using System.Runtime.InteropServices;

[UnityEditor.AssetImporters.ScriptedImporter(1, "lua")]
public class SrtImporter : UnityEditor.AssetImporters.ScriptedImporter
{
    struct result
    {
        public IntPtr data;
        public long dataSize;
        public bool compiled;
    };

    public unsafe override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    {
 
        //Compile it 
        if (true)
        {
            string data = File.ReadAllText(ctx.assetPath) + "\r\n" + "\r\n";
            LuauCore.s_shutdown = false;
            LuauCore.Instance.CheckSetup();

            IntPtr filenameStr = Marshal.StringToCoTaskMemUTF8(ctx.assetPath);

            IntPtr dataStr = Marshal.StringToCoTaskMemUTF8(data);
    
            IntPtr res = LuauPlugin.LuauCompileCode(dataStr, data.Length, filenameStr, ctx.assetPath.Length, 1);

            Marshal.FreeCoTaskMem(dataStr);
            Marshal.FreeCoTaskMem(filenameStr);
         
            //Figure out what happened
            result resStruct = (result)Marshal.PtrToStructure(res, typeof(result));
            Debug.Log("Compilation of " + ctx.assetPath + ":" + resStruct.compiled.ToString());


            string ext = Path.GetExtension(ctx.assetPath);
            string fileName = ctx.assetPath.Substring(0, ctx.assetPath.Length - ext.Length) + ".bytes";
            
            Luau.BinaryFile subAsset = (Luau.BinaryFile)ScriptableObject.CreateInstance(typeof(Luau.BinaryFile));
             
            if (resStruct.compiled == false)
            {
                string resString = Marshal.PtrToStringUTF8(resStruct.data, (int)resStruct.dataSize);
                subAsset.m_compiled = false;
                subAsset.m_compilationError = resString;
                Debug.LogError("Failed to compile " + ctx.assetPath + ":" + resString);
            }
            else
            {
                subAsset.m_compiled = true;
                subAsset.m_compilationError = "none";
            }
          
            byte[] bytes = new byte[resStruct.dataSize];
            Marshal.Copy(resStruct.data, bytes, 0, (int)resStruct.dataSize);

          
            subAsset.m_bytes = bytes;

            if (subAsset.m_compiled == true)
            {
                ctx.AddObjectToAsset(fileName, subAsset, (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/gg.easy.airship/Editor/scriptOK.png", typeof(Texture2D)));
            }
            else
            {
                ctx.AddObjectToAsset(fileName, subAsset, (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/gg.easy.airship/Editor/scriptFAIL.png", typeof(Texture2D)));
            }

            ctx.SetMainObject(subAsset);

            LuauCore.ShutdownInstance();
        }
    }

 
}