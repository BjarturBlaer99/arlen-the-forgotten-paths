using UnityEngine;

namespace Arlen.Feedback
{
    /// <summary>
    /// Additive trauma-based shake applied after the camera controller has positioned the camera
    /// (later execution order), so it never accumulates or fights the follow logic.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        [SerializeField, Min(0f)] float maxOffset = 0.25f;
        [SerializeField, Min(0f)] float maxRoll = 2.5f;
        [SerializeField, Min(0f)] float frequency = 22f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField, Min(0f)] float decay = 2.2f;
        [SerializeField] bool effectEnabled = true;

        float trauma;
        float seed;

        void Awake() => seed = Random.value * 100f;

        /// <summary>Adds trauma in 0..1; shake strength is trauma squared for a nicer falloff.</summary>
        public void Add(float amount)
        {
            if (effectEnabled) trauma = Mathf.Clamp01(trauma + amount);
        }

        void LateUpdate()
        {
            if (trauma <= 0f) return;
            float shake = trauma * trauma;
            float t = Time.unscaledTime * frequency;
            Vector3 offset = new Vector3(
                Mathf.PerlinNoise(seed, t) * 2f - 1f,
                Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f,
                0f) * (maxOffset * shake);
            float roll = (Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f) * maxRoll * shake;

            transform.position += transform.rotation * offset;
            transform.rotation *= Quaternion.Euler(0f, 0f, roll);
            trauma = Mathf.Max(0f, trauma - decay * Time.unscaledDeltaTime);
        }
    }
}
