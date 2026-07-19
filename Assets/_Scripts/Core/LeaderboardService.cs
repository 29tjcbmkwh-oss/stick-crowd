using System.Collections.Generic;

namespace _Scripts.Core
{
    /// <summary>
    /// The single seam between the leaderboard UI and its data source. Today this just wraps
    /// the synchronous local board (Leaderboard.LocalBoard is left untouched on purpose). When
    /// a real network/GPGS leaderboard is ready, only the body of FetchBoard needs to change to
    /// call it asynchronously and invoke callback(entries, success) when it returns — the UI
    /// already treats every fetch as a callback, so no screen changes are required.
    /// </summary>
    public static class LeaderboardService
    {
        public delegate void Callback(List<Leaderboard.Entry> entries, bool success);

        public static void FetchBoard(int yourBest, Callback callback)
        {
            List<Leaderboard.Entry> entries = Leaderboard.LocalBoard(yourBest);
            callback?.Invoke(entries, true);
        }
    }
}
