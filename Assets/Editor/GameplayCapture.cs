// Marker-driven gameplay screenshot capture, same automation pattern as AutoBuildTrigger.
// Marker: capture-gameplay-request (project root). Enters Play Mode on Level.unity, waits a
// few seconds for the splash/boot to clear and the run to actually start, captures a PNG via
// Unity's own ScreenCapture API, then exits Play Mode automatically. No external screen/GUI
// automation involved — this is Unity capturing its own Game view to disk.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using _Scripts.Core;

[InitializeOnLoad]
public static class GameplayCapture
{
    private const string Marker = "/Users/a/blue-vs-orange-runner/base/capture-gameplay-request";
    public const string OutputPath = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture.png";
    // Later-run shots (HOD directive 2026-07-21 item 4): the first shot lands ~1 gate into
    // the run, which left the checker line, boss arena, and win screen permanently
    // unverified. Mid catches the track past the first gates; end catches boss/outcome.
    public const string OutputPathMid = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-mid.png";
    public const string OutputPathEnd = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-end.png";
    // Event-triggered, not time-triggered: fires ~0.2s after ArmyController starts a death
    // beat, so the shot lands mid-fall — the only reliable way to catch a ~0.45s exit
    // animation in a screenshot.
    public const string OutputPathLoss = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-loss.png";

    private enum State { Idle, RequestedPlay, WaitingInPlayMode, Captured }

    // BUG FOUND 2026-07-20 (HOD): this project has EnterPlayModeOptionsEnabled=0, i.e. Unity's
    // default FULL domain reload on every Play Mode entry. Plain static fields (the old
    // `_state`/`_framesInPlayMode`) get wiped back to their defaults the instant Play Mode
    // actually starts — AFTER the marker file was already deleted — so this permanently
    // stranded itself in Idle with nothing left to react to, every single time, regardless of
    // Editor focus. SessionState survives domain reload (that's exactly what it's for); a
    // plain static field does not. This is the actual fix, not a timing workaround.
    private const string StateKey = "GameplayCapture_State";
    private const string PlayModeStartKey = "GameplayCapture_PlayModeStartTime";
    private const string RunStartedKey = "GameplayCapture_RunStarted";

    private static State CurrentState
    {
        get => (State)SessionState.GetInt(StateKey, (int)State.Idle);
        set => SessionState.SetInt(StateKey, (int)value);
    }

    // Real elapsed wall-clock time, not a frame count: EditorApplication.update ticks do NOT
    // map 1:1 to rendered Game-view frames (confirmed 2026-07-20 — a 240-tick wait fired in
    // well under the splash's own ~2.3s fade timer and captured mid-splash instead of
    // gameplay). EditorApplication.timeSinceStartup is a monotonic double in real seconds,
    // unaffected by Time.timeScale or Editor frame-rate throttling.
    private static double PlayModeStartTime
    {
        get => SessionState.GetFloat(PlayModeStartKey, 0f);
        set => SessionState.SetFloat(PlayModeStartKey, (float)value);
    }

    private static bool RunStarted
    {
        get => SessionState.GetBool(RunStartedKey, false);
        set => SessionState.SetBool(RunStartedKey, value);
    }

    // BUG FOUND 2026-07-20 (HOD): the first successful capture grabbed the SPLASH/Start screen,
    // not gameplay — because the game boots to GameState.Start and waits for a player tap on the
    // Play button that never comes in an automated capture. Waiting longer never helped: nothing
    // starts the run. Fix: after the splash clears, programmatically force the run to start
    // (GameFlowManager.UpdateGameState(GameState.Game)) exactly once, then give the crowd a few
    // seconds to spawn and move down the track before shooting. Timeline below.
    private const double SecondsBeforeStartingRun = 3.0; // let the ~2.3s splash fully clear first
    private const double SecondsToWaitBeforeCapture = 8.0; // 5s of live gameplay after run start
    private const double SecondsForMidCapture = 18.0;      // past the first gates
    private const double SecondsForEndCapture = 32.0;      // boss arena / outcome screen

