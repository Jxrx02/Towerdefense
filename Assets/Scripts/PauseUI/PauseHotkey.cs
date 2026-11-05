using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
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