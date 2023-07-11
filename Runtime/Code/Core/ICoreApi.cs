namespace Assets.Code.Core
{
	public interface ICoreApi
	{
		static ICoreApi Instance { get; }
		CoreUserData GetCoreUserData();
	}
}