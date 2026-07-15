using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelController : MonoBehaviour
{
    public int currentLevel;
    public List<GameObject> levels = new List<GameObject>();
    public void Awake()
    {
        currentLevel = PlayerPrefs.GetInt("level");
        InitMap();
    }

    private void InitMap()
    {
        Instantiate(levels[currentLevel]);
    }
    public void NextLevel()
    {
        currentLevel++;
        currentLevel = Mathf.Clamp(currentLevel, 0, levels.Count-1);
        PlayerPrefs.SetInt("level", currentLevel);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Debug.Log("LOAD NEXT LEVEL");
    }
    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
}
