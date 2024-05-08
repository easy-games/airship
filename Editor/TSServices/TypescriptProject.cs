using System.Collections.Generic;
using System.IO;
using CsToTs.TypeScript;

namespace Airship.Editor {
    public class TypescriptProject {
        public static IReadOnlyList<TypescriptProject> GetAllProjects() {
            List<TypescriptProject> projects = new();

            var typeScriptDirectories = TypeScriptDirFinder.FindTypeScriptDirectories();
            foreach (var directory in typeScriptDirectories) {
                TypescriptProject project = new TypescriptProject(directory);
                if (!project.IsCompilableTypescriptProject) continue;
                
                projects.Add(project);
            }

            return projects;
        }
        
        public string Directory {
            get;
        }

        public TypescriptConfig TsConfigJson => TypescriptConfig.ReadTsConfig(Directory);
        
        public PackageJson PackageJson => NodePackages.ReadPackageJson(Directory);
        public bool HasNodeModules => System.IO.Directory.Exists(Path.Join(Directory, "node_modules"));

        public bool HasCompiler => System.IO.Directory.Exists(Path.Join(Directory, "node_modules", "@easy-games/unity-ts"));

        private bool IsCompilableTypescriptProject =>
            PackageJson is { DevDependencies: not null } && (PackageJson.DevDependencies.ContainsKey("@easy-games/unity-ts") || PackageJson.Dependencies.ContainsKey("@easy-games/unity-ts"));
        
        public Semver CompilerVersion {
            get {
                var packageInfo = NodePackages.GetPackageInfo(Directory, "@easy-games/unity-ts");
                return Semver.Parse(packageInfo.Version);
            }
        }
        
        public Semver CompilerTypesVersion {
            get {
                var packageInfo = NodePackages.GetPackageInfo(Directory, "@easy-games/compiler-types");
                return Semver.Parse(packageInfo.Version);
            }
        }
        
        public Semver FlameworkVersion {
            get {
                var packageInfo = NodePackages.GetPackageInfo(Directory, "@easy-games/unity-flamework-transformer");
                return Semver.Parse(packageInfo.Version);
            }
        }
        
        public TypescriptProject(string directory) {
            this.Directory = directory;
        }
        
        public static bool operator==(TypescriptProject lhs, TypescriptProject rhs) {
            return lhs.Directory == rhs.Directory;
        }

        public static bool operator !=(TypescriptProject lhs, TypescriptProject rhs) {
            return lhs.Directory != rhs.Directory;
        }
    }
}