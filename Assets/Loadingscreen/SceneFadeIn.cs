using System.Collections;
using LoadingScreen;
using UnityEngine;
using UnityEngine.UI;


public class SceneFadeIn : MonoBehaviour
{
    
    
    public float fadeDuration = 0.5f;
    public Image fadeOverlay;

    public IEnumerator Fade(bool fadein)
    {
        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.enabled = true;

        if (fadein)
        {
            fadeOverlay.CrossFadeAlpha(1, 0, true);
            fadeOverlay.CrossFadeAlpha(0, fadeDuration, true);
            yield return new WaitForSecondsRealtime(fadeDuration);
            fadeOverlay.gameObject.SetActive(false);
            fadeOverlay.enabled = false;
        }
        else
        {
            fadeOverlay.CrossFadeAlpha(0, 0, true);
            fadeOverlay.CrossFadeAlpha(1, fadeDuration, true);
        }
    }

    /*
     * Call this method to switch scenes
     */
    public void FadeToScene(int scene)
    {
        StartCoroutine(fadeToScene(scene));
    }

    public void FadeToScene(string scene)
    {
        StartCoroutine(fadeToScene(scene));
    }

    private IEnumerator fadeToScene(int scene)
    {
        yield return FadeOut();
        Time.timeScale = 1f;
        LoaderInfo.LoadScene(scene);
    }

    private IEnumerator fadeToScene(string scene)
    {
        yield return FadeOut();
        Time.timeScale = 1f;
        LoaderInfo.LoadScene(scene);
    }

    private IEnumerator FadeOut()
    {
        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.enabled = true;

        fadeOverlay.CrossFadeAlpha(0, 0, true);
        fadeOverlay.CrossFadeAlpha(1, fadeDuration, true);
        yield return new WaitForSecondsRealtime(fadeDuration);
    }
}