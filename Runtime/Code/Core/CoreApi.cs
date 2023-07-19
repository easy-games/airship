using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Newtonsoft.Json.Linq;
using Proyecto26;
using SocketIOClient;
using SocketIOClient.Transport;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Code.Core
{
	[LuauAPI]
	public class CoreAPI : MonoBehaviour
	{
		private readonly string GameCoordinatorUrl = $"https://game-coordinator-fxy2zritya-uc.a.run.app";

		private FirebaseAuth auth;
		private readonly Dictionary<string, FirebaseUser> userByAuth = new();

		public delegate void MessageReceivedDelegate(string messageName, string message);
		public event MessageReceivedDelegate GameCoordinatorEvent;

		public static CoreAPI Instance { get; private set; }

		public bool IsInitialized;
		public delegate void InitializedDelegate();
		public event InitializedDelegate InitializedEvent;

		public delegate void IdTokenChangedDelegate(string newTokenId);
		public event IdTokenChangedDelegate IdTokenChangedEvent;

		private string idToken;
		public string IdToken
		{
			get => idToken;
			private set
			{
				if (idToken != value)
				{
					if (sio != null)
					{
						SetSubscriptionState(false);
						sio.Dispose();
						sio = null;
					}

					var socketOptions = new SocketIOOptions()
					{
						Transport = TransportProtocol.WebSocket,
						Auth = new Dictionary<string, string>()
						{
							{ "token", value }
						},
					};

					sio = new SocketIO(new Uri(GameCoordinatorUrl), socketOptions);

					SetSubscriptionState(true);

					sio.ConnectAsync().ContinueWithOnMainThread(task => this.IdTokenChangedEvent.Invoke(value));

					idToken = value;
				}
			}
		}

		private SocketIO sio;
		private void Awake()
		{
			Instance = this;

			if (RunCore.IsServer())
			{
				return;
			}

			// When the app starts, check to make sure that
			// we have the required dependencies to use Firebase,
			// and if not, add them if possible.
			FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
			{
				if (task.Result == DependencyStatus.Available)
				{
					StartInitializeFirebase().ContinueWithOnMainThread(task =>
					{
						auth.CurrentUser.TokenAsync(true).ContinueWithOnMainThread(task =>
						{
							this.IdToken = task.Result;

							//Debug.Log($"CoreApi.Awake() pre InitializedEvent");

							InitializedEvent?.Invoke();

							//Debug.Log($"CoreApi.Awake() post InitializedEvent");

							IsInitialized = true;
						});
					});
				}
				else
				{
					Debug.LogError($"Could not resolve all Firebase dependencies: {task.Result}");
				}
			});
		}

		// Handle initialization of the necessary firebase modules.
		protected Task<AuthResult> StartInitializeFirebase()
		{
			Debug.Log("Setting up Firebase Auth");

			auth = FirebaseAuth.GetAuth(FirebaseApp.Create(new AppOptions()
			{
				ApiKey = "AIzaSyB04k_2lvM2VxcJqLKD6bfwdqelh6Juj2o",
				AppId = "1:987279961241:web:944327bc9353f4f1f15c08",
				ProjectId = "easygg-platform-staging",
			},
			"Primary"));

			auth.StateChanged += AuthStateChanged;
			auth.IdTokenChanged += IdTokenChanged;

			AuthStateChanged(this, null);

			return auth.SignInAnonymouslyAsync();
		}

		// Track state changes of the auth object.
		private void AuthStateChanged(object sender, EventArgs eventArgs)
		{
			var senderAuth = sender as FirebaseAuth;
			FirebaseUser user = null;

			if (senderAuth != null)
			{
				userByAuth.TryGetValue(senderAuth.App.Name, out user);
			}

			if (senderAuth == auth && senderAuth.CurrentUser != user)
			{
				var signedIn = user != senderAuth.CurrentUser && senderAuth.CurrentUser != null;
				if (!signedIn && user != null)
				{
					Debug.Log("Signed out " + user.UserId);
				}

				user = senderAuth.CurrentUser;
				userByAuth[senderAuth.App.Name] = user;

				if (signedIn)
				{
					Debug.Log("AuthStateChanged Signed in " + user.UserId);
					DisplayDetailedUserInfo(user, 1);
				}
			}
		}

		private void IdTokenChanged(object sender, EventArgs eventArgs)
		{
			var senderAuth = sender as FirebaseAuth;
			if (senderAuth == auth && senderAuth.CurrentUser != null)
			{
				senderAuth.CurrentUser.TokenAsync(false).ContinueWithOnMainThread(
					task => this.IdToken = task.Result);
			}
		}

		// Display a more detailed view of a FirebaseUser.
		protected void DisplayDetailedUserInfo(FirebaseUser user, int indentLevel)
		{
			var indent = new string(' ', indentLevel * 2);
			DisplayUserInfo(user, indentLevel);
			Debug.Log(string.Format("{0}Anonymous: {1}", indent, user.IsAnonymous));
			Debug.Log(string.Format("{0}Email Verified: {1}", indent, user.IsEmailVerified));
			Debug.Log(string.Format("{0}Phone Number: {1}", indent, user.PhoneNumber));

			var providerDataList = new List<IUserInfo>(user.ProviderData);
			var numberOfProviders = providerDataList.Count;
			if (numberOfProviders > 0)
			{
				for (int i = 0; i < numberOfProviders; ++i)
				{
					Debug.Log(string.Format("{0}Provider Data: {1}", indent, i));
					DisplayUserInfo(providerDataList[i], indentLevel + 2);
				}
			}
		}

		protected void DisplayUserInfo(IUserInfo userInfo, int indentLevel)
		{
			var indent = new string(' ', indentLevel * 2);
			var userProperties = new Dictionary<string, string> {
				{ "Display Name", userInfo.DisplayName },
				{ "Email", userInfo.Email },
				{ "Photo URL", userInfo.PhotoUrl != null ? userInfo.PhotoUrl.ToString() : null },
				{ "Provider ID", userInfo.ProviderId },
				{ "User ID", userInfo.UserId }
			};

			foreach (var property in userProperties)
			{
				if (!string.IsNullOrEmpty(property.Value))
				{
					Debug.Log(string.Format("{0}{1}: {2}", indent, property.Key, property.Value));
				}
			}
		}

		public CoreUserData GetCoreUserData()
		{
			if (RunCore.IsServer())
			{
				return null;
			}

			var currentUser = auth.CurrentUser;

			var coreUserData = new CoreUserData()
			{
				UserId = currentUser.UserId,
				DisplayName = currentUser.DisplayName,
				Email = currentUser.Email,
				IsAnonymous = currentUser.IsAnonymous,
				IsEmailVerified = currentUser.IsEmailVerified,
				PhoneNumber = currentUser.PhoneNumber,
				ProviderId = currentUser.ProviderId,
				LastSignInTimestamp = currentUser.Metadata.LastSignInTimestamp,
				CreationTimestamp = currentUser.Metadata.CreationTimestamp,
			};

			//Debug.Log($"GetCoreUserData() coreUserData: {JsonConvert.SerializeObject(coreUserData)}");

			return coreUserData;
		}

		public OnCompleteHook SendAsync(
			string url,
			string method,
			string utf8Body,
			string jsonParams,
			string jsonHeaders)
		{
			//Debug.Log($"CoreApi.SendAsync 0");
			var onCompleteHook = new OnCompleteHook();
			//Debug.Log($"CoreApi.SendAsync 1");
			var parameters = string.IsNullOrEmpty(jsonParams) ?
				null :
				JObject.Parse(jsonParams).ToObject<Dictionary<string, string>>();
			//Debug.Log($"CoreApi.SendAsync 2");
			var headers = string.IsNullOrEmpty(jsonHeaders) ?
				null :
				JObject.Parse(jsonHeaders).ToObject<Dictionary<string, string>>();
			//Debug.Log($"CoreApi.SendAsync 3");
			StartCoroutine(InternalSend(
				url,
				method,
				utf8Body,
				parameters,
				headers,
				onCompleteHook));
			//Debug.Log($"CoreApi.SendAsync 4");
			return onCompleteHook;
		}

		private IEnumerator InternalSend(
			string url,
			string method,
			string data,
			Dictionary<string, string> parameters,
			Dictionary<string, string> headers,
			OnCompleteHook onCompleteHook)
		{
			//Debug.Log($"CoreApi.InternalSend 0");
			Debug.Log($"CoreApi.InternalSend() url: {url}, method: {method}, data: {(string.IsNullOrEmpty(data) ? "null" : data)}, parameters: {(parameters == null ? "null" : string.Join(Environment.NewLine, parameters))}, headers: {(headers == null ? "null" : string.Join(Environment.NewLine, headers))}");

			var uploadHandler = string.IsNullOrEmpty(data) ? null : new UploadHandlerRaw(Encoding.UTF8.GetBytes(data));
			//Debug.Log($"CoreApi.InternalSend 1");
			var downloadHandler = new DownloadHandlerBuffer();
			//Debug.Log($"CoreApi.InternalSend 2");

			//Debug.Log($"CoreApi.InternalSend 3");
			var options = new RequestHelper()
			{
				Uri = url,
				Method = method,
				UploadHandler = uploadHandler,
				DownloadHandler = downloadHandler,
				Params = parameters,
				Headers = headers,
				EnableDebug = true,
			};

			yield return HttpBase.DefaultUnityWebRequest(options, new Action<RequestException, ResponseHelper>((exception, helper) =>
			{
				var isSuccess = helper.StatusCode >= 200 && helper.StatusCode <= 299;
				//Debug.Log($"CoreApi.InternalSend 7");
				var returnString = Encoding.UTF8.GetString(helper.Data);
				//Debug.Log($"CoreApi.InternalSend 8");
				var sendResult = new OperationResult(isSuccess, returnString);
				//Debug.Log($"CoreApi.InternalSend 9");
				onCompleteHook.Run(sendResult);
			}));

			//Debug.Log($"CoreApi.InternalSend 4");
			//var asyncOp = uwr.SendWebRequestWithOptions(options) as UnityWebRequestAsyncOperation;
			//Debug.Log($"CoreApi.InternalSend 5");
			//yield return asyncOp;
			//Debug.Log($"CoreApi.InternalSend 6");
			//var isSuccess = asyncOp.webRequest.result == UnityWebRequest.Result.Success;
			//Debug.Log($"CoreApi.InternalSend 7");
			//var returnString = asyncOp.webRequest.downloadHandler == null ? "" : Encoding.UTF8.GetString(asyncOp.webRequest.downloadHandler?.data);
			//Debug.Log($"CoreApi.InternalSend 8");
			//var sendResult = new OperationResult(isSuccess, returnString);
			//Debug.Log($"CoreApi.InternalSend 9");
			//onCompleteHook.Run(sendResult);
			//Debug.Log($"CoreApi.InternalSend 10");
		}

		public OnCompleteHook InitializeGameCoordinatorAsync()
		{
			var onCompleteHook = new OnCompleteHook();

			//sio.OnConnected += (object sender, EventArgs e) =>
			//{
			//	//Debug.Log($"CoreApi.ConnectAsync() sio.Connected: {sio.Connected}");

			//	onCompleteHook.Run(new OperationResult(
			//		isSuccess: sio.Connected,
			//		returnString: sio.Connected ?
			//			"" :
			//			$"Unable to connect to GameCoordinator. url: {GameCoordinatorUrl}"));
			//};

			sio.ConnectAsync().ContinueWithOnMainThread(task =>
			{
				onCompleteHook.Run(new OperationResult(
					isSuccess: sio.Connected,
					returnString: sio.Connected ?
						"" :
						$"Unable to connect to GameCoordinator. url: {GameCoordinatorUrl}"));
			});

			return onCompleteHook;
		}

		public OnCompleteHook EmitAsync(string eventName, string jsonEvent)
		{
			var onCompleteHook = new OnCompleteHook();

			var data = string.IsNullOrEmpty(jsonEvent) ? new object[0] : JObject.Parse(jsonEvent).ToObject<object[]>();

			sio.EmitAsync(eventName, data).ContinueWithOnMainThread(task =>
			{
				onCompleteHook.Run(new OperationResult(
					isSuccess: task.IsCompletedSuccessfully,
					returnString: task.IsCompletedSuccessfully ?
					"" :
					$"Unable to Emit event. eventName: {eventName}, jsonEvent: {jsonEvent}"));
			});

			return onCompleteHook;
		}

		private void OnDestroy()
		{
			SetSubscriptionState(false);
			sio?.DisconnectAsync();
			sio?.Dispose();
		}

		private void SetSubscriptionState(bool subscriptionState)
		{
			if (sio != null)
			{
				if (subscriptionState)
				{
					sio.OnConnected += Sio_OnConnected;
					sio.OnDisconnected += Sio_OnDisconnected;
					sio.OnError += Sio_OnError;
					sio.OnReconnected += Sio_OnReconnected;
					sio.OnReconnectError += Sio_OnReconnectError;
					sio.OnReconnectFailed += Sio_OnReconnectFailed;
					sio.OnPing += Sio_OnPing;
					sio.OnPong += Sio_OnPong;
					sio.OnAny(Sio_OnAny);
				}
				else
				{
					sio.OnConnected -= Sio_OnConnected;
					sio.OnDisconnected -= Sio_OnDisconnected;
					sio.OnError -= Sio_OnError;
					sio.OnReconnected -= Sio_OnReconnected;
					sio.OnReconnectError -= Sio_OnReconnectError;
					sio.OnReconnectFailed -= Sio_OnReconnectFailed;
					sio.OnPing -= Sio_OnPing;
					sio.OnPong -= Sio_OnPong;
					sio.OffAny(Sio_OnAny);
				}
			}
		}

		private void Sio_OnAny(string eventName, SocketIOResponse socketIOResponse)
		{
			var eventBody = socketIOResponse.ToString();

			try
			{
				GameCoordinatorEvent?.Invoke(eventName, eventBody);
			}
			catch(Exception e)
			{
				Debug.LogError($"Unable to process event. eventName: {eventName}, eventBody: {eventBody}. Error: {e}");
			}
		}

		private void Sio_OnPong(object sender, TimeSpan e)
		{
			//Debug.Log($"CoreApi.OnPong() e: {e}");
		}

		private void Sio_OnPing(object sender, EventArgs e)
		{
			//Debug.Log($"CoreApi.OnPing() e: {e}");
		}

		private void Sio_OnReconnectFailed(object sender, EventArgs e)
		{
			//Debug.Log($"CoreApi.OnReconnectFailed() e: {e}");
		}

		private void Sio_OnReconnectError(object sender, Exception e)
		{
			//Debug.Log($"CoreApi.OnReconnectError() e: {e}");
		}

		private void Sio_OnReconnected(object sender, int e)
		{
			//Debug.Log($"CoreApi.OnReconnected() e: {e}");
		}

		private void Sio_OnError(object sender, string e)
		{
			//Debug.Log($"CoreApi.OnError() e: {e}");
		}

		private void Sio_OnDisconnected(object sender, string e)
		{
			//Debug.Log($"CoreApi.OnDisconnected() e: {e}");
		}

		private void Sio_OnConnected(object sender, EventArgs e)
		{
			//Debug.Log($"CoreApi.OnConnected() e: {e}");
		}
	}
}