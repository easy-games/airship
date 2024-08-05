namespace Code.Platform.Shared {
    [LuauAPI]
    public class AirshipPlatformUrl {
#if AIRSHIP_STAGING
        public static string GameCoordinatorSocket = "https://gc-edge-staging.easy.gg";
        public static string GameCoordinator = "https://game-coordinator-fxy2zritya-uc.a.run.app";
        public static string ContentService = "https://content-service-fxy2zritya-uc.a.run.app";
        public static string DataStoreService = "https://data-store-service-fxy2zritya-uc.a.run.app";
        public static string DeploymentService = "https://deployment-service-fxy2zritya-uc.a.run.app";
        public static string CDN = "https://cdn-staging.easy.gg";
#else
        public static string GameCoordinatorSocket = "https://gc-edge.easy.gg";
        public static string GameCoordinator = "https://game-coordinator-hwcvz2epka-uc.a.run.app";
        public static string ContentService = "https://content-service-hwcvz2epka-uc.a.run.app";
        public static string DataStoreService = "https://data-store-service-hwcvz2epka-uc.a.run.app";
        public static string DeploymentService = "https://deployment-service-hwcvz2epka-uc.a.run.app";
        public static string CDN = "https://cdn.airship.gg";
#endif
    }
}