using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class SeatPanelView : MonoBehaviour
    {
        public Image Panel;
        public Text NameText;
        public Text StatusText;
        public Text BidText;
        public Text TricksText;
        public Image BidCalloutPanel;
        public Image BidCalloutSplash;
        public Text BidCalloutText;
        public CanvasGroup BidCalloutGroup;

        public RectTransform Root => (RectTransform)transform;
    }
}
