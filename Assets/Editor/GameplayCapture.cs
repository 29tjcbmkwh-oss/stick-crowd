// Marker-driven gameplay screenshot capture, same automation pattern as AutoBuildTrigger.
// Marker: capture-gameplay-request (project root). Enters Play Mode on Level.unity, waits a
// few seconds for the splash/boot to clear and the run to actually start, captures a PNG via
// Unity's own ScreenCapture API, then exits Play Mode automatically. No external screen/GUI
// automation involved — this is Unity capturing its own Game view to disk.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class GameplayCapture
{
    private const string Marker = "/Users/a/blue-vs-orange-runner/base/capture-gameplay-request";
    public const string OutputPath = "/Users/a/blue-vs-orange-runner/base/Builds/gameplay-capture.png";

    private enum State { Idle, RequestedPlay, WaitingInPlayMode, Captured }
    private static State _state = State.Idle;
    private static int _framesInPlayMode;
    private const int FramesToWaitBeforeCapture = 240; // ~4s at 60fps: past the code-driven splash

    static GameplayCapture()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (_state == State.Idle)
        {
            if (!File.Exists(Marker)) return;
            File.Delete(Marker);
            Debug.Log("[GameplayCapture] marker found — entering Play Mode on Level.unity to capture a screenshot");
            EditorSceneManager.OpenScene("Assets/Scenes/Level.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
            _state = State.RequestedPlay;
            _framesInPlayMode = 0;
            return;
        }

        if (_state == State.RequestedPlay)
        {
            if (!EditorApplication.isPlaying) return; // still spinning up
            _state = State.WaitingInPlayMode;
            return;
        }

        if (_state == State.WaitingInPlayMode)
        {
            if (!EditorApplication.isPlaying)
            {
                // Play Mode ended unexpectedly (compile error, exception) before capture.
                Debug.LogError("[GameplayCapture] FAILED: exited Play Mode before a screenshot was captured");
                _state = State.Idle;
                return;
            }

            _framesInPlayMode++;
            if (_framesInPlayMode < FramesToWaitBeforeCapture) return;

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) !);
            ScreenCapture.CaptureScreenshot(OutputPath);
            Debug.Log($"[GameplayCapture] SUCCESS — screenshot requested at {OutputPath}");
            _state = State.Captured;
            return;
        }

        if (_state == State.Captured)
        {
            // CaptureScreenshot writes asynchronously (end of frame); give it one extra
            // frame before leaving Play Mode so the file is actually flushed to disk.
            if (!File.Exists(OutputPath)) return;
            EditorApplication.isPlaying = false;
            _state = State.Idle;
        }
    }
}
