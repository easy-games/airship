using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Code;
using Code.Bootstrap;
using Code.Platform.Shared;
using Luau;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

class PlatformVersionsResponse {
    public int Core;
    public string Player;
    public int MinPlayerVersion;
}

public class MainMenuSceneManager : MonoBehaviour {
    public MainMenuLoadingScreen loadingScreen;

    private string cachedCoreAssetVersion = "";
    private string cachedCoreCodeVersion = "";

    private void Start() {
        var savedAccount = AuthManager.GetSavedAccount();
        if (savedAccount == null) {
            SceneManager.LoadScene("Login");
            return;
        }

        StartCoroutine(this.StartLoadingCoroutine(0));

        Application.focusChanged += OnApplicationFocus;
        OnApplicationFocus(Application.isFocused);
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
                List<IPromise<PackageLatestVersionResponse>> promises = new();
                promises.Add(GetLatestPackageVersion("@Easy/Core"));
                promises.Add(GetLatestPackageVersion("@Easy/CoreMaterials"));
                PromiseHelpers.All(promises[0], promises[1]).Then((results) => {
                    print(results.Item1.package);
                    promise.Resolve(new List<string>() {
                        results.Item1.package.assetVersionNumber + "",
                        results.Item1.package.codeVersionNumber + "",
                        results.Item2.package.assetVersionNumber + "",
                        results.Item2.package.codeVersionNumber + ""
                    });
                }).Catch((err) => {
                    promise.Reject(err);
                });
            } else {
                promise.Resolve(new List<string>() {
                    "LocalBuild",
                    "LocalBuild",
                    "LocalBuild",
                    "LocalBuild"
                });
            }

            return promise;
        }).Then(async (versions) => {
            var corePackageAssetVersion = versions[0];
            var corePackageCodeVersion = versions[1];
            var coreMaterialsPackageAssetVersion = versions[2];
            var coreMaterialsPackageCodeVersion = versions[3];

            // Check if app update is required
            if (isUsingBundles) {
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
            }

            Debug.Log($"@Easy/Core: {versions[0]}, @Easy/CoreMaterials: {versions[1]}");
            List<AirshipPackage> packages = new();
            packages.Add(new AirshipPackage("@Easy/Core", corePackageAssetVersion, corePackageCodeVersion, AirshipPackageType.Package));
            packages.Add(new AirshipPackage("@Easy/CoreMaterials", coreMaterialsPackageAssetVersion, coreMaterialsPackageCodeVersion, AirshipPackageType.Package));
            if (isUsingBundles) {
                StartCoroutine(this.StartPackageDownload(packages));
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

        var res = JsonUtility.FromJson<PlatformVersionsResponse>(www.downloadHandler.text);
        return AirshipConst.playerVersion < res.MinPlayerVersion;
    }

    private IEnumerator StartPackageDownload(List<AirshipPackage> packages) {
        BundleDownloader.Instance.downloadAccepted = false;
        var loadingScreen = FindAnyObjectByType<MainMenuLoadingScreen>();
        yield return BundleDownloader.Instance.DownloadBundles(AirshipPlatformUrl.gameCdn, packages.ToArray(), null, loadingScreen, null, true);
        yield return StartPackageLoad(packages, true);
    }

    private IEnumerator StartPackageLoad(List<AirshipPackage> packages, bool usingBundles) {
        var st = Stopwatch.StartNew();
        yield return SystemRoot.Instance.LoadPackages(packages, usingBundles, true, true);
        Debug.Log($"Finished loading main menu packages in {st.ElapsedMilliseconds} ms.");

        // var mainMenuBindingGO = new GameObject("MainMenuBinding");
        // var mainMenuBinding = mainMenuBindingGO.AddComponent<ScriptBinding>();
        // mainMenuBinding.SetScriptFromPath("@Easy/Core/shared/resources/ts/mainmenu.lua", LuauContext.Protected);
        // mainMenuBinding.Init();
        //
        // var coreLuauBindingGO = new GameObject("CoreLuauBinding");
        // var coreLuauBinding = coreLuauBindingGO.AddComponent<ScriptBinding>();
        // coreLuauBinding.SetScriptFromPath("@Easy/Core/shared/resources/ts/mainmenubootstrap.lua", LuauContext.Game);
        // coreLuauBinding.Init();

        var coreLuauBindingGO = new GameObject("CoreLuauBinding");
        var coreLuauBinding = coreLuauBindingGO.AddComponent<AirshipComponent>();
        coreLuauBinding.SetScriptFromPath("AirshipPackages/@Easy/Core/Shared/MainMenu.ts", LuauContext.Protected);
        coreLuauBinding.Init();
    }

    public static IPromise<PackageLatestVersionResponse> GetLatestPackageVersion(string packageId) {
        var url = $"{AirshipPlatformUrl.deploymentService}/package-versions/packageSlug/{packageId}";

        return RestClient.Get<PackageLatestVersionResponse>(new RequestHelper() {
            Uri = url
        });
    }
}