using System;
using Assets.Code.Luau;
using UnityEngine;

public class CoreScriptingManager : MonoBehaviour {
    private void Awake() {
        // Whenever we load into the CoreScene we set IsLoaded to false to trigger new core package script loading.
        ScriptingEntryPoint.IsLoaded = false;
    }
}