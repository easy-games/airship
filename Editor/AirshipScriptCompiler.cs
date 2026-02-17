using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Airship.Editor;
using Luau;

public class AirshipScriptCompiler {
    public static LuauCompiler.CompilationResult CompileScript(string assetPath) {
        var data = File.ReadAllText(assetPath);
        var filenameStr = Marshal.StringToCoTaskMemUTF8(assetPath);
        var dataStr = Marshal.StringToCoTaskMemUTF8(data);
        
        var len = Encoding.UTF8.GetByteCount(data);
        var res = LuauPlugin.CompileCode(dataStr, len, filenameStr, assetPath.Length, LuauPlugin.LuauOptimizationLevel.Baseline);
        
        Marshal.FreeCoTaskMem(dataStr);
        Marshal.FreeCoTaskMem(filenameStr);

        var compilationResult = Marshal.PtrToStructure<LuauCompiler.CompilationResult>(res);
        return compilationResult;
    }
    
    public static bool CompileAirshipScript(AirshipScript asset) {
        var outPath = TypescriptProjectsService.Project.GetOutputPath(asset.assetPath);
        var compilationResult = CompileScript(outPath);

        asset.m_path = FileExtensions.Transform(asset.assetPath, FileExtensions.Typescript, FileExtensions.Lua);
        
        if (compilationResult.Compiled) {
            var bytes = new byte[compilationResult.DataSize];
            Marshal.Copy(compilationResult.Data, bytes, 0, (int)compilationResult.DataSize);
            asset.m_bytes = bytes;

            // Perform reading metadata assoc. with this script
            var metadata = FileExtensions.Transform(outPath, FileExtensions.Lua,
                FileExtensions.AirshipComponentMeta);
            if (File.Exists(metadata)) {
                var json = File.ReadAllText(metadata);
                if (AirshipScriptMetadata.ParseScriptMetadata(json, out var scriptMetadata)) {
                    if (scriptMetadata.behaviour != null) {
                        asset.airshipBehaviour = true;
                        asset.scriptType = AirshipScriptType.Behaviour;
                        asset.m_metadata = scriptMetadata.behaviour;
                    } else if (scriptMetadata.scriptable != null) {
                        asset.scriptType = AirshipScriptType.ScriptableObject;
                        asset.m_metadata = scriptMetadata.scriptable;
                    } else {
                        asset.scriptType = AirshipScriptType.Script;
                    }
                    
                    if (scriptMetadata.serializables != null) {
                        asset.m_serializables = new LuauMetadata[scriptMetadata.serializables.Length];
                        for (var i = 0; i < asset.m_serializables.Length; i++) {
                            asset.m_serializables[i] = scriptMetadata.serializables[i];
                        }
                    }
                }
            }
        } else {
            var resString = Marshal.PtrToStringUTF8(compilationResult.Data, (int)compilationResult.DataSize);
            asset.m_compilationError = resString;
            asset.m_bytes = Array.Empty<byte>();
        }
        
        asset.m_compiled = compilationResult.Compiled;
        return compilationResult.Compiled;
    }
}