using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Airship.Editor {
    internal interface IExtendConfig<in T> {
        void Extend(T other);
    }
    
    public class TypescriptConfig : IExtendConfig<TypescriptConfig> {
        public class CompilerOptions : IExtendConfig<CompilerOptions>  {
            [CanBeNull] public string rootDir;
            [CanBeNull] public string[] rootDirs;
            [CanBeNull] public string baseUrl;
            [CanBeNull] public string outDir;

            public void Extend(CompilerOptions other) {
                rootDir ??= other.rootDir;
                rootDirs ??= other.rootDirs;
                baseUrl ??= other.baseUrl;
                outDir ??= other.outDir;
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]  
        public enum ProjectType {
            AirshipBundle,
            Game,
        }

        public class AirshipConfig {
            public ProjectType ProjectType = ProjectType.Game;
        }
        
        public CompilerOptions compilerOptions;
        
        [Obsolete] [CanBeNull] public AirshipConfig rbxts;
        public AirshipConfig airship;

        [JsonProperty("extends")]
        [CanBeNull] public string extendsPath;
        
        [CanBeNull] public string[] include;
        [CanBeNull] public string[] exclude;

        public void Extend(TypescriptConfig other) {
            compilerOptions.Extend(other.compilerOptions);
            include ??= other.include;
            exclude ??= other.exclude;
            airship ??= other.airship;
            rbxts ??= other.rbxts;
        }

        public ProjectType AirshipProjectType {
            get {
                if (airship != null) {
                    return airship.ProjectType;
                } else if (rbxts != null) {
                    return rbxts.ProjectType;
                }

                return ProjectType.Game;
            }
        }
        
        /// <summary>
        /// The directory of the project itself
        /// </summary>
        public string Directory { get; private set; }
        
        /// <summary>
        /// The file path of this project file
        /// </summary>
        public string ConfigFilePath { get; private set; }

        /// <summary>
        /// The output directory of this project
        /// </summary>
        public string OutDir => Path.Join(Directory, compilerOptions.outDir).Replace("\\", "/");
        
        [CanBeNull] public TypescriptConfig Extends { get; private set; }
        
        /// <summary>
        /// The root directories of this project
        /// </summary>
        public string[] RootDirs {
            get {
                if (compilerOptions.rootDirs is {} rootDirs) {
                    return rootDirs.Select(dir => $"{Directory}/{dir}").ToArray();
                } 
                
                if (compilerOptions.rootDir is { } rootDir) {
                    return new [] { Directory + "/" + rootDir };
                }

                return new string[] { };
            }
        }

        public string GetOutputPath(string input) {
            foreach (var rootDir in RootDirs) {
                if (!input.StartsWith(rootDir)) continue;
                
                var output = input.Replace(rootDir, OutDir);
                return output.Replace(".ts", ".lua");
            }

            return input.Replace(".ts", ".lua");
        }

        public static bool FindTsConfig(string dir, out TypescriptConfig config, string tsconfig = "tsconfig.json") {
            var filePath = Path.Join(dir, tsconfig);
            if (!File.Exists(filePath)) {
                config = null;
                return false;
            }
            
            config = JsonConvert.DeserializeObject<TypescriptConfig>(File.ReadAllText(filePath));
            config.Directory = dir;
            config.ConfigFilePath = filePath;

            if (config.extendsPath != null) {
                var folder = Path.GetDirectoryName(config.extendsPath);
                var file = Path.GetFileName(config.extendsPath);
                
                var extendsConfig = ReadTsConfig(Path.Join(dir, folder), file);
                config.Extend(extendsConfig);
            }
            
            return true;
        }

        public static TypescriptConfig ReadTsConfig(string dir, string tsconfig = "tsconfig.json") {
            var filePath = Path.Join(dir, tsconfig);
            var config = JsonConvert.DeserializeObject<TypescriptConfig>(File.ReadAllText(filePath));
            config.Directory = dir;
            config.ConfigFilePath = filePath;
            
            if (config.extendsPath != null) {
                var folder = Path.GetDirectoryName(config.extendsPath);
                var file = Path.GetFileName(config.extendsPath);
                
                var extendsConfig = ReadTsConfig(Path.Join(dir, folder), file);
                config.Extend(extendsConfig);
            }
            
            return config;
        }
    }
}