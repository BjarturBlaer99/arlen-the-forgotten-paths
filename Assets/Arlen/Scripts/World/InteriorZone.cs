using UnityEngine;
using UnityEngine.Rendering;

namespace Arlen.World
{
    /// <summary>
    /// Hides a building's roof (and optionally upper walls) while the player is inside, so the orbit
    /// camera can see the interior. Hidden renderers keep casting shadows, so the room stays shaded.
    /// </summary>
    public class InteriorZone : PlayerZone
    {
        [SerializeField] Renderer[] hideWhenInside;

        public void SetRenderers(Renderer[] renderers) => hideWhenInside = renderers;

        protected override void OnPlayerEntered() => SetHidden(true);
        protected override void OnPlayerExited() => SetHidden(false);

        void SetHidden(bool hidden)
        {
            if (hideWhenInside == null) return;
            foreach (var r in hideWhenInside)
                if (r != null) r.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        void OnDisable() => SetHidden(false);
    }
}
