using System;
using UnityEngine;

namespace _Scripts.Analytics
{
    /// <summary>
    /// Analytics layer (launch blocker per Publishing &amp; Launch Plan / Roadmap growth review,
    /// HOD dispatch 2026-07-23). Same architecture as the ads stack (AdManager/IAdService):
    /// a static facade the gameplay code calls unconditionally, backed by a service chosen at
    /// boot. GameAnalytics is the chosen SDK — free for indie mobile, one self-contained
    /// unitypackage (no google-services.json, no second EDM4U resolver fighting AdMob's), and
    /// its progression/design/ad taxonomy maps 1:1 onto the events we need. The real service
    /// compiles in behind GAMEANALYTICS_ENABLED (set by GameAnalyticsImporter once the SDK
    /// package is imported); until then LogAnalyticsService prints every event to the console
    /// so the whole stream is verifiable in editor captures. NOTE the SDK also needs a game
    /// key + secret from the GameAnalytics dashboard (account creation — Ali's step, entered
    /// in the GA Settings asset via the editor GUI).
    ///
    /// Event coverage (per dispatch): session start/end, level start, gate pass (with type,
    /// label, resulting crowd), game over with cause (obstacle / gate_decrease / boss_battle —
    /// gate_decrease IS the "gate fail"), level complete, ad requested/shown/completed/failed.
    /// </summary>
    public interface IAnalyticsService
    {
        void Initialize();
        void LevelStart(int level);
        void LevelComplete(int level);
        void LevelFail(int level, string cause);
        void Design(string eventId, float value);
        void Ad(string action, string placement);
        void SessionEnd();
    }

    /// <summary>Console-logging fallback so the event stream exists (and is testable) before
    /// the SDK lands. Every line is grep-able as "[Analytics]".</summary>
    public sealed class LogAnalyticsService : IAnalyticsService
    {
        public void Initialize() => Debug.Log("[Analytics] init (log-only service — GAMEANALYTICS_ENABLED not set)");
        public void LevelStart(int level) => Debug.Log($"[Analytics] progression start Level{level:D3}");
        public void LevelComplete(int level) => Debug.Log($"[Analytics] progression complete Level{level:D3}");
        public void LevelFail(int level, string cause) => Debug.Log($"[Analytics] progression fail Level{level:D3} cause={cause}");
        public void Design(string eventId, float value) => Debug.Log($"[Analytics] design {eventId} = {value}");
        public void Ad(string action, string placement) => Debug.Log($"[Analytics] ad {action} placement={placement}");
        public void SessionEnd() => Debug.Log("[Analytics] session_end");
    }

#if GAMEANALYTICS_ENABLED
    /// <summary>Real backend. Compiles only once the GameAnalytics SDK package is imported
    /// (GameAnalyticsImporter sets the define). Session start/end is handled automatically by
    /// the SDK; SessionEnd here is a no-op beyond the SDK's own lifecycle hooks.</summary>
    public sealed class GameAnalyticsService : IAnalyticsService
    {
        public void Initialize() => GameAnalyticsSDK.GameAnalytics.Initialize();
        public void LevelStart(int level) => GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(
            GameAnalyticsSDK.GAProgressionStatus.Start, $"Level{level:D3}");
        public void LevelComplete(int level) => GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(
            GameAnalyticsSDK.GAProgressionStatus.Complete, $"Level{level:D3}");
        public void LevelFail(int level, string cause) => GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(
            GameAnalyticsSDK.GAProgressionStatus.Fail, $"Level{level:D3}", cause);
        public void Design(string eventId, float value) => GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventId, value);
        public void Ad(string action, string placement)
        {
            var adAction = action switch
            {
                "shown" => GameAnalyticsSDK.GAAdAction.Show,
                "completed" => GameAnalyticsSDK.GAAdAction.RewardReceived,
                "failed" => GameAnalyticsSDK.GAAdAction.FailedShow,
                _ => GameAnalyticsSDK.GAAdAction.Request,
            };
            var adType = placement == "interstitial"
                ? GameAnalyticsSDK.GAAdType.Interstitial : GameAnalyticsSDK.GAAdType.RewardedVideo;
            GameAnalyticsSDK.GameAnalytics.NewAdEvent(adAction, adType, "admob", placement);
        }
        public void SessionEnd() { } // SDK-managed
    }
#endif

    public static class Analytics
    {
        private static IAnalyticsService _service;
        private static bool _sessionEnded;

        /// <summary>Set by whichever system causes the loss, immediately before it pushes
        /// GameState.Lose; consumed once by the game-over log so causes can't leak across
        /// runs. "unknown" means a loss path we haven't instrumented.</summary>
        public static string PendingLossCause = "unknown";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_service != null) return;
#if GAMEANALYTICS_ENABLED
            _service = new GameAnalyticsService();
#else
            _service = new LogAnalyticsService();
#endif
            _service.Initialize();
            _service.Design("session_start", 1f);

            var go = new GameObject("[Analytics]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<LifecycleHook>();
        }

        public static void LevelStart(int level) => _service?.LevelStart(level);
        public static void LevelComplete(int level) => _service?.LevelComplete(level);

        public static void LevelFail(int level)
        {
            _service?.LevelFail(level, PendingLossCause);
            PendingLossCause = "unknown";
        }

        public static void GatePass(string type, string label, int crowdAfter) =>
            _service?.Design($"gate:pass:{type}:{label}", crowdAfter);

        public static void Ad(string action, string placement) => _service?.Ad(action, placement);

        /// <summary>On mobile, backgrounding IS the session end signal; quit rarely fires.
        /// Guarded so pause+quit in one teardown logs a single session_end, and unpausing
        /// re-arms it (GA itself treats resume as a new session).</summary>
        private sealed class LifecycleHook : MonoBehaviour
        {
            private void OnApplicationPause(bool paused)
            {
                if (paused) EndOnce();
                else _sessionEnded = false;
            }

            private void OnApplicationQuit() => EndOnce();

            private static void EndOnce()
            {
                if (_sessionEnded) return;
                _sessionEnded = true;
                _service?.SessionEnd();
                _service?.Design("session_end", 1f);
            }
        }
    }
}
