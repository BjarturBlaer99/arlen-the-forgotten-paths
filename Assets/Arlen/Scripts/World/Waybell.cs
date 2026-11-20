using System.Collections;
using Arlen.Core;
using Arlen.Interaction;
using Arlen.Player;
using Arlen.Saving;
using UnityEngine;

namespace Arlen.World
{
    /// <summary>
    /// An ancient Waybell. Ringing a silent one plays a short awakening (the bell swings, its runes light up,
    /// a ring of light rolls outward and distant resonators flare), then narration, then raises an
    /// <see cref="SignalKind.Activated"/> signal. Awake bells stay lit across saves.
    /// Visual references are optional so the logic can be tested without art.
    /// </summary>
    public class Waybell : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] string waybellId = "waybell";

        [Header("Presentation (optional)")]
        [SerializeField] Transform bell;
        [Tooltip("Renderers whose emission lights up when the bell wakes (runes, bell).")]
        [SerializeField] Renderer[] glowRenderers;
        [SerializeField] Color awakeEmission = new Color(0.35f, 1.2f, 1.1f);
        [Tooltip("Parent of the light motes that roll outward as a ring.")]
        [SerializeField] Transform ring;
        [SerializeField, Min(1f)] float ringRadius = 24f;
        [Tooltip("Far-off things that answer the bell (e.g. the shimmer where the road vanished).")]
        [SerializeField] Renderer[] resonators;
        [SerializeField] Color resonatorEmission = new Color(0.3f, 1.4f, 1.2f);
        [SerializeField, Min(0.1f)] float chimeSeconds = 2.4f;

        [Header("Text")]
        [TextArea(2, 4)] [SerializeField] string[] awakeningLines = { "The Waybell rings." };
        [TextArea(2, 4)] [SerializeField] string[] listenLines = { "The Waybell hums softly, warm to the touch." };

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock block;
        bool ringing;

        public string WaybellId => waybellId;
        public bool IsAwake { get; private set; }
        public bool IsRinging => ringing;

        public string InteractionPrompt => IsAwake ? "Listen" : "Ring";
        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.2f;

        public void Configure(string id, Transform bellTransform, Renderer[] glow, Transform ringRoot, Renderer[] answering)
        {
            waybellId = id;
            bell = bellTransform;
            glowRenderers = glow;
            ring = ringRoot;
            resonators = answering;
        }

        public void SetText(string[] awakening, string[] listen)
        {
            awakeningLines = awakening;
            listenLines = listen;
        }

        void Awake()
        {
            if (ring != null) ring.gameObject.SetActive(false);
        }

        public bool CanInteract(PlayerInteractor interactor) => !ringing;

        public void Interact(PlayerInteractor interactor)
        {
            if (ringing || interactor == null) return;
            if (IsAwake)
            {
                if (interactor.Dialogue != null) interactor.Dialogue.Play(listenLines);
                return;
            }
            StartCoroutine(Awaken(interactor));
        }

        IEnumerator Awaken(PlayerInteractor interactor)
        {
            ringing = true;
            var input = interactor.GetComponent<PlayerInputReader>();
            if (input != null) input.PushBlock();

            Quaternion rest = bell != null ? bell.localRotation : Quaternion.identity;
            if (ring != null) ring.gameObject.SetActive(true);
            for (float t = 0f; t < chimeSeconds; t += Time.deltaTime)
            {
                float k = t / chimeSeconds;
                if (bell != null) bell.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 9f) * 22f * (1f - k), 0f, 0f);
                SetEmission(glowRenderers, awakeEmission * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 2f)));
                SetEmission(resonators, resonatorEmission * Mathf.Sin(k * Mathf.PI) * 2f);
                LayoutRing(Mathf.Lerp(0.5f, ringRadius, k * k), 1f - k);
                yield return null;
            }
            if (bell != null) bell.localRotation = rest;
            if (ring != null) ring.gameObject.SetActive(false);
            SetAwakeVisuals();

            IsAwake = true;
            if (input != null) input.PopBlock();
            ringing = false;

            if (interactor.Dialogue != null && interactor.Dialogue.Play(awakeningLines, completed: RaiseSignal)) yield break;
            RaiseSignal();
        }

        void RaiseSignal() => WorldSignals.Raise(SignalKind.Activated, waybellId);

        void LayoutRing(float radius, float scale)
        {
            if (ring == null) return;
            int n = ring.childCount;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                var mote = ring.GetChild(i);
                mote.localPosition = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                mote.localScale = Vector3.one * (0.35f * Mathf.Max(0.05f, scale));
            }
        }

        void SetAwakeVisuals()
        {
            SetEmission(glowRenderers, awakeEmission);
            SetEmission(resonators, resonatorEmission);
        }

        void SetEmission(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionId, color);
                r.SetPropertyBlock(block);
            }
        }

        public void CaptureState(SaveData data)
        {
            if (IsAwake && !data.activatedWaybells.Contains(waybellId)) data.activatedWaybells.Add(waybellId);
        }

        public void RestoreState(SaveData data, SaveContext context)
        {
            if (!data.activatedWaybells.Contains(waybellId)) return;
            IsAwake = true;
            SetAwakeVisuals();
        }
    }
}
