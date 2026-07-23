using System;
using UnityEngine;

namespace _Scripts.Analytics
{
    /// <summary>
    /// Analytics layer (launch blocker per Publishing &amp; Launch Plan / Roadmap growth review,
    /// HOD dispatch 2026-07-23). Same architecture as the ads stack (AdManager/IAdService):
    /// a static facade the gameplay code calls unconditionally, backed by a service chosen at
    /// boot. Vendor: **PostHog** (Ali's call 2026-07-23 — one analytics vendor studio-wide;
    /// BloomStrike already runs it), via the official posthog-unity UPM package (git URL in
    /// Packages/manifest.json; beta, Unity 2021.3+, capture batching + automatic exception
    /// events built in). The real service compiles in behind POSTHOG_ENABLED (set by the
    /// PostHogSetup editor script once the package resolves) and activates only when the
    /// project API key exists at Resources/PostHogKey.txt (line 1 = phc_ key, optional line 2
    /// = host, default https://us.i.posthog.com) — the key is Ali's step, same pattern as the
    /// AdMob keys. Until then LogAnalyticsService prints every event to the console so the
    /// whole stream stays verifiable in editor captures.
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
        void GatePass(string type, string label, int crowdAfter);
        void Design(string eventId, float value);
        void Ad(string action, string placement);
        void SessionEnd();
    }

    /// <summary>Console-logging fallback so the event stream exists (and is testable) before
    /// the SDK lands. Every line is grep-able as "[Analytics]".</summary>
    public sealed class LogAnalyticsService : IAnalyticsService
    {
        public void Initialize() => Debug.Log("[Analytics] init (log-only service — POSTHOG_ENABLED off or Resources/PostHogKey.txt missing)");
        public void LevelStart(int level) => Debug.Log($"[Analytics] progression start Level{level:D3}");
        public void LevelComplete(int level) => Debug.Log($"[Analytics] progression complete Level{level:D3}");
        public void LevelFail(int level, string cause) => Debug.Log($"[Analytics] progression fail Level{level:D3} cause={cause}");
        public void GatePass(string type, string label, int crowdAfter) => Debug.Log($"[Analytics] gate_pass type={type} label={label} crowd={crowdAfter}");
        public void Design(string eventId, float value) => Debug.Log($"[Analytics] design {eventId} = {value}");
        public void Ad(string action, string placement) => Debug.Log($"[Analytics] ad {action} placement={placement}");
        public void SessionEnd() => Debug.Log("[Analytics] session_end");
    }

#if POSTHOG_ENABLED
    /// <summary>Real backend over the official posthog-unity SDK. Compiles once the UPM
    /// package resolves (PostHogSetup sets the define); constructed only when the API key
    /// file exists. Everything maps onto PostHog's capture(event, properties) model; the SDK
    /// batches internally (FlushAt) and auto-captures unhandled exceptions as $exception.</summary>
    public sealed class PostHogAnalyticsService : IAnalyticsService
    {
        private readonly string _apiKey;
        private readonly string _host;

        public PostHogAnalyticsService(string apiKey, string host)
        {
            _apiKey = apiKey;
            _host = host;
        }

        public void Initialize() => PostHogUnity.PostHog.Setup(new PostHogUnity.PostHogConfig
        {
            ApiKey = _apiKey,
            Host = _host,
        });

        public void LevelStart(int level) => PostHogUnity.PostHog.Capture("level_start",
            new System.Collections.Generic.Dictionary<string, object> { { "level", level } });

        public void LevelComplete(int level) => PostHogUnity.PostHog.Capture("level_complete",
            new System.Collections.Generic.Dictionary<string, object> { { "level", level } });

        public void LevelFail(int level, string cause) => PostHogUnity.PostHog.Capture("level_fail",
            new System.Collections.Generic.Dictionary<string, object> { { "level", level }, { "cause", cause } });

        // One event name with properties, never value-embedded names ("gate:pass:+20" as an
        // event id would mint a new event type per gate label and wreck PostHog insights).
        public void GatePass(string type, string label, int crowdAfter) => PostHogUnity.PostHog.Capture("gate_pass",
            new System.Collections.Generic.Dictionary<string, object>
                { { "type", type }, { "label", label }, { "crowd", crowdAfter } });

        public void Design(string eventId, float value) => PostHogUnity.PostHog.Capture(eventId,
            new System.Collections.Generic.Dictionary<string, object> { { "value", value } });

        public void Ad(string action, string placement) => PostHogUnity.PostHog.Capture("ad",
            new System.Collections.Generic.Dictionary<string, object> { { "action", action }, { "placement", placement } });

        public void SessionEnd() => PostHogUnity.PostHog.Flush();
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
#if POSTHOG_ENABLED
            // Key file: Resources/PostHogKey.txt — line 1 = phc_ project key, optional
            // line 2 = ingestion host. Missing/empty file -> log-only service, no network.
            var keyFile = Resources.Load<TextAsset>("PostHogKey");
            var lines = keyFile != null
                ? keyFile.text.Split('\n', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>();
            if (lines.Length > 0 && lines[0].Trim().StartsWith("phc_"))
                _service = new PostHogAnalyticsService(lines[0].Trim(),
                    lines.Length > 1 ? lines[1].Trim() : "https://us.i.posthog.com");
            else
                _service = new LogAnalyticsService();
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
            _service?.GatePass(type, label, crowdAfter);

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
