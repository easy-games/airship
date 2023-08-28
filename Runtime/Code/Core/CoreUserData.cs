namespace Assets.Code.Core
{
	public class CoreUserData
	{
		public string UserId;
		public string DisplayName;
		public string Email;
		public bool IsAnonymous;
		public bool IsEmailVerified;
		public string PhoneNumber;
		public string ProviderId;

		public ulong LastSignInTimestamp;
		public ulong CreationTimestamp;
	}
}