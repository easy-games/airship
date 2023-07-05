public class BootstrapHelper
{
	public static string CoreBundleId = "core";
	public static string GameBundleId = "bedwars";

	public static string CoreBundleVersionFileName = $"{CoreBundleId}_bundle_version.txt";
	public static string GameBundleVersionFileName = $"{GameBundleId}_bundle_version.txt";

	// NOTE: For now, we're building our bundles into the game's bundle folder.
	public static string CoreBundleRelativeRootPath = $"assets/game/{GameBundleId}/bundles/";
	public static string GameBundleRelativeRootPath = $"assets/game/{GameBundleId}/bundles/";
}