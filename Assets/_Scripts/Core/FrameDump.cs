using System.IO;
using UnityEngine;

namespace _Scripts.Core
{
    /// <summary>
    /// Offline gameplay video recorder (HOD 2026-07-23 item 5). Sets Time.captureFramerate so
    /// the engine simulates exact 1/30s steps regardless of wall-clock speed, then writes one
    /// JPG per rendered frame — ffmpeg assembles them into a perfectly smooth 30fps video.
    /// Chosen over ffmpeg/avfoundation screen grab: that path hangs on macOS screen-recording
    /// permission (probed 2026-07-23, blocked prompt), while this needs no permissions and
    /// records the clean Game view with no editor chrome. Never wired into any scene — only
    /// the editor-side VideoRecorder instantiates it during a record-video-request run.
    /// </summary>
    public class FrameDump : MonoBehaviour
    {
        public string outDir;
        public float startAt = 0.2f;      // game-time seconds before the first frame
        public float maxSeconds = 45f;    // hard stop so a stuck run can't fill the disk
        public int fps = 30;

        [System.NonSerialized] public bool stopRequested;
        [System.NonSerialized] public int framesWritten;

        public bool Finished { get; private set; }

        // NOT in Awake: AddComponent runs Awake before the caller can assign outDir, so any
        // outDir use here throws ArgumentNullException (bit the 21:13 recording attempt).
        private void Awake()
        {
            Time.captureFramerate = fps;
        }

        private void Start()
        {
            StartCoroutine(Recorder());
        }

        // WaitForEndOfFrame, not LateUpdate: CaptureScreenshotAsTexture reads the backbuffer,
        // which is only valid at end of frame — from LateUpdate it returns solid black
        // (1350 pure-black frames on the 21:30 attempt; the single-shot CaptureScreenshot
        // path never hit this because the file variant defers to end-of-frame internally).
        private System.Collections.IEnumerator Recorder()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (!Finished)
            {
                yield return endOfFrame;
                if (stopRequested || Time.time >= startAt + maxSeconds)
                {
                    Finish();
                    yield break;
                }
                if (Time.time < startAt) continue;

                if (framesWritten == 0) Directory.CreateDirectory(outDir);
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(outDir, $"frame_{framesWritten:D5}.jpg"),
                    ImageConversion.EncodeToJPG(tex, 92));
                Destroy(tex);
                framesWritten++;
            }
        }

        private void Finish()
        {
            Finished = true;
            Time.captureFramerate = 0;
            File.WriteAllText(Path.Combine(outDir, "done.txt"),
                $"{framesWritten} frames at {fps}fps = {framesWritten / (float)fps:F1}s");
        }

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
        }
    }
}
