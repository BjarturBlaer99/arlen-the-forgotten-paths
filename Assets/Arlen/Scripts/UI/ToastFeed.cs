using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arlen.UI
{
    /// <summary>Short stacked notifications ("New quest", "+ Sunmoss", "Progress saved"). Unscaled time.</summary>
    public class ToastFeed : MonoBehaviour
    {
        [Tooltip("Inactive child Text cloned for each message (lives under a layout group).")]
        [SerializeField] Text template;
        [SerializeField, Min(1)] int maxVisible = 4;
        [SerializeField, Min(0.5f)] float lifetime = 3.5f;
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.6f;

        sealed class Entry
        {
            public Text Text;
            public float Age;
            public Color Color;
        }

        readonly List<Entry> active = new List<Entry>();
        readonly Stack<Text> pool = new Stack<Text>();

        public int VisibleCount => active.Count;
        public string Latest => active.Count > 0 ? active[active.Count - 1].Text.text : null;

        void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        public void Show(string message, Color? color = null)
        {
            if (template == null || string.IsNullOrEmpty(message)) return;
            if (active.Count >= maxVisible) Recycle(0);

            var text = pool.Count > 0 ? pool.Pop() : Instantiate(template, template.transform.parent);
            text.transform.SetAsLastSibling();
            text.text = message;
            var c = color ?? template.color;
            text.color = c;
            text.gameObject.SetActive(true);
            active.Add(new Entry { Text = text, Age = 0f, Color = c });
            enabled = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var e = active[i];
                e.Age += dt;
                if (e.Age >= lifetime) { Recycle(i); continue; }
                float alpha = e.Age > lifetime - fadeSeconds ? (lifetime - e.Age) / fadeSeconds : 1f;
                var c = e.Color;
                c.a *= alpha;
                e.Text.color = c;
            }
            if (active.Count == 0) enabled = false;
        }

        void Recycle(int index)
        {
            var e = active[index];
            active.RemoveAt(index);
            e.Text.gameObject.SetActive(false);
            pool.Push(e.Text);
        }
    }
}
