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

		public event AgonesAction test;

		private AgonesSdk _sdk;

		[Tooltip("Attempt to connect to Agones even when running the game within the Unity editor.")] [SerializeField]
		private bool attemptConnectInEditor;

		public async Task<int> DoNothingTest()
		{
			return 32;
		}

		public async Task<int> SleepTest(int seconds)
		{
			await Task.Delay(seconds * 1000);
			return 64;
		}

		private void OnDestroy()
		{
			Shutdown();
		}

		private void OnEnable()
		{
			AgonesCore.SetAgonesProxy(this);
		}

		private IEnumerator TestEvent()
		{
			yield return new WaitForSeconds(3f);
			test?.Invoke();
		}

		private void Start()
		{
			_sdk = GetComponent<AgonesSdk>();
			StartCoroutine(TestEvent());
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
