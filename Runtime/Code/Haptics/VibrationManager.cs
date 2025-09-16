using System.Runtime.InteropServices;

namespace Code.Haptics {
    // Matches Vibration.mm
    public enum VibrationFeedbackType : int {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Selection = 3,
        NotificationSuccess = 4,
        NotificationWarning = 5,
        NotificationError = 6
    }
    
    [LuauAPI]
    public class VibrationManager {
#if UNITY_IOS
        // [DllImport("__Internal")]
        // private static extern void InitHaptics();
        //
        // [DllImport("__Internal")]
        // private static extern void DeinitHaptics();

        [DllImport("__Internal")]
        private static extern void PlayHaptic(int hapticType);
#endif

        public static void Play(VibrationFeedbackType vibrationFeedbackType) {
#if UNITY_IOS
            PlayHaptic((int) vibrationFeedbackType);
#endif
        }
    }
}