#if !UNITY_2019_1_OR_NEWER
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace VeryAnimation
{
    [ScriptedImporter(VeryAnimationWindow.AsmdefVersion, "asmdefChanger")]
    class AssemblyDefinitionChanger : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            ImportAsset(ctx.assetPath);
        }

        public static void Refresh()
        {
            foreach (var guid in AssetDatabase.FindAssets(checkNameStartWith))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetExtension(assetPath) == ".asmdefChanger")
                {
                    ImportAsset(assetPath);
                }
            }
        }

        private static string checkNameStartWith { get { return "AloneSoft." + typeof(AssemblyDefinitionChanger).Namespace; } }

        private static void ImportAsset(string importAssetPath)
        {
            var path = importAssetPath.Remove(importAssetPath.Length - Path.GetExtension(importAssetPath).Length);
            var name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith(checkNameStartWith))
                return;

            #region VersionCheck
            try
            {
                Func<string, int> ToVersion = (t) =>
                {
                    return Convert.ToInt32(Path.GetExtension(t).Remove(0, 1).Replace('_', '0'));
                };

                List<int> versions = new List<int>();
                {
                    var fullPath = Application.dataPath + importAssetPath.Remove(0, "Assets".Length);
                    var directoryPath = Path.GetDirectoryName(fullPath);
                    var dllNameWithoutExt = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(importAssetPath));
                    foreach (var p in Directory.GetFiles(directoryPath, "*.asmdefChanger"))
                    {
                        var subDllNameWithoutExt = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(p));
                        if (dllNameWithoutExt != subDllNameWithoutExt)
                            continue;

                        var subPath = p.Remove(p.Length - Path.GetExtension(p).Length);
                        var subVersion = ToVersion(subPath);
                        versions.Add(subVersion);
                    }
                    versions.Sort();
                }

                int targetVersion = -1;
                {
                    var editorVersion = Convert.ToInt32(Path.GetFileNameWithoutExtension(Application.unityVersion).Replace('.', '0'));
                    foreach (var ver in versions)
                    {
                        if (ver <= editorVersion)
                            targetVersion = ver;
                    }
                }
                if (targetVersion < 0)
                    return;

                var currentVersion = ToVersion(path);
                if (currentVersion != targetVersion)
                    return;
            }
            catch
            {
                return;
            }
            #endregion

            #region Change
            var originalAssetPath = importAssetPath;
            EditorApplication.delayCall += () =>
            {
                var nameExt = name + ".asmdef";
                foreach (var guid in AssetDatabase.FindAssets(name))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileName(assetPath) != nameExt)
                        continue;

                    var originalFullPath = Application.dataPath + originalAssetPath.Remove(0, "Assets".Length);
                    var fullPath = Application.dataPath + assetPath.Remove(0, "Assets".Length);
                    var originalFileInfo = new FileInfo(originalFullPath);
                    var fileInfo = new FileInfo(fullPath);
                    if (originalFileInfo.Length != fileInfo.Length)
                    {
                        FileUtil.DeleteFileOrDirectory(assetPath);
                        FileUtil.CopyFileOrDirectory(originalAssetPath, assetPath);

                        var importer = AssetImporter.GetAtPath(assetPath);
                        if (importer != null)
                            importer.SaveAndReimport();
                        AssetDatabase.Refresh();
                    }
                    break;
                }
            };
            #endregion
        }
    }
}
#endif