

using System;
using _Scripts.Controllers;
using _Scripts.Utilities;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class ScoreManager : Singleton<ScoreManager>
{
    private const string CoinsKey = "stickcrowd.coins";
    private const string BestScoreKey = "stickcrowd.best_score";
    private const string BestLevelKey = "stickcrowd.best_level";
    private bool _runRewardGranted;
    private bool _runRewardDoubled;

    public static event Action<int> CoinsChanged;
    public static event Action<int> BestScoreChanged;
    public int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
    public int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);
    public int BestLevel => PlayerPrefs.GetInt(BestLevelKey, 1);
    public int LastRunReward { get; private set; }
    public bool CanDoubleLastReward => _runRewardGranted && !_runRewardDoubled;

    // LEVEL END PARAMETERS
    public Transform point1Location;
    public Transform point100Location;
    public TMP_Text upperScoreText;
    public TextMeshProUGUI endGameScoreText;

    private float CalculateLevelEndBonus(GameObject boss)
    {
        if (boss == null || point1Location == null || point100Location == null) return 0;
        float zDiff = point100Location.transform.position.z - point1Location.transform.position.z;
        if (Mathf.Approximately(zDiff, 0)) return 0;
        float progress = Mathf.InverseLerp(point1Location.position.z, point100Location.position.z,
            boss.transform.position.z);
        return Mathf.FloorToInt(progress * Constants.MAX_SCORE);
    }

    public void EndGamePopupScore(GameObject boss)
    {
        int corridorScore = CorridorController.Instance != null ? CorridorController.Instance.score : 0;
        int total = Mathf.Max(1, corridorScore + Mathf.RoundToInt(CalculateLevelEndBonus(boss)));
        // "EARNED +N" phrasing per the Count Master reference win screen (HOD dispatch)
        if (endGameScoreText != null) endGameScoreText.text = $"EARNED +{total}";
        GrantRunReward(total);
    }

    public void EnsureRunReward()
    {
        if (_runRewardGranted) return;
        int total = CorridorController.Instance != null ? Mathf.Max(1, CorridorController.Instance.score) : 1;
        GrantRunReward(total);
    }

    private void GrantRunReward(int amount)
    {
        if (_runRewardGranted) return;
        _runRewardGranted = true;
        LastRunReward = Mathf.Max(1, amount);
        AddCoins(LastRunReward);
        UpdateRecords(LastRunReward);
    }

    // Best score/level records, and submit to the leaderboard (Google Play Games when the
    // team has activated it; otherwise the local competitive board).
    private void UpdateRecords(int runScore)
    {
        int level = PlayerPrefs.GetInt("level", 0) + 1;
        if (level > BestLevel) PlayerPrefs.SetInt(BestLevelKey, level);

        if (runScore > BestScore)
        {
            PlayerPrefs.SetInt(BestScoreKey, runScore);
            PlayerPrefs.Save();
            BestScoreChanged?.Invoke(runScore);
        }
        Leaderboard.SubmitBestScore(Mathf.Max(runScore, BestScore));
    }

    public bool DoubleLastReward()
    {
        if (!CanDoubleLastReward) return false;
        _runRewardDoubled = true;
        AddCoins(LastRunReward);
        return true;
    }

    private void AddCoins(int amount)
    {
        int newBalance = Mathf.Max(0, Coins + Mathf.Max(0, amount));
        PlayerPrefs.SetInt(CoinsKey, newBalance);
        PlayerPrefs.Save();
        CoinsChanged?.Invoke(newBalance);
    }
}
