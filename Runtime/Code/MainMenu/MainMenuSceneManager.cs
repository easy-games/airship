using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Code.Bootstrap;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class MainMenuSceneManager : MonoBehaviour {
    public static string cdnUrl = "https://gcdn-staging.easy.gg";
    public static string deploymentUrl = "https://deployment-service-fxy2zritya-uc.a.run.app";
    public AirshipEditorConfig editorConfig;

    private void Start() {
        var savedAccount = AuthManager.GetSavedAccount();
        if (savedAccount == null) {
            SceneManager.LoadScene("Login");
            return;
        }
        StartCoroutine(this.StartLoadingCoroutine());

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

    private IEnumerator RetryAfterSeconds(float seconds) {
        yield return new WaitForSeconds(seconds);
        yield return this.StartLoadingCoroutine();
    }

    private IEnumerator StartLoadingCoroutine() {
        var isUsingBundles = false;
        new Promise((resolve, reject) => {
            isUsingBundles = SystemRoot.Instance.IsUsingBundles(this.editorConfig);
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
        }).Then((versions) => {
            var corePackageAssetVersion = versions[0];
            var corePackageCodeVersion = versions[1];
            var coreMaterialsPackageAssetVersion = versions[2];
            var coreMaterialsPackageCodeVersion = versions[3];
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
            Debug.LogError("Failed to load core packages: " + err);
            Debug.Log("Retrying in 0.5s...");
            StartCoroutine(this.RetryAfterSeconds(0.5f));
        });
        yield break;
    }

    private IEnumerator StartPackageDownload(List<AirshipPackage> packages) {
        var loadingScreen = FindAnyObjectByType<MainMenuLoadingScreen>();
        yield return BundleDownloader.Instance.DownloadBundles(cdnUrl, packages.ToArray(), null, loadingScreen, null, true);
        yield return StartPackageLoad(packages, true);
    }

    private IEnumerator StartPackageLoad(List<AirshipPackage> packages, bool usingBundles) {
        var st = Stopwatch.StartNew();
        yield return SystemRoot.Instance.LoadPackages(packages, usingBundles, true, true);
        Debug.Log($"Finished loading main menu packages in {st.ElapsedMilliseconds} ms.");

        var coreLuauBindingGO = new GameObject("CoreLuauBinding");
        var coreLuauBinding = coreLuauBindingGO.AddComponent<ScriptBinding>();
        coreLuauBinding.SetScriptFromPath("@Easy/Core/shared/resources/ts/mainmenu.lua");
        coreLuauBinding.Init();
    }

    public static IPromise<PackageLatestVersionResponse> GetLatestPackageVersion(string packageId) {
        var url = $"{deploymentUrl}/package-versions/packageSlug/{packageId}";

        return RestClient.Get<PackageLatestVersionResponse>(new RequestHelper() {
            Uri = url
        });
    }
}