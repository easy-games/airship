using UnityEngine;

namespace Assets.Code.Core
{
	[LuauAPI]
	public class SocketIOMessageHook
	{
		public delegate void EventReceivedDelegate(string messageName, string message);

		public event EventReceivedDelegate EventReceived;

		public void Run(string messageName, string message)
		{
			//Debug.Log($"SocketIOMessageHook.Run() messageName: {messageName}, message: {message}");
			this.EventReceived?.Invoke(messageName, message);
		}
	}
}