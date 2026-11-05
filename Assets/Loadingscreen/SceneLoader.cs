using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoadingScreen
{
    public static class LoaderInfo
    {
        private static int _sceneToLoad = 1;
 
        public static void LoadScene(int no)
        {
            _sceneToLoad = no;
            SceneManager.LoadSceneAsync(0);
        }
 
        public static void LoadScene(string name)
        {
            _sceneToLoad = GetBuildIndexByName(name);
            SceneManager.LoadSceneAsync(0);
        }
 
        public static int GetSceneToLoad()
        {
            return _sceneToLoad;
        }
 
        private static int GetBuildIndexByName(string name)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
 
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return i;
            }
 
            Debug.LogError($"LoaderInfo: Szene '{name}' ist nicht in den Build Settings.");
            return -1;
        }
    }

    public class SceneLoader : MonoBehaviour {

        [Header("Visual References")]
        public Image progressBar;
        public Text info,progress;
        public GameObject animImage;
        public SceneFadeIn sceneFader;
    
        public Image bg;
        public Sprite[] bgImages;

        [Header("Settings")]
        public float waitAfterLoading = 0.75f;
        public float fadeDuration = 0.5f;
        public Color unfinishedColor;
        public Color finishedColor;

        private AsyncOperation _asyncLoad = null;

        private bool _loading, _finished/*, input*/;


        private void Start()
        {
            if (LoaderInfo.GetSceneToLoad() < 0)
            {
                Debug.LogError("Error: Scene buildindex is to low");
                return;
            }       
            if(bgImages.Length > 0)
                bg.sprite = bgImages[Random.Range(0, bgImages.Length)];
        


            StartCoroutine(sceneFader.Fade(true));

            StartLoadingScene();
        }
    
    

    
        private void StartLoadingScene()
        {
            info.text = "Ladevorgang";
            progressBar.color = unfinishedColor;
            progressBar.fillAmount = 0f;

            StartCoroutine(LoadAsync());
            _loading = true;
        }

        void Update()
        {
            animImage.transform.Rotate(new Vector3(0, 0, 1), 360 * Time.deltaTime);

            if (_asyncLoad != null)
            {
                if (_asyncLoad.progress >= 0.9f && _loading)
                {
                    _loading = false;
                    Invoke(nameof(ShowCompletion), 0.75f);
                }
                progress.text = !_finished
                    ? (_asyncLoad.progress * 100).ToString()
                    : "100";
                progressBar.fillAmount = !_finished
                    ? Mathf.Lerp(progressBar.fillAmount, _asyncLoad.progress, 0.25f)
                    : Mathf.Lerp(progressBar.fillAmount, 1, 0.25f);
            }
        }

        private IEnumerator LoadAsync()
        {
            _asyncLoad = SceneManager.LoadSceneAsync(LoaderInfo.GetSceneToLoad());
            _asyncLoad!.allowSceneActivation = false;
            yield return _asyncLoad;
        }

        private void ShowCompletion()
        {
            _finished = true;
            info.text = "Initialisieren";
            progress.text = "100";
            progressBar.color = finishedColor;
            Invoke(nameof(EndScene), waitAfterLoading);
        }



        private void EndScene()
        {
            StartCoroutine(sceneFader.Fade(false));
            Invoke(nameof(SwitchScene), fadeDuration);

        }

        private void SwitchScene()
        {
            _asyncLoad.allowSceneActivation = true;
        }

    }
}