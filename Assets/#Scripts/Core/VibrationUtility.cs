using UnityEngine;
using UnityEngine.Android;

namespace CardClash
{
    /// <summary>
    /// Provides cross-platform controlled vibration.
    /// On Android, duration (ms) and amplitude (1-255) are respected via native Vibrator APIs.
    /// On other platforms (iOS, Editor, etc.), falls back to Unity's default Handheld.Vibrate().
    /// </summary>
    public static class VibrationUtility
    {
        private const string VibrationKey = "Vibration";

        private static bool? _vibrationEnabled;

        public static bool IsEnabled
        {
            get => _vibrationEnabled ??= PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            private set => _vibrationEnabled = value;
        }

        /// <summary>
        /// Enables or disables vibration at runtime. Call this when the user changes the setting.
        /// </summary>
        public static void SetEnabled(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// Vibrates the device for a specific duration and intensity, if vibration is enabled.
        /// </summary>
        /// <param name="milliseconds">Duration of the vibration in milliseconds.</param>
        /// <param name="amplitude">Intensity from 1 (weak) to 255 (strong). Ignored on unsupported platforms.</param>
        public static void Vibrate(long milliseconds, int amplitude = 255)
        {
            if (!IsEnabled)
                return;

#if UNITY_SERVER
            return;
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    return;

                int sdkInt = GetAndroidSdkInt();

                if (sdkInt >= 26)
                {
                    using var vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    int clampedAmplitude = Mathf.Clamp(amplitude, 1, 255);
                    using var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", milliseconds, clampedAmplitude);
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
#else
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static int GetAndroidSdkInt()
        {
            using var versionClass = new AndroidJavaClass("android.os.Build$VERSION");
            return versionClass.GetStatic<int>("SDK_INT");
        }
#endif
    }
}
