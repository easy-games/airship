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

    struct GreetingMessage : NetworkMessage {}

    struct InitializeGameMessage : NetworkMessage {
        public StartupConfig startupConfig;
        public string scriptsHash;
    }

    struct RequestScriptsMessage : NetworkMessage {}

    struct ClientFinishedPreparingMessage : NetworkMessage {}

    struct LuauBytesMessage : NetworkMessage {
        public string hash;
        public LuauScriptsDto scriptsDto;
        public bool first;
        public bool final;
    }

    public class ClientBundleLoader : MonoBehaviour {
        [SerializeField]
        public ServerBootstrap serverBootstrap;
        private List<NetworkConnectionToClient> connectionsReadyToLoadGameScene = new();

        private bool scriptsReady = false;
        private bool packagesReady = false;

        public bool isFinishedPreparing = false;

        private AirshipScript _airshipScriptTemplate;

        public Stopwatch codeReceiveSt = new Stopwatch();
        private List<LuauScriptsDto> scriptsDtos = new();
        private string scriptsHash;

        private void Awake() {
            DevConsole.ClearConsole();
            if (RunCore.IsClient()) {
                this._airshipScriptTemplate = ScriptableObject.CreateInstance<AirshipScript>();
            }
        }

        public void SetupServer() {
            NetworkServer.RegisterHandler<RequestScriptsMessage>((conn, data) => {
                Debug.Log($"Sending {this.scriptsDtos.Count} script dtos to " + conn.identity + " with hash " + this.scriptsHash);
                int i = 0;
                foreach (var dto in this.scriptsDtos) {
                    conn.Send(new LuauBytesMessage() {
                        scriptsDto = dto,
                        hash = this.scriptsHash,
                        first = i == 0,
                        final = i == this.scriptsDtos.Count - 1,
                    });
                    i++;
                }
            }, false);
            NetworkServer.RegisterHandler<ClientFinishedPreparingMessage>((conn, data) => {
                var sceneName = this.serverBootstrap.startupConfig.StartingSceneName.ToLower();
                if (LuauCore.IsProtectedScene(sceneName)) {
                    Debug.LogError("Invalid starting scene name: " + sceneName);
                    conn.Disconnect();
                    return;
                }

                Debug.Log("Sending scene load message to " + conn + ". scene: " + sceneName);
                SceneMessage message = new SceneMessage { sceneName = sceneName, sceneOperation = SceneOperation.LoadAdditive, customHandling = true };
                conn.Send(message);
            }, false);

            NetworkServer.RegisterHandler<GreetingMessage>((conn, data) => {
                if (this.serverBootstrap.isStartupConfigReady) {
                    this.InitConnection(conn, this.serverBootstrap.startupConfig);
                    return;
                }
                StartCoroutine(InitConnectionWhenServerIsReady(conn));
            }, false);
        }

        public void CleanupServer() {
            NetworkServer.UnregisterHandler<RequestScriptsMessage>();
            NetworkServer.UnregisterHandler<ClientFinishedPreparingMessage>();
            NetworkServer.UnregisterHandler<GreetingMessage>();
        }

        public async void SetupClient() {
            this.scriptsReady = false;
            this.packagesReady = false;
            this.isFinishedPreparing = false;

            NetworkClient.RegisterHandler<InitializeGameMessage>(async data => {
                NetworkManager.networkSceneName = data.startupConfig.StartingSceneName;

                StartCoroutine(this.LoadPackages(data.startupConfig));

                if (SystemRoot.Instance.IsUsingBundles()) {
                    try {
                        var st = Stopwatch.StartNew();
                        var path = Path.Join(Application.persistentDataPath, "Scripts", data.scriptsHash + ".dto");
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

                    NetworkClient.Send(new RequestScriptsMessage());
                } else {
                    this.scriptsReady = true;
                }

                while (!this.scriptsReady || !this.packagesReady) {
                    await Awaitable.NextFrameAsync();
                }

                this.isFinishedPreparing = true;
                NetworkClient.Send(new ClientFinishedPreparingMessage());
            }, false);

            NetworkClient.RegisterHandler<LuauBytesMessage>(async data => {
                int totalCounter = 0;
                foreach (var files in data.scriptsDto.files) {
                    totalCounter += files.Value.Count;
                }
                Debug.Log($"Received {totalCounter} luau scripts in " + this.codeReceiveSt.ElapsedMilliseconds + " ms.");

                this.ClientUnpackScriptsDto(data.scriptsDto);

                try {
                    var writer = new NetworkWriter();
                    writer.WriteLuauScriptsDto(data.scriptsDto);
                    if (!Directory.Exists(Path.Join(Application.persistentDataPath, "Scripts"))) {
                        Directory.CreateDirectory(Path.Join(Application.persistentDataPath, "Scripts"));
                    }

                    var path = Path.Join(Application.persistentDataPath, "Scripts", data.hash + ".dto");
                    var bytes = writer.ToArray();
                    if (data.first) {
                        File.WriteAllText(path, "");
                    }
                    using (var stream = new FileStream(path, FileMode.Append)) {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                } catch (Exception e) {
                    Debug.LogException(e);
                }

                if (data.final) {
                    print("scripts hash: " + data.hash);
                    this.scriptsReady = true;
                }
            }, false);

            while (!NetworkClient.isConnected) {
                await Awaitable.NextFrameAsync();
            }
            NetworkClient.Send(new GreetingMessage());
        }

        public void CleanupClient() {
            NetworkClient.UnregisterHandler<LuauBytesMessage>();
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

            int maxBytesPerDto = 50_000;
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

        private IEnumerator InitConnectionWhenServerIsReady(NetworkConnectionToClient conn) {
            while (!this.serverBootstrap.isStartupConfigReady) {
                yield return null;
            }
            this.InitConnection(conn, this.serverBootstrap.startupConfig);
        }

        private void InitConnection(NetworkConnectionToClient connection, StartupConfig startupConfig) {
            // print("Initializing " + connection);
            connection.Send(new InitializeGameMessage() {
                startupConfig = startupConfig,
                scriptsHash = this.scriptsHash,
            });
        }

        private void LoadGameSceneForConnection(NetworkConnectionToClient connection) {
            // Debug.Log("Sending load game scene message to " + connection);
            var sceneName = this.serverBootstrap.startupConfig.StartingSceneName.ToLower();
            if (LuauCore.IsProtectedScene(sceneName)) {
                Debug.LogError("Invalid starting scene name: " + sceneName);
                connection.Disconnect();
                return;
            }

            SceneMessage message = new SceneMessage { sceneName = sceneName, sceneOperation = SceneOperation.LoadAdditive, customHandling = true };
            connection.Send(message);
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

        void Start() {
            if (RunCore.IsServer()) {
                this.serverBootstrap.OnServerReady += ServerBootstrap_OnServerReady;
            }
        }

        private void OnDestroy() {
            if (RunCore.IsServer() && this.serverBootstrap) {
                this.serverBootstrap.OnServerReady -= ServerBootstrap_OnServerReady;
            }
        }

        private void ServerBootstrap_OnServerReady() {
            foreach (var conn in connectionsReadyToLoadGameScene) {
                LoadGameSceneForConnection(conn);
            }
        }

        private IEnumerator LoadPackages(StartupConfig startupConfig) {
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

            this.packagesReady = true;
        }

        // public void LoadAllClients(StartupConfig config) {
        //     foreach (var conn in NetworkServer.connections.Values) {
        //         if (conn.isAuthenticated) {
        //             this.SetupConnection(conn, config);
        //         }
        //     }
        // }

    }
}