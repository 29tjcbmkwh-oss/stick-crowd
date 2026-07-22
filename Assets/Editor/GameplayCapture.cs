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
    // A genuine win screen: the tug-of-war boss battle needs taps (BattleController drains
    // playerBarAmount every frame; only Decrease() pushes back), so an unattended run always
    // loses. The capture auto-taps through the battle and shoots the win screen ~1.2s after
    // GameState.Win so the popup + coin fountain are in frame.
    public const string OutputPathWin = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-win.png";

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
    private const double SecondsBeforeStartingRun = 4.2; // splash clears ~2.5s; leaves a window to shoot the skin store first
    private const double SecondsToWaitBeforeCapture = 8.0; // 5s of live gameplay after run start
    private const double SecondsForMidCapture = 18.0;      // past the first gates
    private const double SecondsForEndCapture = 32.0;      // boss arena approach
    private const double MaxRunSeconds = 100.0;            // hard cap if the win never comes

    private const string Shot1Key = "GameplayCapture_Shot1";
    private const string Shot2Key = "GameplayCapture_Shot2";
    private const string LossShotKey = "GameplayCapture_LossShot";
    private static bool LossShotDone
    {
        get => SessionState.GetBool(LossShotKey, false);
        set => SessionState.SetBool(LossShotKey, value);
    }
    private const string Shot3Key = "GameplayCapture_Shot3";
    private static bool Shot3Done
    {
        get => SessionState.GetBool(Shot3Key, false);
        set => SessionState.SetBool(Shot3Key, value);
    }
    private const string WinTimeKey = "GameplayCapture_WinTime";
    private static double WinTime
    {
        get => SessionState.GetFloat(WinTimeKey, 0f);
        set => SessionState.SetFloat(WinTimeKey, (float)value);
    }
    private static double LastTapTime; // per-frame cadence only; fine as a plain static
    private static _Scripts.Core.PerfProbe _perfProbe; // wiped by domain reload on play-enter, assigned after
    public const string OutputPathBattle = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-battle.png";
    private const string BattleTimeKey = "GameplayCapture_BattleTime";
    private static double BattleTime
    {
        get => SessionState.GetFloat(BattleTimeKey, 0f);
        set => SessionState.SetFloat(BattleTimeKey, (float)value);
    }
    public const string OutputPathStore = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-store.png";
    public const string OutputPathStart = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-start.png";
    private const string StartShotKey = "GameplayCapture_StartShot";
    private static bool StartShotDone
    {
        get => SessionState.GetBool(StartShotKey, false);
        set => SessionState.SetBool(StartShotKey, value);
    }
    public const string OutputPathReward = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-reward.png";
    public const string OutputPathRewardReveal = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture-reward-reveal.png";
    private const string StoreShotKey = "GameplayCapture_StoreShot";
    private static bool StoreShotDone
    {
        get => SessionState.GetBool(StoreShotKey, false);
        set => SessionState.SetBool(StoreShotKey, value);
    }
    private const string WinStepKey = "GameplayCapture_WinStep";
    private static int WinStep
    {
        get => SessionState.GetInt(WinStepKey, 0);
        set => SessionState.SetInt(WinStepKey, value);
    }
    private const string BattleShotKey = "GameplayCapture_BattleShot";
    private static bool BattleShotDone
    {
        get => SessionState.GetBool(BattleShotKey, false);
        set => SessionState.SetBool(BattleShotKey, value);
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

    private static void ClickButtonWithLabel(string label)
    {
        foreach (var b in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>())
        {
            var t = b.GetComponentInChildren<TMPro.TMP_Text>();
            if (t != null && t.text == label && b.interactable)
            {
                b.onClick.Invoke();
                return;
            }
        }
        Debug.LogWarning($"[GameplayCapture] no clickable button labeled '{label}' found");
    }

    static GameplayCapture()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (CurrentState == State.Idle)
        {
            // Never enter Play Mode with a compile pending: the play session would run the
            // OLD assemblies while the reload waits, silently testing stale code (bit us
            // repeatedly on 2026-07-21 — passes/plays that raced the reload).
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(Marker)) return;
            File.Delete(Marker);
            // Equip the RETRO GUY texture skin for this automated run so the capture proves
            // the texture-skin path end-to-end (Cat.Awake reads equipped at spawn). Reset on
            // exit so a human tester starts from CLASSIC.
            PlayerPrefs.SetInt("skin_owned_1", 1);
            PlayerPrefs.SetInt("skin_equipped", 1);
            PlayerPrefs.Save();
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

            // Start-screen shot BEFORE the store opens: verifies the start UI itself —
            // notably the leaderboard RANK button, which the UIManager/Canvas parenting bug
            // had made invisible since it was built (HOD item 2: never once seen in pixels).
            if (!StartShotDone && elapsed >= 2.5 && !RunStarted)
            {
                ScreenCapture.CaptureScreenshot(OutputPathStart);
                bool found = false;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.name != "SkinStoreButton" || !go.scene.IsValid()) continue;
                    found = true;
                    var chain = "";
                    for (var t = go.transform; t != null; t = t.parent)
                        chain = $"{t.name}(active={t.gameObject.activeSelf}) > " + chain;
                    var r = (RectTransform)go.transform;
                    Debug.Log($"[GameplayCapture] SkinStoreButton activeInHierarchy={go.activeInHierarchy} chain: {chain} anchoredPos={r.anchoredPosition} sizeDelta={r.sizeDelta}");
                }
                if (!found) Debug.Log("[GameplayCapture] SkinStoreButton not present AT ALL (destroyed?)");
                StartShotDone = true;
                Debug.Log($"[GameplayCapture] start-screen shot requested at {OutputPathStart}");
            }

            // Group B store verification: open the skin store on the start screen, shoot it,
            // close it, all before the run gets forced below.
            if (!StoreShotDone && elapsed >= 2.9 && elapsed < SecondsBeforeStartingRun && !RunStarted)
            {
                var uim = UnityEngine.Object.FindObjectOfType<_Scripts.Core.UIManager>();
                // startButton anchors the real screen canvas; UIManager itself sits on the
                // Managers object outside the canvas (same trap as UIManager.Start had)
                var canvas = uim != null && uim.startButton != null
                    ? uim.startButton.GetComponentInParent<Canvas>() : null;
                if (canvas != null && !_Scripts.Core.SkinStorePanel.IsOpen)
                    _Scripts.Core.SkinStorePanel.Open(canvas.transform);
                if (elapsed >= 3.6 && _Scripts.Core.SkinStorePanel.IsOpen)
                {
                    ScreenCapture.CaptureScreenshot(OutputPathStore);
                    StoreShotDone = true;
                    Debug.Log($"[GameplayCapture] skin store shot requested at {OutputPathStore}");
                    // Close on a LATER tick: CaptureScreenshot grabs at END of frame, and a
                    // same-frame Close() destroyed the panel before the grab (22:38 shot
                    // showed the start screen instead of the store).
                }
            }
            if (StoreShotDone && elapsed >= 3.95 && _Scripts.Core.SkinStorePanel.IsOpen)
            {
                _Scripts.Core.SkinStorePanel.Close();
            }

            // Once the splash has cleared, force the run to start (nothing else will) so the
            // capture lands on actual crowd gameplay instead of the Start screen.
            if (!RunStarted && elapsed >= SecondsBeforeStartingRun)
            {
                if (GameFlowManager.Instance != null)
                {
                    GameFlowManager.Instance.UpdateGameState(GameState.Game);
                    _perfProbe = _Scripts.Core.PerfProbe.Begin(); // HOD item 3: frame/GC stats over the run
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
            if (!Shot3Done && elapsed >= SecondsForEndCapture)
            {
                ScreenCapture.CaptureScreenshot(OutputPathEnd);
                Shot3Done = true;
                Debug.Log($"[GameplayCapture] shot 3 (end/boss) requested at {OutputPathEnd}");
            }

            // Auto-tap through the tug-of-war so the run can genuinely WIN (the battle
            // drains without input — an unattended run always loses).
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.state == GameState.Battle)
            {
                if (BattleTime <= 0) BattleTime = EditorApplication.timeSinceStartup;
                // battle-phase shot: the only moment the boss health bar visibly drains
                if (!BattleShotDone && EditorApplication.timeSinceStartup - BattleTime >= 1.6)
                {
                    ScreenCapture.CaptureScreenshot(OutputPathBattle);
                    BattleShotDone = true;
                    Debug.Log($"[GameplayCapture] battle shot requested at {OutputPathBattle}");
                }
                if (EditorApplication.timeSinceStartup - LastTapTime > 0.35)
                {
                    var battle = UnityEngine.Object.FindObjectOfType<_Scripts.Controllers.BattleController>();
                    if (battle != null && battle.BattleRunning) battle.Decrease();
                    LastTapTime = EditorApplication.timeSinceStartup;
                }
            }

            if (GameFlowManager.Instance != null && GameFlowManager.Instance.state == GameState.Win)
            {
                if (WinTime <= 0) WinTime = EditorApplication.timeSinceStartup;
                double sinceWin = EditorApplication.timeSinceStartup - WinTime;
                // Group B verification sequence: reward boxes -> pick -> reveal -> continue -> win panel
                if (WinStep == 0 && sinceWin >= 0.8)
                {
                    ScreenCapture.CaptureScreenshot(OutputPathReward);
                    Debug.Log("[GameplayCapture] reward-boxes shot requested");
                    WinStep = 1;
                }
                else if (WinStep == 1 && sinceWin >= 1.4)
                {
                    ClickButtonWithLabel("?");
                    WinStep = 2;
                }
                else if (WinStep == 2 && sinceWin >= 2.4)
                {
                    ScreenCapture.CaptureScreenshot(OutputPathRewardReveal);
                    Debug.Log("[GameplayCapture] reward-reveal shot requested");
                    WinStep = 3;
                }
                else if (WinStep == 3 && sinceWin >= 3.0)
                {
                    ClickButtonWithLabel("CONTINUE");
                    WinStep = 4;
                }
                else if (WinStep == 4 && sinceWin >= 4.0)
                {
                    ScreenCapture.CaptureScreenshot(OutputPathWin);
                    Debug.Log($"[GameplayCapture] win-screen shot requested at {OutputPathWin} — SUCCESS");
                    CurrentState = State.Captured;
                }
                return;
            }

            if (elapsed >= MaxRunSeconds)
            {
                // capture whatever the screen shows so the failure mode is visible, not blind
                ScreenCapture.CaptureScreenshot(OutputPathWin);
                string st = GameFlowManager.Instance != null ? GameFlowManager.Instance.state.ToString() : "?";
                Debug.LogWarning($"[GameplayCapture] run never reached Win within the cap (state={st}) — timeout shot taken");
                CurrentState = State.Captured;
            }
            return;
        }

        if (CurrentState == State.Captured)
        {
            // CaptureScreenshot writes asynchronously (end of frame); give it one extra
            // frame before leaving Play Mode so the files are actually flushed to disk.
            if (!File.Exists(OutputPathEnd)) return;
            if (_perfProbe != null)
            {
                _perfProbe.Dump("/Users/a/blue-vs-orange-runner/base/Builds/perf-report.txt", "run");
                _perfProbe = null;
            }
            EditorApplication.isPlaying = false;
            PlayerPrefs.SetInt("skin_equipped", 0);
            PlayerPrefs.Save();
            StartShotDone = false;
            Shot1Done = false;
            Shot2Done = false;
            Shot3Done = false;
            LossShotDone = false;
            BattleShotDone = false;
            StoreShotDone = false;
            BattleTime = 0;
            WinTime = 0;
            WinStep = 0;
            CurrentState = State.Idle;
        }
    }

}
