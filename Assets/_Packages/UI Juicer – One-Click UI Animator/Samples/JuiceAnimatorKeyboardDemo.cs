using UnityEngine;

namespace JuiceUp
{
    /// <summary>
    /// Simple demo script showing how to control UiJuiceAnimator with keyboard input.
    /// Press SPACE to play in, ESCAPE to play out.
    /// </summary>
    public class JuiceAnimatorKeyboardDemo : MonoBehaviour
    {
        [Tooltip("Reference to the UiJuiceAnimator component. If null, will try to find one on this GameObject.")]
        public UiJuiceAnimator juiceAnimator;

        [Tooltip("Key to press for Play In animation.")]
        public KeyCode playInKey = KeyCode.Space;
        
        [Tooltip("Key to press for Play Out animation.")]
        public KeyCode playOutKey = KeyCode.Escape; 
          
        private void Start()
        {
            // Auto-find animator if not assigned
            if (juiceAnimator == null)
            {
                juiceAnimator = GetComponent<UiJuiceAnimator>();
            }

            if (juiceAnimator == null)
            {
                Debug.LogWarning($"[JuiceAnimatorKeyboardDemo] No UiJuiceAnimator found on {gameObject.name}. Please assign one in the inspector.");
            }
        }

        private void Update()
        {
            if (juiceAnimator == null)
                return;

            // Check for Play In key
            if (Input.GetKeyDown(playInKey))
            {
                juiceAnimator.PlayIn();
            }

            // Check for Play Out key
            if (Input.GetKeyDown(playOutKey))
            {
                juiceAnimator.PlayOut();
            } 
        }
    }
}

