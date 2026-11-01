using UnityEngine;

namespace NeonBrawl
{
    /// <summary>
    /// Short phone vibrations for hits, kills and explosions (Android VibrationEffect, API 26+).
    /// Rate-limited so rapid fire never turns into a buzz. No-op on PC and in the editor.
    /// </summary>
    public sealed class Haptics
    {
        private readonly UserPrefs _prefs;
        private float _next;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool s_never;
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _effectClass;
        private bool _modern;
#endif

        public Haptics(UserPrefs prefs)
        {
            _prefs = prefs;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_never) Handheld.Vibrate(); // keeps the VIBRATE permission in the Android manifest
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _modern = version.GetStatic<int>("SDK_INT") >= 26;
                if (_modern) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Haptics unavailable: " + e.Message);
                _vibrator = null;
            }
#endif
        }

        public void Pulse(int milliseconds)
        {
            if (!_prefs.Vibration || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.07f;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null) return;
            try
            {
                if (_modern)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, -1))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", (long)milliseconds);
                }
            }
            catch (System.Exception)
            {
                _vibrator = null;
            }
#endif
        }
    }
}
