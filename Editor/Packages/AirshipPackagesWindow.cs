using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Code.GameBundle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using ZipFile = Unity.VisualScripting.IonicZip.ZipFile;

namespace Editor.Packages {
    public class AirshipPackagesWindow : EditorWindow {
        private GameConfig gameConfig;

        [MenuItem ("Window/Airship Packages")]
        public static void ShowWindow () {
            EditorWindow.GetWindow(typeof(AirshipPackagesWindow));
        }

        private void OnEnable() {
            this.gameConfig = GameConfig.Load();
        }

        private void OnGUI() {
            GUILayout.Label("Packages", EditorStyles.largeLabel);
            AirshipEditorGUI.HorizontalLine();
            foreach (var package in this.gameConfig.packages) {
                GUILayout.BeginHorizontal();
                GUILayout.Label(package.id);
                if (package.localSource) {
                    var localSourceStyle = new GUIStyle(GUI.skin.label);
                    localSourceStyle.fontStyle = FontStyle.Italic;
                    GUILayout.Label("Local Source", localSourceStyle);
                    if (GUILayout.Button("Publish")) {
                        this.PublishPackage(package);
                    }
                } else {
                    if (GUILayout.Button("Redownload")) {
                        this.DownloadPackage(package.id, package.version);
                    }
                    GUILayout.Space(5);
                    if (GUILayout.Button("Update to Latest")) {
                        this.UpdateToLatest(package.id);
                    }
                }
                GUILayout.EndHorizontal();
                AirshipEditorGUI.HorizontalLine();
            }
        }

        public void PublishPackage(AirshipPackageDocument packageDoc) {
            List<AssetBundleBuild> builds = new();

            string[] assetBundleFiles = new[] {
                "shared/resources",
                "shared/scenes",
                "client/resources",
                "client/scenes",
                "server/resources",
                "server/scenes"
            };
            foreach (var assetBundleFile in assetBundleFiles) {
                var name = $"{packageDoc.id}_${assetBundleFile}";
                var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(name);
                builds.Add(new AssetBundleBuild() {
                    assetBundleName = packageDoc.id + "_" + assetBundleFile,
                    assetNames = assetPaths
                });
            }
            BuildPipeline.BuildAssetBundles(AssetBridge.PackagesPath, $"{packageDoc.id}_v{packageDoc.version}", )

            var importsFolder = Path.Join("Assets", "Bundles", "Imports");
            var sourceAssetsFolder = Path.Join(importsFolder, packageDoc.id);
            var typesFolder = Path.Join(Path.Join("Assets", "Bundles", "Types~"), packageDoc.id);

            var sourceAssetsZip = new ZipFile();
            sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Client"), "Client");
            sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Shared"), "Shared");
            sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Server"), "Server");
            sourceAssetsZip.AddDirectory(typesFolder, "Types");

            var zippedSourceAssetsZipPath = Path.Join(Application.persistentDataPath, "Uploads", packageDoc.id + ".zip");
            if (Directory.Exists(zippedSourceAssetsZipPath)) {
                Directory.Delete(zippedSourceAssetsZipPath);
            }
            sourceAssetsZip.Save(zippedSourceAssetsZipPath);

            List<IMultipartFormSection> formData = new()
            {
                new MultipartFormDataSection("packageId", packageDoc.id),
            };

            var bytes = File.ReadAllBytes(zippedSourceAssetsZipPath);
            formData.Add(new MultipartFormFileSection(
                "sourceZip",
                bytes,
                zippedSourceAssetsZipPath,
                "multipart/form-data")
            );

            UnityWebRequest req = UnityWebRequest.Post("https://deployment-service-fxy2zritya-uc.a.run.app/bundle-versions/upload", formData);
            req.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
        }

        public void DownloadPackage(string id, string version) {

        }

        public void UpdateToLatest(string packageId) {

        }

        public void UpdateToVersion(string packageId, string version) {

        }
    }
}