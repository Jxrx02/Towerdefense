using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Bestätigungsdialog ("Wirklich beenden?"). Unterkind des Pause-Canvas, standardmäßig inaktiv.
    /// </summary>
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private Text txtMessage;
        [SerializeField] private Button btnYes;
        [SerializeField] private Button btnNo;

        private Action _onYes;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            btnYes.onClick.AddListener(() => { var cb = _onYes; Close(); cb?.Invoke(); });
            btnNo.onClick.AddListener(Close);
        }

        public void Open(string message, Action onYes)
        {
            _onYes = onYes;
            txtMessage.text = message;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            _onYes = null;
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Muss auf einem IMMER AKTIVEN Objekt liegen (z.B. am LevelManager-Objekt),
    /// weil PauseMenuUI auf dem inaktiven Pause-Canvas nicht updaten kann.
    /// - Pause-Taste (rebindbar) öffnet/schließt das Menü
    /// - Auto-Pause beim Fokusverlust
    /// - Optionale FPS-Anzeige
    /// </summary>
    public class PauseHotkey : MonoBehaviour
    {
        [SerializeField] private GameObject pauseCanvas;
        [SerializeField] private Text txtFps;

        private float _fpsTimer;
        private int _fpsFrames;

        private bool CanPause()
        {
            var lm = LevelManager.instance;
            if (lm == null) return false;
            if (lm.deathScreen != null && lm.deathScreen.activeSelf) return false;
            if (lm.endScreen != null && lm.endScreen.activeSelf) return false;
            if (lm.questionPanel != null && lm.questionPanel.activeSelf) return false;
            return true;
        }

        private void Update()
        {
            // Öffnen. Schließen übernimmt PauseMenuUI (damit Settings/Dialog zuerst zugehen).
            if (KeyBindings.Down(GameAction.Pause) && !pauseCanvas.activeSelf && CanPause())
                LevelManager.instance.Pause();

            UpdateFps();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && GameSettings.AutoPauseOnFocusLoss &&
                pauseCanvas != null && !pauseCanvas.activeSelf && CanPause())
            {
                LevelManager.instance.Pause();
            }
        }

        private void UpdateFps()
        {
            if (txtFps == null) return;

            txtFps.gameObject.SetActive(GameSettings.ShowFps);
            if (!GameSettings.ShowFps) return;

            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;

            if (_fpsTimer >= 0.5f)
            {
                txtFps.text = Mathf.RoundToInt(_fpsFrames / _fpsTimer) + " FPS";
                _fpsTimer = 0f;
                _fpsFrames = 0;
            }
        }
    }
}