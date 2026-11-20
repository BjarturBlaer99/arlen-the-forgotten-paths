using UnityEngine;

namespace Arlen.Combat
{
    /// <summary>
    /// Briefly tints renderers when the linked HealthComponent takes damage (and a different colour
    /// when a hit is avoided by i-frames). Uses MaterialPropertyBlocks: no material instances are created.
    /// </summary>
    public class DamageFlash : MonoBehaviour
    {
        [SerializeField] HealthComponent health;
        [Tooltip("Renderers to tint. Defaults to all child renderers.")]
        [SerializeField] Renderer[] renderers;
        [SerializeField] Color hitColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] Color avoidedColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField, Min(0.01f)] float duration = 0.15f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        Color[] originalColors;
        Color flashColor;
        float timer;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<HealthComponent>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
                originalColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }
        }

        void OnEnable()
        {
            if (health == null) return;
            health.Damaged += OnDamaged;
            health.DamageAvoided += OnAvoided;
        }

        void OnDisable()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.DamageAvoided -= OnAvoided;
            timer = 0f;
            Apply(0f);
        }

        void OnDamaged(DamageInfo _) => Begin(hitColor);
        void OnAvoided(DamageInfo _) => Begin(avoidedColor);

        void Begin(Color color)
        {
            flashColor = color;
            timer = duration;
            Apply(1f);
        }

        void Update()
        {
            if (timer <= 0f) return;
            timer -= Time.deltaTime;
            Apply(Mathf.Clamp01(timer / duration));
        }

        void Apply(float strength)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, Color.Lerp(originalColors[i], flashColor, strength));
                r.SetPropertyBlock(block);
            }
        }
    }
}
