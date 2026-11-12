using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    /// <summary>
    /// Hält die Beschriftung eines Text-Elements in der aktuellen Sprache.
    /// Setzt den Text beim Aktivieren und bei jedem Sprachwechsel neu.
    ///
    ///   LocalizedText.Bind(label, () => Texts.Research.Title);
    ///
    /// Der Getter wird jedes Mal neu ausgewertet, dadurch passen auch Texte mit Zahlen (Texts.Research.Points(n)),
    /// wenn der Getter den aktuellen Wert liest.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
        private Text _text;
        private Func<string> _getter;

        public static LocalizedText Bind(Text text, Func<string> getter)
        {
            if (text == null)
                return null;

            LocalizedText localized = text.GetComponent<LocalizedText>();
            if (localized == null)
                localized = text.gameObject.AddComponent<LocalizedText>();

            localized._text = text;
            localized._getter = getter;
            localized.Refresh();
            return localized;
        }

        private void OnEnable()
        {
            Texts.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Texts.Changed -= Refresh;
        }

        public void Refresh()
        {
            if (_text == null)
                _text = GetComponent<Text>();

            if (_text != null && _getter != null)
                _text.text = _getter();
        }
    }
}