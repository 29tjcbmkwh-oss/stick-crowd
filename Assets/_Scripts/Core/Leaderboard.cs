using System.Collections.Generic;
using UnityEngine;
#if GPGS_ENABLED
using GooglePlayGames;
#endif

/// <summary>
/// Leaderboard service. Google Play Games code is compiled only when GPGS_ENABLED is defined
/// (after the team imports the GPGS SDK AND configures the leaderboard in Play Console). Until
/// then it is a safe no-op and the UI shows the local competitive board. Never breaks the build.
///
/// TEAM ACTIVATION (Play Console side, then flip the define):
///   1. Play Console > your game > Play Games Services > set up + create a Leaderboard.
///   2. Put its ID in PlayGamesLeaderboardId below.
///   3. Import the Google Play Games Unity SDK, add scripting define GPGS_ENABLED.
/// </summary>
public static class Leaderboard
{
    public const string PlayGamesLeaderboardId = "REPLACE_WITH_PLAY_GAMES_LEADERBOARD_ID";
    private const string BestKey = "stickcrowd.best_score";

    public static bool Authenticated
    {
#if GPGS_ENABLED
        get { return PlayGamesPlatform.Instance != null && PlayGamesPlatform.Instance.IsAuthenticated(); }
#else
        get { return false; }
#endif
    }

    public static void Initialize()
    {
#if GPGS_ENABLED
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(status => Debug.Log("[Leaderboard] GPGS auth: " + status));
#endif
    }

    public static void SubmitBestScore(int score)
    {
        PlayerPrefs.SetInt(BestKey, Mathf.Max(score, PlayerPrefs.GetInt(BestKey, 0)));
        PlayerPrefs.Save();
#if GPGS_ENABLED
        if (Authenticated)
            PlayGamesPlatform.Instance.ReportScore(score, PlayGamesLeaderboardId, _ => { });
#endif
    }

    /// <summary>Opens the native Play Games leaderboard if available. Returns false so the
    /// caller falls back to the local board.</summary>
    public static bool ShowNative()
    {
#if GPGS_ENABLED
        if (Authenticated)
        {
            PlayGamesPlatform.Instance.ShowLeaderboardUI(PlayGamesLeaderboardId);
            return true;
        }
#endif
        return false;
    }

    public struct Entry { public string Name; public int Score; public bool You; }

    /// <summary>Local competitive board: deterministic rivals arranged so YOU sit mid-pack —
    /// always someone just above to chase and someone just below to hold off (retention).</summary>
    public static List<Entry> LocalBoard(int yourBest)
    {
        string[] names = { "ProSprint", "N1njaRun", "GateKing", "RushHour", "BlazeX",
                           "Vortex", "K.O.Kid", "ZoomZoom", "TitanRun", "EchoDash" };
        int[] deltas = { 380, 250, 160, 90, 40, -30, -80, -150, -240, -360 };
        int b = Mathf.Max(yourBest, 30);

        var list = new List<Entry>();
        for (int i = 0; i < names.Length; i++)
            list.Add(new Entry { Name = names[i], Score = Mathf.Max(1, b + deltas[i]) });
        list.Add(new Entry { Name = "YOU", Score = b, You = true });
        list.Sort((x, y) => y.Score.CompareTo(x.Score));
        return list;
    }
}
