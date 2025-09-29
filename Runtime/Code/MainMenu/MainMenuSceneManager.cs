using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Code;
using Code.Bootstrap;
using Code.Platform.Shared;
using Code.UI;
using Luau;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Newtonsoft.Json;
using Sentry;


[Serializable]
class PlatformVersionsResponse {
    public PlatformVersion platformVersion;
}

[Serializable]
class PlatformVersion {
    public int Core;
    public string Player;
    public int MinPlayerVersion;
}

public class MainMenuSceneManager : MonoBehaviour {
    public MainMenuLoadingScreen loadingScreen;

    private string cachedCoreAssetVersion = "";
    private string cachedCoreCodeVersion = "";

    private bool successfulTSLoad = false;

    private void Start()
    {
        InternalAirshipUtil.HandleWindowSize();

        var savedAccount = AuthManager.GetSavedAccount();
        if (savedAccount == null)
        {
            SceneManager.LoadScene("Login");
            return;
        }

        StartCoroutine(this.StartLoadingCoroutine(0));

        Application.focusChanged += OnApplicationFocus;
        OnApplicationFocus(Application.isFocused);

        SentrySdk.CaptureMessage("Test event1");

        Debug.LogError("This is an error message");
    }

    /**
     * Called by TS to signal that the menu loaded successfully.
     */
    public void CompletedTSLoad() {
        this.successfulTSLoad = true;
    }

    private void OnDestroy() {
        Application.focusChanged -= OnApplicationFocus;
    }

    private void OnApplicationFocus(bool hasFocus) {
        if (hasFocus) {
            Application.targetFrameRate = (int)Math.Ceiling(Screen.currentResolution.refreshRateRatio.value);
        } else {
            Application.targetFrameRate = 10;
        }
    }

    private IEnumerator RetryAfterSeconds(float seconds, int retryCount) {
        yield return new WaitForSecondsRealtime(seconds);
        yield return this.StartLoadingCoroutine(retryCount);
    }

    public void Retry() {
        // Delete existing @Easy folder. This will force redownload of core.
        var easyDir = Path.Combine(Application.persistentDataPath, "Packages", "@Easy");
        if (Directory.Exists(easyDir)) {
            Directory.Delete(easyDir, true);
        }

        StartCoroutine(this.StartLoadingCoroutine(0));
    }

    private IEnumerator StartLoadingCoroutine(int retryCount) {
        var isUsingBundles = false;
        new Promise((resolve, reject) => {
            isUsingBundles = SystemRoot.Instance.IsUsingBundles();
            resolve();
        }).Then(() => {
            Promise<List<string>> promise = new Promise<List<string>>();
            if (isUsingBundles) {
                List<IPromise<PackageVersionResponse>> promises = new();
                promises.Add(GetLatestPackageVersion("@Easy/Core"));
                // promises.Add(GetLatestPackageVersion("@Easy/CoreMaterials"));
                promises[0].Then((results) => {
                    print(JsonConvert.SerializeObject(results.package));
                    print("Asset Version Number: " + results.package.assetVersionNumber);
                    print("Code Version Number: " + results.package.codeVersionNumber);
                    promise.Resolve(new List<string>() {
                        results.package.assetVersionNumber + "",
                        results.package.codeVersionNumber + "",
                        // results.Item2.package.assetVersionNumber + "",
                        // results.Item2.package.codeVersionNumber + ""
                    });
                }).Catch((err) => {
                    promise.Reject(err);
                });
            } else {
                promise.Resolve(new List<string>() {
                    "LocalBuild",
                    "LocalBuild",
                    // "LocalBuild",
                    // "LocalBuild"
                });
            }

            return promise;
        }).Then(async (versions) => {
            var corePackageAssetVersion = versions[0];
            var corePackageCodeVersion = versions[1];
            // var coreMaterialsPackageAssetVersion = versions[2];
            // var coreMaterialsPackageCodeVersion = versions[3];

            // Check if app update is required
            if (isUsingBundles) {
                var versionCheckSw = Stopwatch.StartNew();
                if (
                    this.cachedCoreCodeVersion != corePackageCodeVersion ||
                    this.cachedCoreAssetVersion != corePackageAssetVersion
                ) {
                    var requiresAppUpdate = await this.CheckIfNeedsAppUpdate();
                    if (requiresAppUpdate) {
                        SceneManager.LoadScene("AirshipUpdateApp");
                        return;
                    }
                }
                this.cachedCoreAssetVersion = corePackageAssetVersion;
                this.cachedCoreCodeVersion = corePackageCodeVersion;
                Debug.Log("Checked latest airship version in " + versionCheckSw.ElapsedMilliseconds + " ms.");
            }

            Debug.Log($"@Easy/Core: {versions[0]}");
            List<AirshipPackage> packages = new();
            packages.Add(new AirshipPackage("@Easy/Core", corePackageAssetVersion, corePackageCodeVersion, "", AirshipPackageType.Package));
            // packages.Add(new AirshipPackage("@Easy/CoreMaterials", coreMaterialsPackageAssetVersion, coreMaterialsPackageCodeVersion, AirshipPackageType.Package));
            if (isUsingBundles) {
                await this.StartPackageDownload(packages);
            } else {
                StartCoroutine(this.StartPackageLoad(packages, isUsingBundles));
            }
        }).Catch((err) => {
            Debug.LogError($"Failed to load core packages ({retryCount}): " + err);
            this.loadingScreen.SetProgress("Failed to connect. Retrying in 0.5s..", 0);
            if (retryCount >= 2) {
                this.loadingScreen.SetError("<b>Failed to connect.</b> Are you connected to the internet?");
                return;
            }
            StartCoroutine(this.RetryAfterSeconds(0.5f, retryCount + 1));
        });
        yield break;
    }

