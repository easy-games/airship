using System;
using Assets.Code.Luau;
using Mirror;
using UnityEngine;

[LuauAPI(LuauContext.Protected)]
public class CoreScriptingManager : MonoBehaviour {
    /// <summary>
    /// Params: Scene scene, NetworkConnection connection, bool Added
    /// </summary>
    public event Action<object, object, object> OnClientPresenceChangeStart;

    /// <summary>
    /// Params: Scene scene, NetworkConnection connection, bool Added
    /// </summary>
    public event Action<object, object, object> OnClientPresenceChangeEnd;

    private void Awake() {
        // Whenever we load into the CoreScene we set IsLoaded to false to trigger new core package script loading.
        ScriptingEntryPoint.IsLoaded = false;

        // InstanceFinder.SceneManager.OnClientPresenceChangeStart += SceneManager_ClientPresenceChangeStart;
        // InstanceFinder.SceneManager.OnClientPresenceChangeEnd += SceneManager_ClientPresenceChangeEnd;
    }

    // private void SceneManager_ClientPresenceChangeStart(ClientPresenceChangeEventArgs args) {
    //     OnClientPresenceChangeStart?.Invoke(args.Scene, args.Connection, args.Added);
    // }
    //
    // private void SceneManager_ClientPresenceChangeEnd(ClientPresenceChangeEventArgs args) {
    //     OnClientPresenceChangeEnd?.Invoke(args.Scene, args.Connection, args.Added);
    // }

    private void OnDestroy() {
        // if (InstanceFinder.SceneManager != null) {
        //     InstanceFinder.SceneManager.OnClientPresenceChangeStart -= SceneManager_ClientPresenceChangeStart;
        //     InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= SceneManager_ClientPresenceChangeEnd;
        // }
    }
}