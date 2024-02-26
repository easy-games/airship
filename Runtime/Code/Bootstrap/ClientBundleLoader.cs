using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using Luau;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Code.Bootstrap {
    [Serializable]
    public class LuauFileDto {
        /// <summary>
        /// file path
        /// </summary>
        public string path;

        /// <summary>
        /// luau bytes
        /// </summary>
        public byte[] bytes;

        /// <summary>
        /// Metadata json
        /// </summary>
        public string metadataJson;
    }

    public class ClientBundleLoader : NetworkBehaviour {
        [SerializeField]
        public ServerBootstrap serverBootstrap;
        private List<NetworkConnection> connectionsToLoad = new();
        public AirshipEditorConfig editorConfig;

        private BinaryFile binaryFileTemplate;

        public Stopwatch codeReceiveSt = new Stopwatch();

        private void Awake() {
            if (RunCore.IsClient()) {
                this.binaryFileTemplate = ScriptableObject.CreateInstance<BinaryFile>();
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneManager_OnSceneLoaded;
            }
        }

        private void OnDestroy() {
            if (RunCore.IsClient()) {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneManager_OnSceneLoaded;
            }
        }

        public override void OnSpawnServer(NetworkConnection connection) {
            base.OnSpawnServer(connection);
            if (this.serverBootstrap.isStartupConfigReady) {
                this.SetupConnection(connection, this.serverBootstrap.startupConfig);
            }
        }

        [Server]
        private void SetupConnection(NetworkConnection connection, StartupConfig startupConfig) {
            // print("Setting up connection " + connection.ClientId);

            var root = SystemRoot.Instance;

            var st = Stopwatch.StartNew();
            var dto = new LuauScriptsDto();
            foreach (var packagePair in root.luauFiles) {
                LuauFileDto[] files = new LuauFileDto[packagePair.Value.Count];
                int i = 0;
                foreach (var filePair in packagePair.Value) {
                    files[i] = new LuauFileDto() {
                        path = filePair.Value.m_path,
                        bytes = filePair.Value.m_bytes,
                        metadataJson = string.Empty
                    };
                    if (filePair.Value.m_metadata != null) {
                        files[i].metadataJson = JsonUtility.ToJson(filePair.Value.m_metadata);
                    }
                    i++;
                }
                dto.files.Add(packagePair.Key, files);
            }

            if (!RunCore.IsEditor()) {
                Debug.Log("Packed luauScripts dto in " + st.ElapsedMilliseconds + " ms.");
                this.BeforeSendLuauBytes(connection);
                this.SendLuaBytes(connection, dto);
            }


            // int pkgI = 0;
            // foreach (var pair1 in root.luauFiles) {
            //     int fileI = 0;
            //     foreach (var filePair in pair1.Value) {
            //         var bf = filePair.Value;
            //         var metadataJson = string.Empty;
            //         if (bf.m_metadata != null) {
            //             metadataJson = JsonUtility.ToJson(bf.m_metadata);
            //         }
            //         this.SendLuaBytes(connection, pair1.Key, filePair.Key, bf.m_bytes, metadataJson,  pkgI == 0 && fileI == 0,  pkgI == root.luauFiles.Count - 1 && fileI == pair1.Value.Count - 1);
            //         fileI++;
            //     }
            //
            //     pkgI++;
            // }

            this.LoadGameRpc(connection, startupConfig);
        }

        [TargetRpc]
        public void BeforeSendLuauBytes(NetworkConnection conn) {
            this.codeReceiveSt.Restart();
        }

        [TargetRpc]
        public void SendLuaBytes(NetworkConnection conn, LuauScriptsDto scriptsDto) {
            foreach (var packagePair in scriptsDto.files) {
                string packageId = packagePair.Key;
                foreach (var dto in packagePair.Value) {
                    LuauMetadata metadata = null;
                    if (!string.IsNullOrEmpty(dto.metadataJson)) {
                        var (m, s) = LuauMetadata.FromJson(dto.metadataJson);
                        metadata = m;
                    }

                    var br = Object.Instantiate(this.binaryFileTemplate);
                    br.m_bytes = dto.bytes;
                    br.m_path = dto.path;
                    br.m_metadata = metadata;

                    var split = dto.path.Split("/");
                    if (split.Length > 0) {
                        br.name = split[split.Length - 1];
                    }

                    var root = SystemRoot.Instance;
                    root.AddLuauFile(packageId, br);
                }
            }

            Debug.Log("Received luau scripts in " + this.codeReceiveSt.ElapsedMilliseconds + " ms.");
        }

        [TargetRpc][ObserversRpc]
        public void LoadGameRpc(NetworkConnection conn, StartupConfig startupConfig) {
            StartCoroutine(this.ClientSetup(startupConfig));
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            this.serverBootstrap.OnServerReady += ServerBootstrap_OnServerReady;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (this.serverBootstrap) {
                this.serverBootstrap.OnServerReady -= ServerBootstrap_OnServerReady;
            }
        }

        private void ServerBootstrap_OnServerReady() {
            foreach (var conn in connectionsToLoad) {
                LoadConnection(conn);
            }
        }

        private void SceneManager_OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (scene.IsValid()) {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            }
        }

        private IEnumerator ClientSetup(StartupConfig startupConfig) {
            if (!RunCore.IsClient()) {
                yield break;
            }

            List<AirshipPackage> packages = new();
            foreach (var packageDoc in startupConfig.packages) {
                packages.Add(new AirshipPackage(packageDoc.id, packageDoc.assetVersion, packageDoc.codeVersion, packageDoc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
            }

            if (CrossSceneState.IsLocalServer() || CrossSceneState.UseLocalBundles)
            {
                // Debug.Log("Skipping bundle download.");
            } else {
                var loadingScreen = FindAnyObjectByType<CoreLoadingScreen>();
                var bundleDownloader = FindAnyObjectByType<BundleDownloader>();
                yield return bundleDownloader.DownloadBundles(startupConfig.CdnUrl, packages.ToArray(), null, loadingScreen);
            }

            // Debug.Log("Starting to load game: " + startupConfig.GameBundleId);
            if (!RunCore.IsServer()) {
                yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles(editorConfig), false);
            }

            EasyFileService.ClearCache();

            // Debug.Log("Finished loading bundles. Requesting scene load...");
            LoadGameSceneServerRpc();
        }

        [Server]
        public void LoadAllClients(StartupConfig config) {
            foreach (var conn in InstanceFinder.ServerManager.Clients.Values) {
                this.SetupConnection(conn, config);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void LoadGameSceneServerRpc(NetworkConnection conn = null)
        {
            if (!this.serverBootstrap.serverReady) {
                connectionsToLoad.Add(conn);
                return;
            }

            LoadConnection(conn);
        }

        [Server]
        private void LoadConnection(NetworkConnection connection)
        {
            var sceneName = this.serverBootstrap.startupConfig.StartingSceneName.ToLower();
            var scenePath = $"assets/bundles/shared/scenes/{sceneName}.unity";
            var sceneLoadData = new SceneLoadData(scenePath);
            sceneLoadData.ReplaceScenes = ReplaceOption.None;
            sceneLoadData.Options = new LoadOptions()
            {
                AutomaticallyUnload = false,
            };
            InstanceFinder.SceneManager.LoadConnectionScenes(connection, sceneLoadData);
        }

    }
}