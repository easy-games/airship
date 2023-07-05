using System.Collections;
using UnityEngine;

namespace Assets.Code.Agones
{
	public class AutoShutdownBridge : MonoBehaviour
	{
		[SerializeField]
		private int shutdownDelaySeconds = 120;

		private bool assetBundlesLoaded = false;
		public void SetBundlesLoaded(bool assetBundlesLoaded)
		{
			this.assetBundlesLoaded = assetBundlesLoaded;
		}

		private void Awake()
		{
			// this.StartCoroutine(this.CheckIfBundlesLoadedAfterDelay());
		}

		private IEnumerator CheckIfBundlesLoadedAfterDelay()
		{
			yield return new WaitForSeconds(this.shutdownDelaySeconds);

			if(!this.assetBundlesLoaded)
			{
				AgonesCore.Agones.Shutdown();
				Debug.Log($"Agones has been shutdown because not bundles were loaded.");
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