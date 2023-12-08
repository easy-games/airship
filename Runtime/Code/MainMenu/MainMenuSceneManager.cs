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
        }).Then<string>(() => {
            var promise = new Promise<string>();
            if (isUsingBundles) {
                GetLatestPackageVersion("@Easy/Core").Then((res) => {
                    promise.Resolve(res.package.assetVersionNumber + "");
                }).Catch((err) => {
                    promise.Reject(err);
                });
            } else {
                promise.Resolve("LocalBuild");
            }

            return promise;
        }).Then((corePackageVersion) => {
            Debug.Log("Using core version: v" + corePackageVersion);
            List<AirshipPackage> packages = new();
            packages.Add(new AirshipPackage("@Easy/Core", corePackageVersion, AirshipPackageType.Package));
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
        var loadingScreen = FindObjectOfType<MainMenuLoadingScreen>();
        yield return BundleDownloader.Instance.DownloadBundles(cdnUrl, packages.ToArray(), null, loadingScreen);
        yield return StartPackageLoad(packages, true);
    }

    private IEnumerator StartPackageLoad(List<AirshipPackage> packages, bool usingBundles) {
        var st = Stopwatch.StartNew();
        yield return SystemRoot.Instance.LoadPackages(packages, usingBundles);
        Debug.Log($"Finished loading main menu packages in {st.ElapsedMilliseconds} ms.");

        Application.targetFrameRate = 140;

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