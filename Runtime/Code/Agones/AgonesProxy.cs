using System;
using System.Collections;
using System.Threading.Tasks;
using Agones;
using UnityEngine;

namespace Airship
{
	[LuauAPI]
	public class AgonesProxy : MonoBehaviour
	{
		public delegate void AgonesAction();

		public event AgonesAction connected;
		public event AgonesAction ready;

		private AgonesSdk _sdk;

		[Tooltip("Attempt to connect to Agones even when running the game within the Unity editor.")] [SerializeField]
		private bool attemptConnectInEditor;

		public async Task<int> DoTestTask()
		{
			print("WAITING 2 SECONDS...");
			await Task.Delay(2000);
			print("WAITED 2 SECONDS");
			return 32;
		}

		private void OnDestroy()
		{
			Shutdown();
		}

		private void OnEnable()
		{
			AgonesCore.SetAgonesProxy(this);
		}

		private void Start()
		{
			_sdk = GetComponent<AgonesSdk>();
		}

		public void Connect()
		{
			if (!RunCore.IsServer()) return;
#if UNITY_EDITOR
		if (attemptConnectInEditor) {
			SignalConnect();
		} else {
			StartCoroutine(MockSignalConnect());
		}
#else
			SignalConnect();
#endif
		}

		public void Ready()
		{
			if (!RunCore.IsServer()) return;
#if UNITY_EDITOR
		if (attemptConnectInEditor) {
			SignalReady();
		} else {
			StartCoroutine(MockSignalReady());
		}
#else
			SignalReady();
#endif
		}

		public void Shutdown()
		{
			if (!RunCore.IsServer()) return;
#if UNITY_EDITOR
		if (attemptConnectInEditor) {
			SignalShutdown();
		}
#else
			SignalShutdown();
#endif
		}

		private async void SignalConnect()
		{
			var connectSuccess = await _sdk.Connect();
			if (connectSuccess)
			{
				connected?.Invoke();
			}
			else
			{
				Debug.LogError("Failed to connect to Agones");
			}
		}

		private async void SignalReady()
		{
			var readySuccess = await _sdk.Ready();
			if (readySuccess)
			{
				ready?.Invoke();
			}
			else
			{
				Debug.LogError("Failed to mark Agones as ready");
			}
		}

		private async void SignalShutdown()
		{
			var shutdownSuccess = await _sdk.Shutdown();
			if (!shutdownSuccess)
			{
				Debug.LogError("Failed to signal shutdown");
			}
		}

#if UNITY_EDITOR
	private IEnumerator MockSignalConnect() {
		yield return new WaitForSeconds(0);
		print("Mock Agones connect");
		connected?.Invoke();
	}
	private IEnumerator MockSignalReady() {
		print("Mock Agones ready");
		yield return new WaitForSeconds(0);
		ready?.Invoke();
	}
#endif
	}
}
