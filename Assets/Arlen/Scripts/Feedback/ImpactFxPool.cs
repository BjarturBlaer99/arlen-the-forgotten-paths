using System.Collections.Generic;
using UnityEngine;

namespace Arlen.Feedback
{
    /// <summary>
    /// Pooled impact flashes: a small emissive mesh that pops and shrinks at the hit point.
    /// Pre-warmed at startup so combat doesn't instantiate or allocate.
    /// </summary>
    public class ImpactFxPool : MonoBehaviour
    {
        [Tooltip("Template object (disabled in the scene). Should have a MeshRenderer and no collider.")]
        [SerializeField] GameObject template;
        [SerializeField, Range(1, 64)] int poolSize = 12;
        [SerializeField, Min(0.01f)] float lifetime = 0.18f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        struct Active
        {
            public Transform transform;
            public float age;
            public float size;
        }

        readonly Queue<GameObject> free = new Queue<GameObject>();
        readonly List<Active> active = new List<Active>();
        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            if (template == null)
            {
                Debug.LogWarning($"{nameof(ImpactFxPool)} has no template; impacts won't be shown.", this);
                return;
            }
            template.SetActive(false);
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(template, transform);
                go.name = $"{template.name}_{i}";
                go.SetActive(false);
                free.Enqueue(go);
            }
        }

        public void Spawn(Vector3 position, Color color, float size)
        {
            GameObject go;
            if (free.Count > 0) go = free.Dequeue();
            else if (active.Count > 0)
            {
                go = active[0].transform.gameObject; // recycle the oldest
                active.RemoveAt(0);
            }
            else return;

            go.transform.SetPositionAndRotation(position, Random.rotation);
            go.transform.localScale = Vector3.one * size;
            if (go.TryGetComponent(out Renderer r))
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                block.SetColor(EmissionId, color * 3f);
                r.SetPropertyBlock(block);
            }
            go.SetActive(true);
            active.Add(new Active { transform = go.transform, age = 0f, size = size });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime; // keep animating during hit-stop
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];
                a.age += dt;
                float t = a.age / lifetime;
                if (t >= 1f)
                {
                    a.transform.gameObject.SetActive(false);
                    free.Enqueue(a.transform.gameObject);
                    active.RemoveAt(i);
                    continue;
                }
                // Pop out quickly, then shrink away.
                float s = t < 0.25f ? Mathf.Lerp(0.4f, 1.2f, t / 0.25f) : Mathf.Lerp(1.2f, 0f, (t - 0.25f) / 0.75f);
                a.transform.localScale = Vector3.one * (a.size * s);
                active[i] = a;
            }
        }
    }
}
