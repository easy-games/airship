using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Airship.Editor {
    internal interface IExtendConfig<in T> {
        void Extend(T other);
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum JsModule {
        commonjs,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModuleResolution {
        Node,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModuleDetection {
        force,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ModuleTarget {
        ESNext,
    }

    public class TypescriptPluginConfig {
        public string transform;
    }

    public class TypescriptConfig : IExtendConfig<TypescriptConfig> {
        public class CompilerOptions : IExtendConfig<CompilerOptions>  {
            [CanBeNull] public string rootDir;
            [CanBeNull] public string[] rootDirs;
            [CanBeNull] public string baseUrl;
            [CanBeNull] public string outDir;

            public Dictionary<string, string[]> paths;
            
            // Compiler required
            public TypescriptPluginConfig[] plugins;
            public bool allowSyntheticDefaultImports = true;
            public bool downlevelIteration = true;
            public string jsx;
            public string jsxFactory;
            public string jsxFragmentFactory;
            public JsModule module = JsModule.commonjs;
            public ModuleResolution moduleResolution = ModuleResolution.Node;
            public bool noLib = true;
            public bool resolveJsonModule = true;
            public bool experimentalDecorators = true;
            public bool forceConsistentCasingInFileNames = true;
            public ModuleDetection ModuleDetection = ModuleDetection.force;
            public bool strict = true;
            public ModuleTarget target = ModuleTarget.ESNext;
            public string[] typeRoots;
            public bool? incremental;
            public string tsBuildInfoFile;
            public bool skipLibCheck = true;
            public bool? strictPropertyInitialization = true;
            
            public void Extend(CompilerOptions other) {
                rootDir ??= other.rootDir;
                rootDirs ??= other.rootDirs;
                baseUrl ??= other.baseUrl;
                outDir ??= other.outDir;
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]  
        public enum ProjectType {
            [EnumMember(Value = "bundle")]
            AirshipBundle,
            [EnumMember(Value = "game")]
            Game,
        }

        public class AirshipConfig {
            [JsonProperty("type")]
            public ProjectType ProjectType = ProjectType.Game;

            [JsonProperty("package")]
            public string PackageFolderPath = "Typescript~";

            [JsonProperty("runtimePath")]
            public string RuntimeFolderPath;
        }
        
        public CompilerOptions compilerOptions;
        
        public AirshipConfig airship;

        [JsonProperty("extends")]
        [CanBeNull] public string extendsPath;
        
        [CanBeNull] public string[] include;
        [CanBeNull] public string[] exclude;

        public TypescriptConfig(string directory) {
            Directory = directory;
            ConfigFilePath = Path.Join(directory, "tsconfig.json");
        }
        
        public void Extend(TypescriptConfig other) {
            compilerOptions.Extend(other.compilerOptions);
            include ??= other.include;
            exclude ??= other.exclude;
            airship ??= other.airship;
        }

        [JsonIgnore]
        public ProjectType AirshipProjectType {
            get {
                if (airship != null) {
                    return airship.ProjectType;
                }

                return ProjectType.Game;
            }
        }
        
        /// <summary>
        /// The directory of the project itself
        /// </summary>
        [JsonIgnore]
        public string Directory { get; private set; }
        
        /// <summary>
        /// The file path of this project file
        /// </summary>
        [JsonIgnore]
        public string ConfigFilePath { get; private set; }

        /// <summary>
        /// The output directory of this project
        /// </summary>
        [JsonIgnore]
        public string OutDir => Path.Join(Directory, compilerOptions.outDir).Replace("\\", "/");
        
        [JsonIgnore] [CanBeNull] public TypescriptConfig Extends { get; private set; }
        
        /// <summary>
        /// The root directories of this project
        /// </summary>
        [JsonIgnore]
        public string[] RootDirs {
            get {
                if (compilerOptions.rootDirs is {} rootDirs) {
                    return rootDirs.Select(dir => dir == "." ? Directory : $"{Directory}/{dir}").ToArray();
                } 
                
                if (compilerOptions.rootDir is { } rootDir) {
                    return new [] { rootDir == "." ? Directory : Directory + "/" + rootDir };
                }

                return new string[] { Directory };
            }
        }

        public static bool ExistsInDirectory(string dir, string tsconfig = "tsconfig.json") {
            var filePath = Path.Join(dir, tsconfig);
            return File.Exists(filePath);
        }

        public static bool FindInDirectory(string dir, out TypescriptConfig config, string tsconfig = "tsconfig.json") {
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
                
                var extendsConfig = ReadFromDirectory(Path.Join(dir, folder), file);
                config.Extend(extendsConfig);
            }
            
            return true;
        }

        public static TypescriptConfig ReadFromDirectory(string dir, string tsconfig = "tsconfig.json") {
            var filePath = Path.Join(dir, tsconfig);
            var config = JsonConvert.DeserializeObject<TypescriptConfig>(File.ReadAllText(filePath));
            config.Directory = dir;
            config.ConfigFilePath = filePath;
            
            if (config.extendsPath != null) {
                var folder = Path.GetDirectoryName(config.extendsPath);
                var file = Path.GetFileName(config.extendsPath);
                
                var extendsConfig = ReadFromDirectory(Path.Join(dir, folder), file);
                config.Extend(extendsConfig);
            }
            
            return config;
        }

        internal bool RemoveTransformer(string plugin) {
            var plugins = compilerOptions.plugins?.ToList() ?? new List<TypescriptPluginConfig>();
            var matchingPlugin = plugins.FirstOrDefault(pluginConfig => pluginConfig.transform == plugin);
            if (matchingPlugin == null) return false;
                
            plugins.Remove(matchingPlugin);
            compilerOptions.plugins = plugins.Count > 0 ? plugins.ToArray() : null;
            return true;
        }
        
        internal void Modify() {
            File.WriteAllText(ConfigFilePath, ToString());
        }
        
        public override string ToString() {
            var resultingJson = JsonConvert.SerializeObject(this, new JsonSerializerSettings() {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver() {
                    NamingStrategy = new CamelCaseNamingStrategy()
                }
            });
            return resultingJson;
        }
    }
}