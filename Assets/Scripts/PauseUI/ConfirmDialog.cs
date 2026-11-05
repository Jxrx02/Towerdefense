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
}