using UnityEngine;

namespace Arlen.Interaction
{
    /// <summary>
    /// Bed or bedroll: fades out, restores health and stamina, fades back in.
    /// Saving progress hooks in here once the save system exists (Milestone 4).
    /// </summary>
    public class RestSpot : MonoBehaviour, IInteractable
    {
        [SerializeField] string verb = "Rest";
        [SerializeField, Min(0f)] float fadeSeconds = 0.6f;
        [SerializeField, Min(0f)] float holdSeconds = 0.8f;
        [TextArea(2, 4)] [SerializeField] string[] afterRestLines = { "You rest for a while. Body and mind feel renewed." };

        bool resting;

        public string InteractionPrompt => verb;
        public Vector3 InteractionPoint => transform.position + Vector3.up * 0.5f;

        public bool CanInteract(PlayerInteractor interactor) => !resting;

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor == null) return;
            resting = true;
            var input = interactor.GetComponent<Player.PlayerInputReader>();
            if (input != null) input.PushBlock();

            void Restore()
            {
                if (interactor.Health != null) interactor.Health.Heal(interactor.Health.Max);
                if (interactor.Stamina != null) interactor.Stamina.Refill();
            }

            void Done()
            {
                resting = false;
                if (input != null) input.PopBlock();
                if (interactor.Dialogue != null && afterRestLines != null && afterRestLines.Length > 0)
                    interactor.Dialogue.Play(afterRestLines);
            }

            if (interactor.ScreenFader != null) interactor.ScreenFader.FadeOutIn(fadeSeconds, holdSeconds, Restore, Done);
            else { Restore(); Done(); }
        }
    }
}
