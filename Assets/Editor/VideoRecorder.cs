// Marker-driven 30-second continuous gameplay recording (HOD 2026-07-23 item 5), same
// automation family as GameplayCapture. Marker: record-video-request (project root).
// Enters Play Mode on Level.unity, attaches a FrameDump (Time.captureFramerate + JPG per
// frame), and drives a full run the same way GameplayCapture does — force the run start
// after the splash, auto-tap the boss battle, click through the reward reveal — but with the
// schedule in GAME time (Time.time), because captureFramerate decouples game time from wall
// clock (a realtime schedule would fire mid-splash or skip the battle entirely).
// Frames land in Builds/video-frames/; ffmpeg assembly happens outside the editor.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using _Scripts.Core;

[InitializeOnLoad]
public static class VideoRecorder
{
    private const string Marker = "/Users/a/blue-vs-orange-runner/base/record-video-request";
    private const string FramesDir = "/Users/a/blue-vs-orange-runner/base/Builds/video-frames";

    private enum State { Idle, RequestedPlay, Recording }

    // SessionState, not plain statics: play-mode entry does a FULL domain reload in this
    // project (EnterPlayModeOptionsEnabled=0) which wipes statics — the exact
    // stranded-in-Idle bug GameplayCapture hit on 2026-07-20.
    private const string StateKey = "VideoRecorder_State";
    private static State CurrentState
    {
        get => (State)SessionState.GetInt(StateKey, (int)State.Idle);
        set => SessionState.SetInt(StateKey, (int)value);
    }

    // In-play flags only ever touched AFTER the play-enter domain reload, so plain statics
    // are safe here (nothing recompiles mid-play in the restart-per-cycle workflow).
    private static bool _runStarted;
    private static double _lastTapGameTime;
    private static double _winGameTime;
    private static int _winStep;
    private static FrameDump _dump;

    private const double RunStartAtGameTime = 2.6; // splash pop+hold+fade ends ~2.3
    private const double MaxGameSeconds = 46.0;

    static VideoRecorder()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (CurrentState == State.Idle)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(Marker)) return;
            File.Delete(Marker);
            if (Directory.Exists(FramesDir)) Directory.Delete(FramesDir, true);
            Debug.Log("[VideoRecorder] marker found — entering Play Mode to record gameplay video frames");
            EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);
            CurrentState = State.RequestedPlay;
            EditorApplication.isPlaying = true;
            return;
        }

        if (CurrentState == State.RequestedPlay)
        {
            if (!EditorApplication.isPlaying) return; // still spinning up / reloading domain
            var go = new GameObject("FrameDump");
            _dump = go.AddComponent<FrameDump>();
            _dump.outDir = FramesDir;
            _dump.startAt = 0.2f; // catch the splash pop as the opening beat of the video
            _runStarted = false;
            _lastTapGameTime = 0;
            _winGameTime = 0;
            _winStep = 0;
            CurrentState = State.Recording;
            return;
        }

        if (CurrentState == State.Recording)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[VideoRecorder] FAILED: exited Play Mode before recording finished");
                CurrentState = State.Idle;
                return;
            }
            if (_dump == null) _dump = Object.FindObjectOfType<FrameDump>();
            if (_dump == null) return;

            double t = Time.timeAsDouble;

            if (!_runStarted && t >= RunStartAtGameTime)
            {
                if (GameFlowManager.Instance != null)
                    GameFlowManager.Instance.UpdateGameState(GameState.Game);
                _runStarted = true;
            }

            // Auto-tap the tug-of-war (cadence in game time — the battle drains per frame in
            // game time too, so the balance matches a real player's ~3 taps/sec).
            if (GameFlowManager.Instance != null && GameFlowManager.Instance.state == GameState.Battle
                && t - _lastTapGameTime > 0.35)
            {
                var battle = Object.FindObjectOfType<_Scripts.Controllers.BattleController>();
                if (battle != null && battle.BattleRunning) battle.Decrease();
                _lastTapGameTime = t;
            }

            if (GameFlowManager.Instance != null && GameFlowManager.Instance.state == GameState.Win)
            {
                if (_winGameTime <= 0) _winGameTime = t;
                double sinceWin = t - _winGameTime;
                if (_winStep == 0 && sinceWin >= 1.0) { ClickButtonWithLabel("?"); _winStep = 1; }
                else if (_winStep == 1 && sinceWin >= 2.8) { ClickButtonWithLabel("CONTINUE"); _winStep = 2; }
                else if (_winStep == 2 && sinceWin >= 4.2) _dump.stopRequested = true;
            }

            if (t >= MaxGameSeconds) _dump.stopRequested = true;

            if (_dump.Finished)
            {
                Debug.Log($"[VideoRecorder] SUCCESS — {_dump.framesWritten} frames written to {FramesDir}");
                EditorApplication.isPlaying = false;
                CurrentState = State.Idle;
            }
        }
    }

    private static void ClickButtonWithLabel(string label)
    {
        foreach (var b in Object.FindObjectsOfType<UnityEngine.UI.Button>())
        {
            var t = b.GetComponentInChildren<TMPro.TMP_Text>();
            if (t != null && t.text == label && b.interactable) { b.onClick.Invoke(); return; }
        }
    }
}
