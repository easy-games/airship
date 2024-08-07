namespace Code.Platform.Shared {
    [LuauAPI]
    public class AirshipPlatformUrl {
#if AIRSHIP_STAGING
        public static string gameCoordinatorSocket = "https://gc-edge-staging.easy.gg";
        public static string gameCoordinator = "https://game-coordinator-fxy2zritya-uc.a.run.app";
        public static string contentService = "https://content-service-fxy2zritya-uc.a.run.app";
        public static string dataStoreService = "https://data-store-service-fxy2zritya-uc.a.run.app";
        public static string deploymentService = "https://deployment-service-fxy2zritya-uc.a.run.app";
        public static string gameCdn = "https://gcdn-staging.easy.gg";
        public static string cdn = "https://cdn-staging.easy.gg";
        public static string mainWeb = "https://staging.airship.gg";
        public static string firebaseApiKey = "AIzaSyB04k_2lvM2VxcJqLKD6bfwdqelh6Juj2o";
#else
        public static string gameCoordinatorSocket = "https://gc-edge.airship.gg";
        public static string gameCoordinator = "https://game-coordinator-hwcvz2epka-uc.a.run.app";
        public static string contentService = "https://content-service-hwcvz2epka-uc.a.run.app";
        public static string dataStoreService = "https://data-store-service-hwcvz2epka-uc.a.run.app";
        public static string deploymentService = "https://deployment-service-hwcvz2epka-uc.a.run.app";
        public static string cdn = "https://cdn.airship.gg";
        public static string gameCdn = "https://gcdn.airship.gg";
        public static string mainWeb = "https://airship.gg";
        public static string firebaseApiKey = "AIzaSyAYw0C18Mt3wijT0ZHKGcS7zVdaPlR_sGI";
#endif
    }
}