    private const string Shot1Key = "GameplayCapture_Shot1";
    private const string Shot2Key = "GameplayCapture_Shot2";
    private const string LossShotKey = "GameplayCapture_LossShot";
    private static bool LossShotDone
    {
        get => SessionState.GetBool(LossShotKey, false);
        set => SessionState.SetBool(LossShotKey, value);
    }
    private static bool Shot1Done
    {
        get => SessionState.GetBool(Shot1Key, false);
        set => SessionState.SetBool(Shot1Key, value);
    }
    private static bool Shot2Done
    {
        get => SessionState.GetBool(Shot2Key, false);
        set => SessionState.SetBool(Shot2Key, value);
    }

    static GameplayCapture()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (CurrentState == State.Idle)
        {
            if (!File.Exists(Marker)) return;
            File.Delete(Marker);
            Debug.Log("[GameplayCapture] marker found — entering Play Mode on Level.unity to capture a screenshot");
            EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);
            CurrentState = State.RequestedPlay; // set BEFORE isPlaying=true triggers the reload
            EditorApplication.isPlaying = true;
            return;
        }

        if (CurrentState == State.RequestedPlay)
        {
            if (!EditorApplication.isPlaying) return; // still spinning up
            PlayModeStartTime = EditorApplication.timeSinceStartup;
            RunStarted = false;
            CurrentState = State.WaitingInPlayMode;
            return;
        }

        if (CurrentState == State.WaitingInPlayMode)
        {
            if (!EditorApplication.isPlaying)
            {
                // Play Mode ended unexpectedly (compile error, exception) before capture.
                Debug.LogError("[GameplayCapture] FAILED: exited Play Mode before a screenshot was captured");
                CurrentState = State.Idle;
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - PlayModeStartTime;

            // Once the splash has cleared, force the run to start (nothing else will) so the
            // capture lands on actual crowd gameplay instead of the Start screen.
            if (!RunStarted && elapsed >= SecondsBeforeStartingRun)
            {
                if (GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.UpdateGameState(GameState.Game);
                    Debug.Log("[GameplayCapture] forced GameState.Game to start the run for capture");
                }
                else
                {
                    Debug.LogWarning("[GameplayCapture] GameFlowManager.Instance was null when trying to start the run");
                }
                RunStarted = true;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) !);

            // loss moment: snap ~0.2s into a death beat so falling units are mid-tween
            double lastKill = _Scripts.Controllers.ArmyController.LastKillRealtime;
            if (!LossShotDone && lastKill > 0 &&
                Time.realtimeSinceStartupAsDouble - lastKill >= 0.2 &&
                Time.realtimeSinceStartupAsDouble - lastKill <= 0.4)
            {
                ScreenCapture.CaptureScreenshot(OutputPathLoss);
                LossShotDone = true;
                Debug.Log($"[GameplayCapture] loss-moment shot requested at {OutputPathLoss}");
            }

            if (!Shot1Done && elapsed >= SecondsToWaitBeforeCapture)
            {
                ScreenCapture.CaptureScreenshot(OutputPath);
                Shot1Done = true;
                Debug.Log($"[GameplayCapture] shot 1 (early run) requested at {OutputPath}");
            }
            if (!Shot2Done && elapsed >= SecondsForMidCapture)
            {
                ScreenCapture.CaptureScreenshot(OutputPathMid);
                Shot2Done = true;
                Debug.Log($"[GameplayCapture] shot 2 (mid run) requested at {OutputPathMid}");
            }
            if (elapsed >= SecondsForEndCapture)
            {
                ScreenCapture.CaptureScreenshot(OutputPathEnd);
                Debug.Log($"[GameplayCapture] shot 3 (end/boss) requested at {OutputPathEnd} — SUCCESS");
                CurrentState = State.Captured;
            }
            return;
        }

        if (CurrentState == State.Captured)
        {
            // CaptureScreenshot writes asynchronously (end of frame); give it one extra
            // frame before leaving Play Mode so the files are actually flushed to disk.
            if (!File.Exists(OutputPathEnd)) return;
            EditorApplication.isPlaying = false;
            Shot1Done = false;
            Shot2Done = false;
            LossShotDone = false;
            CurrentState = State.Idle;
        }
    }

}
