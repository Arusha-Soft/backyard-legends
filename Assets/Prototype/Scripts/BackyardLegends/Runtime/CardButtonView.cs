using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class CardButtonView : MonoBehaviour
    {
        public Image Panel;
        public Image FaceImage;
        public Button Button;
        public Text RankText;
        public Text SuitText;
        public CanvasGroup CanvasGroup;

        public RectTransform Root => (RectTransform)transform;
    }
}
