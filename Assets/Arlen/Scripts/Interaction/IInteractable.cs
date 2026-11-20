using UnityEngine;

namespace Arlen.Interaction
{
    /// <summary>
    /// Anything the player can use with the Interact button: doors, NPCs, chests, notes, Waybells.
    /// Found via colliders on the object or its children (trigger colliders work too).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Verb shown in the prompt, e.g. "Open", "Talk", "Examine".</summary>
        string InteractionPrompt { get; }

        /// <summary>World position used for distance/facing checks and prompt placement.</summary>
        Vector3 InteractionPoint { get; }

        bool CanInteract(PlayerInteractor interactor);

        void Interact(PlayerInteractor interactor);
    }

    /// <summary>Pure scoring used by <see cref="PlayerInteractor"/>; static so it can be unit-tested.</summary>
    public static class InteractionScoring
    {
        /// <summary>
        /// Lower is better. Returns float.MaxValue if out of range or outside the facing cone.
        /// Facing matters more than raw distance so the player picks what they're looking at.
        /// </summary>
        public static float Score(Vector3 origin, Vector3 forward, Vector3 point, float maxDistance, float maxAngle)
        {
            Vector3 to = point - origin;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > maxDistance) return float.MaxValue;

            forward.y = 0f;
            // Very close objects count as "in front" so you can use something you're standing on top of.
            float angle = distance < 0.4f || forward.sqrMagnitude < 0.0001f ? 0f : Vector3.Angle(forward, to);
            if (angle > maxAngle) return float.MaxValue;

            return distance + angle * 0.03f;
        }
    }
}
