using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    [RequireComponent(typeof(Tower))]
    public class StatDiffDisplay : MonoBehaviour
    {
        [SerializeField] private GameObject statTextPrefab;
        [SerializeField] private int poolSize = 15;

        [Header("Animation")]
        [SerializeField] private float startY = 1.75f;
        [SerializeField] private float endY = 2.15f;
        [SerializeField] private float duration = 1f;

        private readonly List<Text> pool = new List<Text>();

        private struct DiffRequest
        {
            public string statName;
            public float oldValue;
            public float newValue;
            public Color color;
        }

        private readonly Queue<DiffRequest> queue = new Queue<DiffRequest>();
        private bool isPlaying;

        private void Awake()
        {
            var statDiffCanvas = GetComponentInChildren<Canvas>();
            if (statDiffCanvas == null)
                return;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = Instantiate(statTextPrefab, statDiffCanvas.transform);
                go.SetActive(false);

                // Lokale Position relativ zum Canvas/Tower setzen (nicht Weltposition!)
                go.transform.localPosition = new Vector3(0f, startY, 0f);
                pool.Add(go.GetComponent<Text>());
            }
        }

        public void ShowDiff(string statName, float oldValue, float newValue)
        {
            ShowDiff(statName, oldValue, newValue, Color.white);
        }

        public void ShowDiff(string statName, float oldValue, float newValue, Color color)
        {
            queue.Enqueue(new DiffRequest
            {
                statName = statName,
                oldValue = oldValue,
                newValue = newValue,
                color = color
            });

            if (!isPlaying)
                StartCoroutine(ProcessQueue());
        }

        
        private IEnumerator ProcessQueue()
        {
            isPlaying = true;

            while (queue.Count > 0)
            {
                DiffRequest request = queue.Dequeue();

                Text text = GetFreeText();
                while (text == null)
                {
                    yield return null;
                    text = GetFreeText();
                }

                yield return AnimateDiff(
                    text,
                    request.statName,
                    request.oldValue,
                    request.newValue,
                    request.color);
            }

            isPlaying = false;
        }

        private Text GetFreeText()
        {
            foreach (Text txt in pool)
            {
                if (!txt.gameObject.activeInHierarchy)
                    return txt;
            }

            return null;
        }

        private IEnumerator AnimateDiff(
            Text text,
            string statName,
            float oldValue,
            float newValue,
            Color color)
        {
            float diff = newValue - oldValue;

            if (Mathf.Approximately(diff, 0f))
                yield break;

            text.gameObject.SetActive(true);
            text.color = color;
            text.text = $"{(diff >= 0 ? "+" : "")}{diff:F1} {statName}";

            float elapsed = 0f;

            Transform t = text.transform;
            Vector3 startPos = new Vector3(0f, startY, 0f);
            Vector3 endPos = new Vector3(0f, endY, 0f);

            // Sicherstellen, dass die Animation immer sauber bei startY beginnt
            t.localPosition = startPos;

            Color startColor = color;
            Color endColor = color;
            endColor.a = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float lerpT = Mathf.SmoothStep(0f, 1f, elapsed / duration);

               /* float currentValue = Mathf.Lerp(oldValue, newValue, lerpT);
                float shownDiff = currentValue - oldValue;

                text.text = $"{(shownDiff >= 0 ? "+" : "")}{shownDiff:F1} {statName}"; */

                t.localPosition = Vector3.Lerp(startPos, endPos, lerpT);
                text.color = Color.Lerp(startColor, endColor, lerpT);

                yield return null;
            }

            text.text = $"{(diff >= 0 ? "+" : "")}{diff:F1} {statName}";

            t.localPosition = startPos;
            text.color = color;
            text.gameObject.SetActive(false);
        }
    }
}