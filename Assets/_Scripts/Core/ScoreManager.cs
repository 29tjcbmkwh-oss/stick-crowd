

using System;
using _Scripts.Controllers;
using _Scripts.Utilities;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;

public class ScoreManager : Singleton<ScoreManager>
{
    // LEVEL END PARAMETERS
    public Transform point1Location;
    public Transform point100Location;
    public TMP_Text upperScoreText;
    public TextMeshProUGUI endGameScoreText;

    private float CalculateLevelEndBonus(GameObject boss)
    {
        float zDiff = point100Location.transform.position.z - point1Location.transform.position.z;
        float pointPerLocation = zDiff / Constants.MAX_SCORE;
        float bossTransformZ = boss.transform.position.z;
        return Mathf.FloorToInt(pointPerLocation * bossTransformZ);
    }

    public void EndGamePopupScore(GameObject boss)
    {
        endGameScoreText.text = (CorridorController.Instance.score + CalculateLevelEndBonus(boss)).ToString();
    }
}
