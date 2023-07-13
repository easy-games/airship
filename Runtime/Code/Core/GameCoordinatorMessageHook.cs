using UnityEngine;

namespace Assets.Code.Core
{
	[LuauAPI]
	public class GameCoordinatorMessageHook
	{
		public delegate void MessageReceivedDelegate(string messageName, string message);

		public event MessageReceivedDelegate MessageReceived;

		public void Run(string messageName, string message)
		{
			//Debug.Log($"GameCoordinatorMessageHook.Run() messageName: {messageName}, message: {message}");
			this.MessageReceived?.Invoke(messageName, message);
		}
	}
}