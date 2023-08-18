using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Code.Bootstrap;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class MainMenuSceneManager : MonoBehaviour {
    private void Start() {
        StartCoroutine(this.StartLoadingCoroutine());
    }

    private IEnumerator StartLoadingCoroutine() {


        // TODO: download core package

        List<AirshipPackage> packages = new();
        packages.Add(new AirshipPackage("Core", "LocalBuild", AirshipPackageType.Package));
        var st = Stopwatch.StartNew();
        yield return SystemRoot.Instance.LoadPackages(packages, false);
        Debug.Log($"Finished loading main menu packages in {st.ElapsedMilliseconds} ms.");

        var coreLuauBindingGO = new GameObject("CoreLuauBinding");
        var coreLuauBinding = coreLuauBindingGO.AddComponent<LuauBinding>();
        coreLuauBinding.m_fileFullPath = "imports/core/shared/resources/ts/mainmenu.lua";
        coreLuauBinding.Init();
    }
}