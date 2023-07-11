using UnityEngine;

namespace Assets.Code.Core
{
	[LuauAPI]
	public class OnCompleteHook
	{
		public delegate void CompleteDelegate(OperationResult operationResult);

		public event CompleteDelegate CompleteEvent;

		public void Run(OperationResult operationResult)
		{
			//Debug.Log($"OnCompleteHook.Run() operationResult: {JsonUtility.ToJson(operationResult)}");
			this.CompleteEvent?.Invoke(operationResult);
		}
	}
}