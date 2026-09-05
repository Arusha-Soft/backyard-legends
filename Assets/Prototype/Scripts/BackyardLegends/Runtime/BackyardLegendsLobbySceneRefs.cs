using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsLobbySceneRefs : MonoBehaviour
    {
        [Header("Theme Surfaces")]
        public Image BackgroundImage;
        public Image SheetImage;
        public Image PreviewPanelImage;
        public Image HeroCardImage;

        [Header("Texts")]
        public Text TitleText;
        public Text SubtitleText;
        public Text FlavorText;
        public Text RuleSummaryText;
        public Text SelectionSummaryText;
        public Text AccountStatusText;
        public InputField EmailInput;
        public InputField PasswordInput;

        [Header("Buttons")]
        public Button StartMatchButton;
        public Button[] ModeButtons;
        public Button[] TargetButtons;
        public Button SignInGoogleButton;
        public Button SignInAppleButton;
        public Button EmailRegisterButton;
        public Button EmailSignInButton;
        public Button SignOutButton;
        public Text SessionAccountLabel;

        [Header("Auth Prefab")]
        public BackyardLegendsLoginAuthView LoginAuthPanelPrefab;
        public BackyardLegendsLoginAuthView LoginAuthPanelInstance;

        public RectTransform LobbySheetRect => SheetImage != null ? SheetImage.rectTransform : null;
        public RectTransform PreviewPanelRect => PreviewPanelImage != null ? PreviewPanelImage.rectTransform : null;
        public RectTransform ModeRowRect => ModeButtons != null && ModeButtons.Length > 0 && ModeButtons[0] != null
            ? ModeButtons[0].transform.parent as RectTransform
            : null;
        public RectTransform ScoreRowRect => TargetButtons != null && TargetButtons.Length > 0 && TargetButtons[0] != null
            ? TargetButtons[0].transform.parent as RectTransform
            : null;

        public void ResolveMissingReferences()
        {
            BackgroundImage = BackgroundImage != null ? BackgroundImage : FindByPath<Image>("Background");
            SheetImage = SheetImage != null ? SheetImage : FindByPath<Image>("Lobby Sheet");
            PreviewPanelImage = PreviewPanelImage != null ? PreviewPanelImage : FindByPath<Image>("Lobby Sheet/Preview Panel");
            TitleText = TitleText != null ? TitleText : FindByPath<Text>("Lobby Sheet/Title");
            SubtitleText = SubtitleText != null ? SubtitleText : FindByPath<Text>("Lobby Sheet/Subtitle");
            FlavorText = FlavorText != null ? FlavorText : FindByPath<Text>("Lobby Sheet/Preview Panel/Flavor");
            RuleSummaryText = RuleSummaryText != null ? RuleSummaryText : FindByPath<Text>("Lobby Sheet/Preview Panel/Rule Summary");
            SelectionSummaryText = SelectionSummaryText != null ? SelectionSummaryText : FindByPath<Text>("Lobby Sheet/Selection Summary");
            AccountStatusText = AccountStatusText != null ? AccountStatusText : FindByPath<Text>("Lobby Sheet/Account Status");
            EmailInput = EmailInput != null ? EmailInput : FindByPath<InputField>("Lobby Sheet/Email Panel/Email Input");
            PasswordInput = PasswordInput != null ? PasswordInput : FindByPath<InputField>("Lobby Sheet/Email Panel/Password Input");
            StartMatchButton = StartMatchButton != null ? StartMatchButton : FindByPath<Button>("Lobby Sheet/Start Match");
            SignInGoogleButton = SignInGoogleButton != null ? SignInGoogleButton : FindByPath<Button>("Lobby Sheet/Account Row/Sign In Google");
            SignInAppleButton = SignInAppleButton != null ? SignInAppleButton : FindByPath<Button>("Lobby Sheet/Account Row/Sign In Apple");
            EmailRegisterButton = EmailRegisterButton != null ? EmailRegisterButton : FindByPath<Button>("Lobby Sheet/Email Panel/Email Register");
            EmailSignInButton = EmailSignInButton != null ? EmailSignInButton : FindByPath<Button>("Lobby Sheet/Email Panel/Email Sign In");
            SignOutButton = SignOutButton != null ? SignOutButton : FindByPath<Button>("Lobby Sheet/Sign Out");
            SessionAccountLabel = SessionAccountLabel != null ? SessionAccountLabel : FindByPath<Text>("Lobby Sheet/Session Account");
            ModeButtons = ResolveButtons(
                ModeButtons,
                "Lobby Sheet/Mode Row/Classic Mode",
                "Lobby Sheet/Mode Row/Street Mode");
            TargetButtons = ResolveScoreButtons(TargetButtons);
        }

        private T FindByPath<T>(string path) where T : Component
        {
            var target = transform.Find(path);
            if (target == null && transform.root != transform)
            {
                target = transform.root.Find(path);
            }

            if (target != null && target.TryGetComponent<T>(out var component))
            {
                return component;
            }

            var leafNameIndex = path.LastIndexOf('/');
            var leafName = leafNameIndex >= 0 ? path.Substring(leafNameIndex + 1) : path;
            var candidates = transform.root.GetComponentsInChildren<T>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].name == leafName)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private Button[] ResolveButtons(Button[] existing, params string[] paths)
        {
            var resolved = new Button[paths.Length];
            for (var i = 0; i < paths.Length; i++)
            {
                if (existing != null && i < existing.Length && existing[i] != null)
                {
                    resolved[i] = existing[i];
                    continue;
                }

                resolved[i] = FindByPath<Button>(paths[i]);
            }

            return resolved;
        }

        private Button[] ResolveScoreButtons(Button[] existing)
        {
            var resolved = ResolveButtons(
                existing,
                "Lobby Sheet/Score Row/Score 100",
                "Lobby Sheet/Score Row/Score 200",
                "Lobby Sheet/Score Row/Score 500");

            if (resolved.Length > 2 && resolved[2] == null)
            {
                resolved[2] = FindByPath<Button>("Lobby Sheet/Score Row/Score 300");
            }

            return resolved;
        }
    }
}
