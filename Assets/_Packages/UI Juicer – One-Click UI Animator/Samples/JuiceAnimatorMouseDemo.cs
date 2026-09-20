using UnityEngine;

namespace JuiceUp
{
    /// <summary>
    /// Simple demo script showing how to control UiJuiceAnimator with mouse input.
    /// Left click to play in, right click to play out.
    /// </summary>
    public class JuiceAnimatorMouseDemo : MonoBehaviour
    {
        [Tooltip("Reference to the UiJuiceAnimator component. If null, will try to find one on this GameObject.")]
        public UiJuiceAnimator juiceAnimator; 
        private void Start()
        {
            // Auto-find animator if not assigned
            if (juiceAnimator == null)
            {
                juiceAnimator = GetComponent<UiJuiceAnimator>();
            }

            if (juiceAnimator == null)
            {
                Debug.LogWarning($"[JuiceAnimatorMouseDemo] No UiJuiceAnimator found on {gameObject.name}. Please assign one in the inspector.");
            }
        }

        private void Update()
        {
            if (juiceAnimator == null)
                return;

            // Check for left mouse button (Play In)
            if (Input.GetMouseButtonDown(0))
            {
                juiceAnimator.PlayIn();
            }

            // Check for right mouse button (Play Out)
            if (Input.GetMouseButtonDown(1))
            {
                juiceAnimator.PlayOut();
            }
        } 
    }
}

