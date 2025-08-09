using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Airship.DevConsole;
using Code.Authentication;
using Code.Analytics;
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
        private InitializeGameMessage initMessage;

        public bool scriptsReady = false;
        public bool packagesReady = false;

        public bool isFinishedPreparing = false;

        private AirshipScript _airshipScriptTemplate;

        public Stopwatch codeReceiveSt = new Stopwatch();
        private List<LuauScriptsDto> scriptsDtos = new();
        private string scriptsHash;

        private int setupClientSessionCounter = 0;

        /// <summary>
        /// Client downloads and combines all LuauScriptDto objects into this single dto.
        /// This is then serialized to disk as a cache.
        /// </summary>
        private LuauScriptsDto clientLuauScriptsDto;

        private void Awake() {
            if (DevConsole.clearConsoleOnServerConnect) {
                DevConsole.ClearConsole();
            }
            if (RunCore.IsClient()) {
                this._airshipScriptTemplate = ScriptableObject.CreateInstance<AirshipScript>();
            }
        }

        public void SetupServer() {
            NetworkServer.RegisterHandler<RequestScriptsMessage>((conn, data) => {
                Debug.Log($"Sending {this.scriptsDtos.Count} script dtos to " + conn + " with hash " + this.scriptsHash);
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
                var sceneName = serverBootstrap.startupConfig.StartingSceneName;
                if (LuauCore.IsProtectedSceneName(sceneName)) {
                    Debug.LogError("Invalid starting scene name: " + sceneName);
                    conn.Disconnect();
                    return;
                }

                // Debug.Log("Sending scene load message to " + conn + ". scene: " + sceneName);
                conn.Send(new SceneMessage { sceneName = sceneName, sceneOperation = SceneOperation.LoadAdditive, customHandling = true });
            }, false);

            NetworkServer.RegisterHandler<GreetingMessage>(async (conn, data) => {
                while (!this.serverBootstrap.isServerReady) {
                    await Awaitable.NextFrameAsync();
                }

                // Validate scene name
                var sceneName = this.serverBootstrap.startupConfig.StartingSceneName;
                if (LuauCore.IsProtectedSceneName(sceneName)) {
                    Debug.LogError("Invalid starting scene name: " + sceneName + ". The name of this scene is not allowed.");
                    conn.Send(new KickMessage() {
                        reason = "Invalid starting scene name: " + sceneName + ". The name of this scene is not allowed. Report this to the game developer.",
                    });
                    await Awaitable.WaitForSecondsAsync(1);
                    conn.Disconnect();
                    return;
                }

                conn.Send(new InitializeGameMessage() {
                    startupConfig = this.serverBootstrap.startupConfig,
                    scriptsHash = this.scriptsHash,
                });
            }, false);
        }

        public void CleanupServer() {
            try {
                NetworkServer.UnregisterHandler<RequestScriptsMessage>();
                NetworkServer.UnregisterHandler<ClientFinishedPreparingMessage>();
                NetworkServer.UnregisterHandler<GreetingMessage>();
            } catch (Exception e) {
                Debug.LogException(e);
            }
        }

        public void RetryDownload() {
            StartCoroutine(this.LoadPackages(this.initMessage.startupConfig));
        }

        /// <summary>
        /// Called when client starts but before it's ready.
        /// </summary>
        /// Client timeline:
        /// 1. Send GreetingMessage
        /// 2. Receive InitializeGameMessage
        /// 3. (Optional) Send RequestScriptsMessage
        ///     a. Receives LuauBytesMessage
        /// 4. Send ClientFinishedPreparingMessage
        /// 5. Receive SceneMessage
        /// 6. NetworkClient.Ready()
        public async void SetupClient() {
            this.setupClientSessionCounter++;
            var session = this.setupClientSessionCounter;
            this.scriptsReady = false;
            this.packagesReady = false;
            this.isFinishedPreparing = false;

            NetworkClient.RegisterHandler<InitializeGameMessage>(async data => {
                this.initMessage = data;
                NetworkManager.networkSceneName = data.startupConfig.StartingSceneName;

                StartCoroutine(this.LoadPackages(data.startupConfig));
                LoadLuau(data);

                while (!this.scriptsReady || !this.packagesReady) {
                    await Awaitable.NextFrameAsync();
                }

                this.isFinishedPreparing = true;
                AnalyticsRecorder.InitGame(data.startupConfig);
                NetworkClient.Send(new ClientFinishedPreparingMessage());
            }, false);

            NetworkClient.RegisterHandler<LuauBytesMessage>(data => {
                int totalCounter = 0;
                foreach (var files in data.scriptsDto.files) {
                    totalCounter += files.Value.Count;
                }
                // Debug.Log($"Received {totalCounter} luau scripts in " + this.codeReceiveSt.ElapsedMilliseconds + " ms.");

                this.ClientUnpackScriptsDto(data.scriptsDto);

                if (data.first) {
                    this.clientLuauScriptsDto = data.scriptsDto;
                } else {
                    foreach (var pair in data.scriptsDto.files) {
                        if (!this.clientLuauScriptsDto.files.ContainsKey(pair.Key)) {
                            this.clientLuauScriptsDto.files.Add(pair.Key, pair.Value);
                            continue;
                        }
                        List<LuauFileDto> cachedFiles = this.clientLuauScriptsDto.files[pair.Key];
                        cachedFiles.AddRange(pair.Value);
                    }
                }

                if (data.final) {
                    // print("scripts hash: " + data.hash);
                    try {
                        var writer = new NetworkWriter();
                        writer.WriteLuauScriptsDto(data.scriptsDto);
                        if (!Directory.Exists(Path.Join(Application.persistentDataPath, "Scripts"))) {
                            Directory.CreateDirectory(Path.Join(Application.persistentDataPath, "Scripts"));
                        }

                        var path = Path.Join(Application.persistentDataPath, "Scripts", data.hash + ".bytes");
                        var bytes = writer.ToArray();
                        File.WriteAllBytes(path, bytes);
                        File.WriteAllText(path + ".success-2", "");
                    } catch (Exception e) {
                        Debug.LogException(e);
                    }

                    using var md5 = System.Security.Cryptography.MD5.Create();
                    var filenames = new List<string>();
                    var bytecodeHashes = new List<byte[]>();
                    foreach (var (hash, files) in clientLuauScriptsDto.files) {
                        foreach (var file in files) {
                            var path = file.path;
                            if (path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) {
                                path = path.Substring(7);
                            }
                            filenames.Add(path);
                            bytecodeHashes.Add(md5.ComputeHash(file.bytes));
                        }
                    }

                    if (LuauPlugin.LuauPushScripts(filenames, bytecodeHashes)) {
                        Debug.Log("scripts pushed");
                    } else {
                        Debug.LogWarning("scripts not pushed");
                    }
                    
                    this.scriptsReady = true;
                }
            }, false);

            while (!NetworkClient.isConnected) {
                await Awaitable.NextFrameAsync();
            }

            if (this.setupClientSessionCounter != session) return;

            NetworkClient.Send(new GreetingMessage());
        }

        private async void LoadLuau(InitializeGameMessage data) {
            if (!SystemRoot.Instance.IsUsingBundles()) {
                this.scriptsReady = true;
                return;
            }
            try {
                var st = Stopwatch.StartNew();
                var path = Path.Join(Application.persistentDataPath, "Scripts", data.scriptsHash + ".bytes");
                if (SystemRoot.Instance.codeZipCacheEnabled && File.Exists(path) && File.Exists(path + ".success-2")) {
                    Debug.Log("Found code.zip cache! Unpacking..");
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

            this.codeReceiveSt.Restart();

            print("[Airship] Requesting scripts from server...");
            NetworkClient.Send(new RequestScriptsMessage());
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
                    // Debug.Log("Add luau file: " + dto.path);
                }
            }
        }

        private IEnumerator LoadPackages(StartupConfig startupConfig) {
            if (!RunCore.IsClient()) {
                yield break;
            }

            List<AirshipPackage> packages = new();
            foreach (var packageDoc in startupConfig.packages) {
                if (packageDoc.id.ToLower() == "@easy/corematerials") {
                    continue;
                }

                packages.Add(new AirshipPackage(packageDoc.id, packageDoc.assetVersion, packageDoc.codeVersion, packageDoc.publishVersionNumber, packageDoc.game ? AirshipPackageType.Game : AirshipPackageType.Package));
            }

            var loadingScreen = FindAnyObjectByType<CoreLoadingScreen>();

            if (CrossSceneState.IsLocalServer() || CrossSceneState.UseLocalBundles)
            {
                // Debug.Log("Skipping bundle download.");
            } else {
                BundleDownloader.Instance.downloadAccepted = false;
                bool finishedDownload = false;
                var downloadResult = BundleDownloader.Instance.DownloadBundles(
                    startupConfig.CdnUrl,
                    packages.ToArray(),
                    null,
                    loadingScreen,
                    null,
                    false,
                    (success) => {
                        finishedDownload = true;
                        if (!success) {
                            loadingScreen.SetError("Failed to download game content. An error has occurred.");
                        }
                    }
                );

                while (!finishedDownload) {
                    yield return null;
                }

                // Something failed in the bundle downloader.
                // So we stop and wait for player to press retry (which calls this function again)
                if (!downloadResult.Result) {
                    yield break;
                }
                
                yield return new WaitUntil(() => this.scriptsReady);
            }

            // Debug.Log("Starting to load game: " + startupConfig.GameBundleId);
            if (!RunCore.IsServer()) {
                // This right here. Third parameter, `useUnityAssetBundles`.
                yield return SystemRoot.Instance.LoadPackages(packages, SystemRoot.Instance.IsUsingBundles(), false, false,
                    step => {
                        loadingScreen.SetProgress(step, 40);
                    });
            }

            EasyFileService.ClearCache();

            //Setup project configurations from loaded package
            PhysicsSetup.SetupFromGameConfig();

            this.packagesReady = true;
        }
    }
}