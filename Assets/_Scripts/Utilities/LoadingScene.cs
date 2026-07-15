using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
public class LoadingScene : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(LoadScene());
    }

    public IEnumerator LoadScene()
    {
        yield return new WaitForSeconds(3);
        SceneManager.LoadScene("Level");
    }

}
