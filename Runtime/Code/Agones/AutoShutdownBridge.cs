using System;
using System.Collections;
using UnityEngine;

namespace Airship
{
	[LuauAPI]
	public class AutoShutdownBridge : MonoBehaviour
	{
		[SerializeField]
		private int shutdownDelaySeconds = 120;

		private bool assetBundlesLoaded = false;
		public void SetBundlesLoaded(bool assetBundlesLoaded)
		{
			this.assetBundlesLoaded = assetBundlesLoaded;
		}

		private void OnEnable() {
			if (RunCore.IsClient()) return;

			var serverBootstrap = FindObjectOfType<ServerBootstrap>();
			if (serverBootstrap != null) {
				serverBootstrap.OnStartLoadingGame += ServerBootstrap_OnStartLoadingGame;
			}
		}

		private void OnDisable() {
			if (RunCore.IsClient()) return;
			var serverBootstrap = FindObjectOfType<ServerBootstrap>();
			if (serverBootstrap != null) {
				serverBootstrap.OnStartLoadingGame -= ServerBootstrap_OnStartLoadingGame;
			}
		}

		private void ServerBootstrap_OnStartLoadingGame() {
#if UNITY_SERVER
			this.StartCoroutine(this.CheckIfBundlesLoadedAfterDelay());
#endif
		}

		private IEnumerator CheckIfBundlesLoadedAfterDelay()
		{
			yield return new WaitForSecondsRealtime(this.shutdownDelaySeconds);

			if(!this.assetBundlesLoaded)
			{
				AgonesCore.Agones.Shutdown();
				Debug.Log($"Agones has been shutdown because no bundles were loaded.");
			}
		}

		[SerializeField]
		private bool shutdownBridgeNextFrame = false;

		private void Update()
		{
			if(this.shutdownBridgeNextFrame)
			{
				this.shutdownBridgeNextFrame = false;

				AgonesCore.Agones.Shutdown();
				Debug.Log($"Agones has been shutdown.");
			}
		}
	}
}