using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Airship.DevConsole;
using Luau;
using Mirror;
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
        private List<NetworkConnectionToClient> connectionsToLoad = new();

        private bool scriptsReady = false;

        private AirshipScript _airshipScriptTemplate;

        public Stopwatch codeReceiveSt = new Stopwatch();
        private List<LuauScriptsDto> scriptsDtos = new();
        private string scriptsHash;

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

        public void GenerateScriptsDto() {
            // We don't need to generate script dto in editor.
            if (RunCore.IsEditor()) {
                return;
            }
            var root = SystemRoot.Instance;

            var st = Stopwatch.StartNew();
            this.scriptsDtos.Clear();
            List<byte> totalBytes = new();

            int maxBytesPerDto = 4_000;
            int writtenBytes = 0;
            LuauScriptsDto currentDto = null;
            foreach (var packagePair in root.luauFiles) {
                int i = 0;
                foreach (var filePair in packagePair.Value) {
                    if (currentDto == null) {
                        currentDto = new LuauScriptsDto();
                        this.scriptsDtos.Add(currentDto);
                    }

                    var file = new LuauFileDto() {
                        path = filePair.Value.m_path,
                        bytes = filePair.Value.m_bytes,
                        airshipBehaviour = filePair.Value.airshipBehaviour
                    };
                    if (!currentDto.files.ContainsKey(packagePair.Key)) {
                        currentDto.files.Add(packagePair.Key, new List<LuauFileDto>());
                    }
                    currentDto.files[packagePair.Key].Add(file);

                    // totalBytes is only used for calculating hash
                    totalBytes.AddRange(filePair.Value.m_bytes);
                    writtenBytes += filePair.Value.m_bytes.Length;
                    i++;

                    if (writtenBytes >= maxBytesPerDto) {
                        currentDto = null;
                        writtenBytes = 0;
                    }
                }
            }
            var sha = new System.Security.Cryptography.SHA1CryptoServiceProvider();
            this.scriptsHash = System.BitConverter.ToString(sha.ComputeHash(totalBytes.ToArray()));

            if (!RunCore.IsEditor()) {
                Debug.Log("Generated scripts dto in " + st.ElapsedMilliseconds + " ms. Hash: " + this.scriptsHash);
            }
        }

        [Command(requiresAuthority = false)]
        void ClientReadyCommand(NetworkConnectionToClient connection = null) {
            if (this.serverBootstrap.isStartupConfigReady) {
                this.SetupConnection(connection, this.serverBootstrap.startupConfig);
                return;
            }

            StartCoroutine(SetupConnectionWhenServerIsReady(connection));
        }

        private IEnumerator SetupConnectionWhenServerIsReady(NetworkConnectionToClient conn) {
            while (!this.serverBootstrap.isStartupConfigReady) {
                yield return null;
            }
            this.SetupConnection(conn, this.serverBootstrap.startupConfig);
        }

        public override void OnStartClient() {
            this.scriptsReady = false;
            this.ClientReadyCommand();
        }
        
        private void SetupConnection(NetworkConnectionToClient connection, StartupConfig startupConfig) {
            // print("Setting up connection " + connection.ClientId);

            this.LoadGameRpc(connection, startupConfig);

            if (!RunCore.IsEditor()) {
                this.BeforeSendLuauBytes(connection, scriptsHash);
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

        [Command(requiresAuthority = false)]
        public void RequestScriptsDto(NetworkConnectionToClient conn = null) {
            Debug.Log($"Sending {this.scriptsDtos.Count} script dtos to " + conn.identity + " with hash " + this.scriptsHash);
            int i = 0;
            foreach (var dto in this.scriptsDtos) {
                this.SendLuaBytes(conn, this.scriptsHash, dto, i == 0, i == this.scriptsDtos.Count - 1);
                i++;
            }
        }

        [TargetRpc]
        public void BeforeSendLuauBytes(NetworkConnectionToClient conn, string scriptsHash) {
            if (!SystemRoot.Instance.IsUsingBundles()) {
                this.scriptsReady = true;
                return;
            }

            // todo: check for local scripts cache that matches hash and return early
            try {
                var st = Stopwatch.StartNew();
                var path = Path.Join(Application.persistentDataPath, "Scripts", scriptsHash + ".dto");
                if (File.Exists(path)) {
                    var bytes = File.ReadAllBytes(path);
                    NetworkReader reader = new NetworkReader(bytes);
                    var scriptsDto = reader.ReadLuauScriptsDto();
                    this.ClientUnpackScriptsDto(scriptsDto);
                    this.scriptsReady = true;
                    Debug.Log("Unpacked code.zip cache in " + st.ElapsedMilliseconds + " ms.");
                    return;
                }
            } catch (Exception e) {
                Debug.LogException(e);
            }

            Debug.Log("Missing code.zip cache. Requesting scripts from server...");
            this.codeReceiveSt.Restart();
            this.RequestScriptsDto();
        }

        private void ClientUnpackScriptsDto(LuauScriptsDto scriptsDto) {
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
        }

        [TargetRpc]
        public void SendLuaBytes(NetworkConnection conn, string hash, LuauScriptsDto scriptsDto, bool first, bool final) {
            int totalCounter = 0;
            foreach (var files in scriptsDto.files) {
                totalCounter += files.Value.Count;
            }
            Debug.Log($"Received {totalCounter} luau scripts in " + this.codeReceiveSt.ElapsedMilliseconds + " ms.");

            this.ClientUnpackScriptsDto(scriptsDto);

            try {
                var writer = new NetworkWriter();
                writer.WriteLuauScriptsDto(scriptsDto);
                if (!Directory.Exists(Path.Join(Application.persistentDataPath, "Scripts"))) {
                    Directory.CreateDirectory(Path.Join(Application.persistentDataPath, "Scripts"));
                }

                var path = Path.Join(Application.persistentDataPath, "Scripts", hash + ".dto");
                var bytes = writer.ToArray();
                if (first) {
                    File.WriteAllText(path, "");
                }
                using (var stream = new FileStream(path, FileMode.Append)) {
                    stream.Write(bytes, 0, bytes.Length);
                }
            } catch (Exception e) {
                Debug.LogException(e);
            }

            if (final) {
                print("scripts hash: " + hash);
                this.scriptsReady = true;
            }
        }

        [TargetRpc]
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
                yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles(), false);
            }

            EasyFileService.ClearCache();

            // Debug.Log("Finished loading bundles. Requesting scene load...");
            LoadGameSceneServerRpc();
        }

        // public void LoadAllClients(StartupConfig config) {
        //     foreach (var conn in NetworkServer.connections.Values) {
        //         if (conn.isAuthenticated) {
        //             this.SetupConnection(conn, config);
        //         }
        //     }
        // }

        [Command(requiresAuthority = false)]
        private void LoadGameSceneServerRpc(NetworkConnectionToClient conn = null) {
            if (!this.serverBootstrap.serverReady) {
                connectionsToLoad.Add(conn);
                return;
            }

            LoadConnection(conn);
        }

        private void LoadConnection(NetworkConnectionToClient connection) {
            Debug.Log("Loading connection " + connection.connectionId);
            var sceneName = this.serverBootstrap.startupConfig.StartingSceneName.ToLower();
            if (LuauCore.IsProtectedScene(sceneName)) {
                Debug.LogError("Invalid starting scene name: " + sceneName);
                connection.Disconnect();
                return;
            }

            SceneMessage message = new SceneMessage { sceneName = sceneName, sceneOperation = SceneOperation.LoadAdditive, customHandling = true };
            connection.Send(message);
        }

    }
}