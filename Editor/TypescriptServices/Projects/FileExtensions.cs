using System;
using System.Collections.Generic;

namespace Airship.Editor {
    public enum InputFileType {
        /// <summary>
        /// <tt>.ts</tt> file
        /// </summary>
        Typescript,
        /// <summary>
        /// <tt>.tsx</tt> file
        /// </summary>
        TypescriptJsx,
    }
        
    public enum OutputFileType {
        /// <summary>
        /// <tt>.lua</tt> file
        /// </summary>
        Lua,
        /// <summary>
        /// <tt>.d.ts</tt> file
        /// </summary>
        TypescriptDeclaration,
        /// <summary>
        /// <tt>.lua.json~</tt> file
        /// </summary>
        AirshipComponentMetadata,
    }
    
    /// <summary>
    /// A static class for methods and constants around Airship projects
    /// </summary>
    public static class FileExtensions {
        public const string TypescriptDeclaration = "d.ts";
        public const string Lua = "lua";
        public const string AirshipComponentMeta = "lua.json~";
        public const string Typescript = "ts";
        public const string TypescriptJsx = "tsx";

        public static string Transform(string filePath, string from, string to) {
            if (!from.StartsWith("."))
                from = "." + from;

            if (!to.StartsWith("."))
                to = "." + to;
            
            return filePath.Replace(from, to);
        }

        public static bool EndsWith(string filePath, string ext) {
            if (!ext.StartsWith("."))
                ext = "." + ext;
            
            return filePath.EndsWith(ext);
        }
        
        public static string GetExtensionForInputType(InputFileType inputFileType) {
            return inputFileType switch {
                InputFileType.Typescript => Typescript,
                InputFileType.TypescriptJsx => TypescriptJsx,
                _ => throw new ArgumentOutOfRangeException(nameof(inputFileType), inputFileType, null)
            };
        }
        
        public static string GetExtensionForOutputType(OutputFileType outputFileType) {
            return outputFileType switch {
                OutputFileType.Lua => Lua,
                OutputFileType.TypescriptDeclaration => TypescriptDeclaration,
                OutputFileType.AirshipComponentMetadata => AirshipComponentMeta,
                _ => throw new ArgumentOutOfRangeException(nameof(outputFileType), outputFileType, null)
            };
        }
    }
}