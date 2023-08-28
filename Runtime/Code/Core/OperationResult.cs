namespace Assets.Code.Core
{
	public class OperationResult
	{
		public bool IsSuccess;
		public string ReturnString;

		public OperationResult(bool isSuccess, string returnString)
		{
			IsSuccess = isSuccess;
			ReturnString = returnString;
		}
	}
}