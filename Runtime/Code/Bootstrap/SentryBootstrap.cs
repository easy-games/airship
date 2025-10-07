#if AIRSHIP_PLAYER
using Code.Bundles;
using Sentry;
using Sentry.Unity;
using UnityEngine;

namespace Code.Bootstrap {
    public static class SentryBootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitSentry() {
            // Make sure this value is valid at runtime on all platforms
            var hash = AirshipVersion.GetVersionHash();
            if (string.IsNullOrEmpty(hash)) {
                hash = "unknown";
            }

            // If Sentry might already be auto-initialized, bail out
            if (SentrySdk.IsEnabled) {
                Debug.LogError("Sentry initialized before Airship sentry bootstrap ran. Cancelling custom Init()");
                return;
            }

            SentryUnity.Init(o => {
                o.Release = Application.version + "@" + AirshipVersion.GetVersionHash();

#if AIRSHIP_STAGING
            o.Environment = "staging";
#else
                o.Environment = "production";
#endif
            });
        }
    }
}
#endif