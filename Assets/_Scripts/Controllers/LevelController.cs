using System.Collections;
using System.Collections.Generic;
using _Scripts.Controllers;
using _Scripts.Models;
using _Scripts.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelController : MonoBehaviour
{
    public int currentLevel;
    public List<GameObject> levels = new List<GameObject>();
    public void Awake()
    {
        currentLevel = Mathf.Max(0, PlayerPrefs.GetInt("level", 0));
        InitMap();
    }

    private void InitMap()
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("[LevelController] No level prefabs are configured.");
            return;
        }

        int prefabIndex = currentLevel % levels.Count;
        GameObject levelRoot = Instantiate(levels[prefabIndex]);
        ApplyDifficultyVariation(levelRoot, currentLevel % 10);
    }

    // The source project only contains one authored level. These ten deterministic variations
    // keep progression moving while reusing its proven layout, with a gradual difficulty ramp.
    private static void ApplyDifficultyVariation(GameObject levelRoot, int variation)
    {
        int additiveBoost = variation % 3;
        int hazardBoost = variation / 3;

        foreach (Corridor corridor in levelRoot.GetComponentsInChildren<Corridor>(true))
        {
            switch (corridor.GetCorridorType())
            {
                case Constants.CorridorTypes.Increase:
                    corridor.increaseAmount = Mathf.Max(1, corridor.increaseAmount + additiveBoost);
                    break;
                case Constants.CorridorTypes.Decrease:
                    corridor.decreaseAmount = Mathf.Max(1, corridor.decreaseAmount + hazardBoost);
                    break;
                case Constants.CorridorTypes.Multiply:
                    corridor.multiplyAmount = Mathf.Max(2, corridor.multiplyAmount);
                    break;
                case Constants.CorridorTypes.Divide:
                    corridor.divideAmount = Mathf.Max(2, corridor.divideAmount);
                    break;
            }

            TMP_Text label = corridor.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = GetGateLabel(corridor);
        }

        foreach (MiniBattleController battle in levelRoot.GetComponentsInChildren<MiniBattleController>(true))
            battle.corridorEnemyCount = Mathf.Max(1, battle.corridorEnemyCount + hazardBoost * 2);

        foreach (Obstacle obstacle in levelRoot.GetComponentsInChildren<Obstacle>(true))
            obstacle.decreaseAmount = Mathf.Max(1, obstacle.decreaseAmount + hazardBoost);
    }

    private static string GetGateLabel(Corridor corridor)
    {
        switch (corridor.GetCorridorType())
        {
            case Constants.CorridorTypes.Increase: return "+" + corridor.increaseAmount;
            case Constants.CorridorTypes.Decrease: return "-" + corridor.decreaseAmount;
            case Constants.CorridorTypes.Multiply: return "x" + corridor.multiplyAmount;
            case Constants.CorridorTypes.Divide: return "÷" + corridor.divideAmount;
            default: return string.Empty;
        }
    }
    public void NextLevel()
    {
        currentLevel++;
        PlayerPrefs.SetInt("level", currentLevel);
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
}
