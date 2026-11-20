using Arlen.UI;
using UnityEngine;

namespace Arlen.World
{
    /// <summary>Named region ("Haven Village", "Mosswood Forest"). Shows a title banner on entry.</summary>
    public class AreaZone : PlayerZone
    {
        [SerializeField] string areaName = "Unnamed";
        [SerializeField] string subtitle;
        [SerializeField] AreaBanner banner;

        public string AreaName => areaName;

        public void Configure(string title, string sub, AreaBanner targetBanner)
        {
            areaName = title;
            subtitle = sub;
            banner = targetBanner;
        }

        protected override void OnPlayerEntered()
        {
            if (banner != null) banner.Show(areaName, subtitle);
        }

        protected override void OnPlayerExited() { }
    }
}
