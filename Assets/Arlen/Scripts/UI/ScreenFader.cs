using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>Full-screen fade used for resting, and later scene transitions. Uses unscaled time.</summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] Image overlay;
        [SerializeField] Color color = new Color(0.02f, 0.02f, 0.05f, 1f);

        Coroutine routine;

        public bool IsFading => routine != null;

        void Awake() => SetAlpha(0f);

        /// <summary>Fades to black, invokes <paramref name="atBlack"/>, holds, then fades back in.</summary>
        public void FadeOutIn(float fadeSeconds, float holdSeconds, Action atBlack, Action finished = null)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(fadeSeconds, holdSeconds, atBlack, finished));
        }

        IEnumerator Run(float fade, float hold, Action atBlack, Action finished)
        {
            if (overlay != null) overlay.raycastTarget = true;
            yield return FadeTo(1f, fade);
            atBlack?.Invoke();
            float end = Time.unscaledTime + hold;
            while (Time.unscaledTime < end) yield return null;
            yield return FadeTo(0f, fade);
            if (overlay != null) overlay.raycastTarget = false;
            routine = null;
            finished?.Invoke();
        }

        IEnumerator FadeTo(float target, float seconds)
        {
            float start = overlay != null ? overlay.color.a : 0f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / seconds));
                yield return null;
            }
            SetAlpha(target);
        }

        void SetAlpha(float a)
        {
            if (overlay == null) return;
            var c = color;
            c.a = a;
            overlay.color = c;
            overlay.enabled = a > 0.001f;
        }
    }
}