    private async Task<bool> CheckIfNeedsAppUpdate() {
        var www = UnityWebRequest.Get(AirshipPlatformUrl.gameCoordinator + "/versions/platform");
        await www.SendWebRequest();
        if (www.result != UnityWebRequest.Result.Success) {
            Debug.LogError("Failed to load platform version. Allowing through...");
            return false;
        }

        print("Platform version:" + www.downloadHandler.text);
        var res = JsonUtility.FromJson<PlatformVersionsResponse>(www.downloadHandler.text);
        print(JsonConvert.SerializeObject(res));

        if (res.platformVersion == null) {
            Debug.LogError("No platform version found. Something went wrong. Allowing through...");
            return false;
        }

        return AirshipConst.playerVersion < res.platformVersion.MinPlayerVersion;
    }

    private async Task StartPackageDownload(List<AirshipPackage> packages) {
        BundleDownloader.Instance.downloadAccepted = false;
        var downloadSuccess = await BundleDownloader.Instance.DownloadBundles(
            AirshipPlatformUrl.gameCdn,
            packages.ToArray(),
            null,
            loadingScreen,
            null,
            true,
            (success) => {
                if (!success) {
                    this.loadingScreen.SetError("Failed to download game content. An error has occurred.");
                    return;
                }
                StartCoroutine(StartPackageLoad(packages, true));
            }
        );
        if (!downloadSuccess) {
            this.loadingScreen.SetError("<b>Failed to download content.</b> Would you like to try again?");
        }
    }

    private IEnumerator StartPackageLoad(List<AirshipPackage> packages, bool usingBundles) {
        var st = Stopwatch.StartNew();
        this.successfulTSLoad = false;
        yield return SystemRoot.Instance.LoadPackages(packages, usingBundles, true, true, (step) => {
            loadingScreen.SetProgress(step, 50);
        });
        Debug.Log($"Finished loading main menu packages in {st.ElapsedMilliseconds} ms.");

        //Setup project configurations from loaded package
        PhysicsSetup.SetupFromGameConfig();
        
        // var mainMenuBindingGO = new GameObject("MainMenuBinding");
        // var mainMenuBinding = mainMenuBindingGO.AddComponent<ScriptBinding>();
        // mainMenuBinding.SetScriptFromPath("@Easy/Core/shared/resources/ts/mainmenu.lua", LuauContext.Protected);
        // mainMenuBinding.Init();
        //
        // var coreLuauBindingGO = new GameObject("CoreLuauBinding");
        // var coreLuauBinding = coreLuauBindingGO.AddComponent<ScriptBinding>();
        // coreLuauBinding.SetScriptFromPath("@Easy/Core/shared/resources/ts/mainmenubootstrap.lua", LuauContext.Game);
        // coreLuauBinding.Init();

        var coreLuauBindingGo = new GameObject("CoreLuauBinding");
        LuauScript.Create(coreLuauBindingGo, "AirshipPackages/@Easy/Core/Shared/MainMenu.ts", LuauContext.Protected, false);
        
        StartCoroutine(CheckForFailedStartup());
    }

    public IEnumerator CheckForFailedStartup() {
        yield return new WaitForSeconds(2);
        if (!this.successfulTSLoad) {
            LuauCore.ResetContext(LuauContext.Protected);

            // Delete core packages
            var path = Path.Combine(Application.persistentDataPath, "Packages", "@Easy");
            if (Directory.Exists(path)) {
                try {
                    Directory.Delete(path, true);
                } catch (Exception e) {
                    Debug.LogError(e);
                }
            }

            // yield return SceneManager.UnloadSceneAsync("MainMenu");
            SceneManager.LoadScene("MainMenu");
        }
    }

    public static IPromise<PackageVersionResponse> GetLatestPackageVersion(string packageId) {
        var url = $"{AirshipPlatformUrl.deploymentService}/package-versions/packageSlug/{packageId}";

        return RestClient.Get<PackageLatestVersionResponse>(new RequestHelper() {
            Uri = url
        }).Then((res) => {
            return res.version;
        });
    }
}