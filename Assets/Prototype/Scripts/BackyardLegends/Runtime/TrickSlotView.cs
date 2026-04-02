using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class TrickSlotView : MonoBehaviour
    {
        public Image Panel;
        public Text RankText;
        public Text SuitText;

        public RectTransform Root => (RectTransform)transform;
    }
}
