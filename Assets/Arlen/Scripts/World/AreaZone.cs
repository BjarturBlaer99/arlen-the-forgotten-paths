using Arlen.Core;
using Arlen.UI;
using UnityEngine;

namespace Arlen.World
{
    /// <summary>Named region ("Haven Village", "Mosswood Forest"). Shows a title banner on entry.</summary>
    public class AreaZone : PlayerZone
    {
        [Tooltip("Stable id for quests (e.g. \"mosswood\"). Defaults to the area name.")]
        [SerializeField] string areaId;
        [SerializeField] string areaName = "Unnamed";
        [SerializeField] string subtitle;
        [SerializeField] AreaBanner banner;

        public string AreaName => areaName;
        public string AreaId => string.IsNullOrEmpty(areaId) ? areaName : areaId;

        public void SetId(string id) => areaId = id;

        public void Configure(string title, string sub, AreaBanner targetBanner)
        {
            areaName = title;
            subtitle = sub;
            banner = targetBanner;
        }

        protected override void OnPlayerEntered()
        {
            if (banner != null) banner.Show(areaName, subtitle);
            WorldSignals.Raise(SignalKind.EnteredArea, AreaId);
        }

        protected override void OnPlayerExited() { }
    }
}
