using UnityEngine;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>Fades a location title in and out when the player enters a named area.</summary>
    public class AreaBanner : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Text title;
        [SerializeField] Text subtitle;
        [SerializeField, Min(0f)] float fadeIn = 0.6f;
        [SerializeField, Min(0f)] float hold = 2.2f;
        [SerializeField, Min(0f)] float fadeOut = 1f;

        float timer = -1f;

        public string CurrentTitle => title != null ? title.text : string.Empty;

        void Awake()
        {
            if (group != null) group.alpha = 0f;
        }

        public void Show(string areaName, string areaSubtitle)
        {
            if (title != null) title.text = areaName;
            if (subtitle != null)
            {
                subtitle.text = areaSubtitle;
                subtitle.gameObject.SetActive(!string.IsNullOrEmpty(areaSubtitle));
            }
            timer = 0f;
        }

        void Update()
        {
            if (timer < 0f || group == null) return;
            timer += Time.unscaledDeltaTime;
            float a;
            if (timer < fadeIn) a = timer / fadeIn;
            else if (timer < fadeIn + hold) a = 1f;
            else a = 1f - (timer - fadeIn - hold) / fadeOut;
            group.alpha = Mathf.Clamp01(a);
            if (timer > fadeIn + hold + fadeOut) timer = -1f;
        }
    }
}
