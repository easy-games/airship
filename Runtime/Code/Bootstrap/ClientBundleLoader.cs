using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Airship.DevConsole;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
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
        public bool airshipBehaviour;
    }

    public class ClientBundleLoader : NetworkBehaviour {
        [SerializeField]
        public ServerBootstrap serverBootstrap;
        private List<NetworkConnection> connectionsToLoad = new();
        public AirshipEditorConfig editorConfig;

        private bool scriptsReady = false;

        private AirshipScript _airshipScriptTemplate;

        public Stopwatch codeReceiveSt = new Stopwatch();

        private void Awake() {
            DevConsole.ClearConsole();
            if (RunCore.IsClient()) {
                this._airshipScriptTemplate = ScriptableObject.CreateInstance<AirshipScript>();
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

        public override void OnStartClient() {
            base.OnStartClient();
            this.scriptsReady = false;
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
                        airshipBehaviour = filePair.Value.airshipBehaviour
                    };
                    i++;
                }
                dto.files.Add(packagePair.Key, files);
            }

            if (!RunCore.IsEditor()) {
                Debug.Log("Compressed luau files in " + st.ElapsedMilliseconds + " ms.");
            }

            this.LoadGameRpc(connection, startupConfig);

            if (!RunCore.IsEditor()) {
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
        }

        [TargetRpc]
        public void BeforeSendLuauBytes(NetworkConnection conn) {
            this.codeReceiveSt.Restart();
        }

        [TargetRpc]
        public void SendLuaBytes(NetworkConnection conn, LuauScriptsDto scriptsDto) {
            int totalCounter = 0;
            foreach (var files in scriptsDto.files) {
                totalCounter += files.Value.Length;
            }
            Debug.Log($"Received {totalCounter} luau scripts in " + this.codeReceiveSt.ElapsedMilliseconds + " ms.");

            foreach (var packagePair in scriptsDto.files) {
                string packageId = packagePair.Key;
                foreach (var dto in packagePair.Value) {
                    // LuauMetadata metadata = null;
                    // if (!string.IsNullOrEmpty(dto.metadataJson)) {
                    //     var (m, s) = LuauMetadata.FromJson(dto.metadataJson);
                    //     metadata = m;
                    // }

                    var br = Object.Instantiate(this._airshipScriptTemplate);
                    br.m_bytes = dto.bytes;
                    br.m_path = dto.path;
                    br.m_compiled = true;
                    if (dto.airshipBehaviour) {
                        br.airshipBehaviour = true;
                    }
                    
                    var split = dto.path.Split("/");
                    if (split.Length > 0) {
                        br.name = split[split.Length - 1];
                    }

                    var root = SystemRoot.Instance;
                    root.AddLuauFile(packageId, br);
                }
            }

            this.scriptsReady = true;
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
                // UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
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
                BundleDownloader.Instance.downloadAccepted = false;
                yield return BundleDownloader.Instance.DownloadBundles(startupConfig.CdnUrl, packages.ToArray(), null, loadingScreen);
                
                yield return new WaitUntil(() => this.scriptsReady);
            }

            // Debug.Log("Starting to load game: " + startupConfig.GameBundleId);
            if (!RunCore.IsServer()) {
                // This right here. Third parameter, `useUnityAssetBundles`.
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
            if (LuauCore.IsProtectedScene(sceneName)) {
                Debug.LogError("Invalid starting scene name: " + sceneName);
                connection.Kick(KickReason.ExploitAttempt);
                return;
            }

            // todo: New path
            // var scenePath = $"assets/bundles/shared/scenes/{sceneName}.unity";
            var sceneLoadData = new SceneLoadData(new SceneLookupData(sceneName));
            sceneLoadData.PreferredActiveScene = new PreferredScene(new SceneLookupData(sceneName));
            sceneLoadData.Options = new LoadOptions()
            {
                AutomaticallyUnload = false,
            };
            InstanceFinder.SceneManager.LoadConnectionScenes(connection, sceneLoadData);
        }

    }
}