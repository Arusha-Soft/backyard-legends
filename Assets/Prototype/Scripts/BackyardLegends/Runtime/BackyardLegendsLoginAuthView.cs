using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    /// <summary>
    /// Self-contained login / account UI. Wire from lobby or instantiate the LoginAuthPanel prefab.
    /// </summary>
    public sealed class BackyardLegendsLoginAuthView : MonoBehaviour
    {
        [Header("Status")]
        public Text AccountStatusText;
        public Text TitleText;

        [Header("Email / Password")]
        public InputField EmailInput;
        public InputField PasswordInput;
        public Button EmailRegisterButton;
        public Button EmailSignInButton;

        [Header("Providers")]
        public Button SignInGoogleButton;
        public Button SignInAppleButton;
        public Button ContinueAsGuestButton;

        public void ApplyToLobbyRefs(BackyardLegendsLobbySceneRefs refs)
        {
            if (refs == null)
            {
                return;
            }

            if (AccountStatusText != null)
            {
                refs.AccountStatusText = AccountStatusText;
            }

            if (EmailInput != null)
            {
                refs.EmailInput = EmailInput;
            }

            if (PasswordInput != null)
            {
                refs.PasswordInput = PasswordInput;
            }

            if (EmailRegisterButton != null)
            {
                refs.EmailRegisterButton = EmailRegisterButton;
            }

            if (EmailSignInButton != null)
            {
                refs.EmailSignInButton = EmailSignInButton;
            }

            if (SignInGoogleButton != null)
            {
                refs.SignInGoogleButton = SignInGoogleButton;
            }

            if (SignInAppleButton != null)
            {
                refs.SignInAppleButton = SignInAppleButton;
            }
        }
    }
}
