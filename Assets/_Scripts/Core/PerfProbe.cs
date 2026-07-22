using System.Text;
using UnityEngine;

namespace _Scripts.Core
{
    /// <summary>
    /// Lightweight in-play performance recorder (HOD item 3): frame-time distribution and
    /// GC collection counts over a run, written to Builds/perf-report.txt. Exists because
    /// the Profiler window can't be driven headlessly — these are real measurements from
    /// the running game rather than structural arguments, with the honest caveat that they
    /// are coarser than Profiler captures (no per-frame GC.Alloc bytes, which needs the
    /// Profiler backend).
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        public static PerfProbe Begin()
        {
            var go = new GameObject("PerfProbe");
            DontDestroyOnLoad(go);
            return go.AddComponent<PerfProbe>();
        }

        private readonly System.Collections.Generic.List<float> _frames =
            new System.Collections.Generic.List<float>(8192);
        private int _gc0Start, _gc1Start, _gc2Start;
        private long _memStart;

        private void Start()
        {
            _gc0Start = System.GC.CollectionCount(0);
            _gc1Start = System.GC.CollectionCount(1);
            _gc2Start = System.GC.CollectionCount(2);
            _memStart = System.GC.GetTotalMemory(false);
        }

        private void Update()
        {
            _frames.Add(Time.unscaledDeltaTime);
        }

        public void Dump(string path, string label)
        {
            if (_frames.Count < 10) return;
            _frames.Sort();
            float avg = 0f; foreach (var f in _frames) avg += f;
            avg /= _frames.Count;
            float p50 = _frames[_frames.Count / 2];
            float p95 = _frames[(int)(_frames.Count * 0.95f)];
            float p99 = _frames[(int)(_frames.Count * 0.99f)];
            var sb = new StringBuilder();
            sb.AppendLine($"[{label}] {System.DateTime.Now:HH:mm:ss} frames={_frames.Count}");
            sb.AppendLine($"  frame ms: avg={avg * 1000f:F2} p50={p50 * 1000f:F2} p95={p95 * 1000f:F2} p99={p99 * 1000f:F2} (avg fps {1f / avg:F1})");
            sb.AppendLine($"  GC collections during run: gen0={System.GC.CollectionCount(0) - _gc0Start} gen1={System.GC.CollectionCount(1) - _gc1Start} gen2={System.GC.CollectionCount(2) - _gc2Start}");
            sb.AppendLine($"  managed heap: start={_memStart / 1024 / 1024}MB end={System.GC.GetTotalMemory(false) / 1024 / 1024}MB");
            System.IO.File.AppendAllText(path, sb.ToString());
        }
    }
}
