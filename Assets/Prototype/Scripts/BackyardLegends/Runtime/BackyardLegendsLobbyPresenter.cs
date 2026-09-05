using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Firebase;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsLobbyPresenter : MonoBehaviour
    {
        [SerializeField] private BackyardLegendsLobbySceneRefs sceneRefs;
        [SerializeField] private ThemeConfig themeOverride;
        private readonly List<Button> modeButtons = new();
        private readonly List<Button> targetButtons = new();
        private readonly Dictionary<Button, BackyardLegendsLobbyButtonFeedback> buttonFeedback = new();
        private readonly List<RectTransform> modeSelectionMarkers = new();
        private readonly HashSet<Button> transparentHitAreaButtons = new();

        private BackyardLegendsSession session;
        private ThemeConfig theme;
        private AudioSource feedbackAudioSource;
        private AudioClip hoverClip;
        private AudioClip selectClip;
        private AudioClip confirmClip;
        private Coroutine backgroundDestroyedRoutine;
        private Coroutine backgroundSharpenRoutine;
        private Component backgroundDestroyedFx;
        private Component backgroundSharpenFx;
        private Coroutine authRefreshRoutine;
        private bool authActionInFlight;
        private bool authGateCompleted;

        private const float BackgroundDestroyedRevealSeconds = 1f;
        private const float BackgroundSharpenMin = 0.001f;
        private const float BackgroundSharpenMax = 12f;
        private const float BackgroundSharpenCycleSeconds = 1f;
        private const float BackgroundSharpenMinHoldSeconds = 1f;

        private enum FeedbackCue
        {
            Hover,
            Select,
            Confirm
        }

        private void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;

            session = BackyardLegendsSession.GetOrCreateRuntimeInstance();
            theme = themeOverride != null ? themeOverride : session.Theme ?? ThemeConfig.CreateFallback();
            sceneRefs = sceneRefs != null ? sceneRefs : GetComponent<BackyardLegendsLobbySceneRefs>();
            if (sceneRefs == null)
            {
                sceneRefs = FindFirstObjectByType<BackyardLegendsLobbySceneRefs>();
            }

            if (sceneRefs == null)
            {
                Debug.LogError("Backyard Legends lobby refs are missing. Rebuild the authored lobby scene.");
                enabled = false;
                return;
            }

            sceneRefs.ResolveMissingReferences();
            EnsureAccountUi();
            sceneRefs.ResolveMissingReferences();
            CacheButtons();
            if (!HasRequiredReferences())
            {
                Debug.LogError("Backyard Legends lobby refs are incomplete. Resolve the missing authored objects before entering play mode.");
                enabled = false;
                return;
            }

            CacheModeSelectionMarkers();
            ConfigureFeedbackAudio();
            ConfigureUiCallbacks();
            ConfigureAuthCallbacks();
            ApplyTheme();
            ConfigureBackgroundEffects();
            ConfigureButtonFeedback();
            RefreshContent();
            RefreshAccountUi();
            if (authRefreshRoutine != null)
            {
                StopCoroutine(authRefreshRoutine);
            }

            authRefreshRoutine = StartCoroutine(WatchAuthStatus());
        }

        private void OnDestroy()
        {
            if (authRefreshRoutine != null)
            {
                StopCoroutine(authRefreshRoutine);
                authRefreshRoutine = null;
            }

            if (FirebaseAuthService.Instance != null)
            {
                FirebaseAuthService.Instance.StateChanged -= HandleAuthStateChanged;
            }
        }

        private void CacheButtons()
        {
            modeButtons.Clear();
            targetButtons.Clear();
            transparentHitAreaButtons.Clear();

            PrepareButtonForRuntime(sceneRefs.StartMatchButton, false);
            PrepareButtonForRuntime(sceneRefs.SignInGoogleButton, false);
            PrepareButtonForRuntime(sceneRefs.SignInAppleButton, false);
            PrepareButtonForRuntime(sceneRefs.EmailRegisterButton, false);
            PrepareButtonForRuntime(sceneRefs.EmailSignInButton, false);
            PrepareButtonForRuntime(sceneRefs.SignOutButton, false);
            CacheConfiguredButtons(sceneRefs.ModeButtons, modeButtons, true);
            CacheConfiguredButtons(sceneRefs.TargetButtons, targetButtons, false);
        }

        private bool HasRequiredReferences()
        {
            return sceneRefs.StartMatchButton != null &&
                   modeButtons.Count > 0 &&
                   targetButtons.Count > 0;
        }

        private void CacheModeSelectionMarkers()
        {
            modeSelectionMarkers.Clear();
            for (var i = 0; i < modeButtons.Count; i++)
            {
                var marker = ResolveModeSelectionMarker(modeButtons[i], i);
                modeSelectionMarkers.Add(marker != null ? marker.rectTransform : null);
            }
        }

        private void ConfigureUiCallbacks()
        {
            BindButtonFamily(sceneRefs.StartMatchButton, () =>
            {
                PlayFeedback(FeedbackCue.Confirm, 0.95f);
                KickButton(sceneRefs.StartMatchButton, 0.7f);
                session.LoadGameplayScene();
            });

            for (var i = 0; i < modeButtons.Count; i++)
            {
                var localIndex = i;
                var button = modeButtons[i];
                BindButtonFamily(button, () =>
                {
                    session.SelectMode(localIndex);
                    PlayFeedback(FeedbackCue.Select, 0.85f);
                    RefreshContent();
                    PlayModeSelectionFeedback(localIndex);
                });
            }

            for (var i = 0; i < targetButtons.Count; i++)
            {
                var localIndex = i;
                var button = targetButtons[i];
                BindButtonFamily(button, () =>
                {
                    var options = session.GetTargetOptions();
                    if (localIndex < options.Length)
                    {
                        session.SelectTarget(options[localIndex]);
                        PlayFeedback(FeedbackCue.Select, 0.75f);
                        RefreshContent();
                        PlayTargetSelectionFeedback(button);
                    }
                });
            }
        }

        private void ConfigureAuthCallbacks()
        {
            if (FirebaseAuthService.Instance != null)
            {
                FirebaseAuthService.Instance.StateChanged -= HandleAuthStateChanged;
                FirebaseAuthService.Instance.StateChanged += HandleAuthStateChanged;
            }

            BindButtonFamily(sceneRefs.SignInGoogleButton, () =>
            {
                if (authActionInFlight)
                {
                    return;
                }

                PlayFeedback(FeedbackCue.Select, 0.85f);
                StartCoroutine(RunAuthAction(() => FirebaseAuthService.GetOrCreate().LinkWithGoogleAsync()));
            });

            BindButtonFamily(sceneRefs.SignInAppleButton, () =>
            {
                if (authActionInFlight)
                {
                    return;
                }

                PlayFeedback(FeedbackCue.Select, 0.85f);
                StartCoroutine(RunAuthAction(() => FirebaseAuthService.GetOrCreate().LinkWithAppleAsync()));
            });

            BindButtonFamily(sceneRefs.EmailRegisterButton, () =>
            {
                if (authActionInFlight)
                {
                    return;
                }

                PlayFeedback(FeedbackCue.Select, 0.85f);
                var email = sceneRefs.EmailInput != null ? sceneRefs.EmailInput.text : string.Empty;
                var password = sceneRefs.PasswordInput != null ? sceneRefs.PasswordInput.text : string.Empty;
                StartCoroutine(RunAuthAction(() => FirebaseAuthService.GetOrCreate().RegisterWithEmailPasswordAsync(email, password)));
            });

            BindButtonFamily(sceneRefs.EmailSignInButton, () =>
            {
                if (authActionInFlight)
                {
                    return;
                }

                PlayFeedback(FeedbackCue.Select, 0.85f);
                var email = sceneRefs.EmailInput != null ? sceneRefs.EmailInput.text : string.Empty;
                var password = sceneRefs.PasswordInput != null ? sceneRefs.PasswordInput.text : string.Empty;
                StartCoroutine(RunAuthAction(() => FirebaseAuthService.GetOrCreate().SignInWithEmailPasswordAsync(email, password)));
            });

            BindButtonFamily(sceneRefs.SignOutButton, () =>
            {
                if (authActionInFlight)
                {
                    return;
                }

                PlayFeedback(FeedbackCue.Select, 0.85f);
                StartCoroutine(RunSignOutAction());
            });
        }

        private void HandleAuthStateChanged(AuthUserSnapshot snapshot)
        {
            if (snapshot != null && snapshot.IsSignedIn && !snapshot.IsAnonymous && string.IsNullOrEmpty(FirebaseAuthService.Instance?.LastError))
            {
                authGateCompleted = true;
            }

            RefreshAccountUi();
        }

        private IEnumerator WatchAuthStatus()
        {
            var wait = new WaitForSecondsRealtime(0.25f);
            while (enabled)
            {
                RefreshAccountUi();
                if (session != null && session.IsAuthReady)
                {
                    if (session.CurrentUser != null && session.CurrentUser.IsSignedIn && !session.CurrentUser.IsAnonymous)
                    {
                        authGateCompleted = true;
                        RefreshAccountUi();
                    }

                    yield break;
                }

                yield return wait;
            }
        }

        private IEnumerator RunAuthAction(System.Func<System.Threading.Tasks.Task> action)
        {
            authActionInFlight = true;
            RefreshAccountUi();
            var task = action();
            while (task != null && !task.IsCompleted)
            {
                yield return null;
            }

            authActionInFlight = false;
            var auth = FirebaseAuthService.Instance;
            if (auth != null
                && string.IsNullOrEmpty(auth.LastError)
                && auth.CurrentUser != null
                && auth.CurrentUser.IsSignedIn
                && !auth.CurrentUser.IsAnonymous)
            {
                authGateCompleted = true;
            }

            RefreshAccountUi();
            RefreshContent();
        }

        private IEnumerator RunSignOutAction()
        {
            authActionInFlight = true;
            RefreshAccountUi();
            var task = FirebaseAuthService.GetOrCreate().SignOutAsync();
            while (task != null && !task.IsCompleted)
            {
                yield return null;
            }

            authActionInFlight = false;
            authGateCompleted = false;
            RefreshAccountUi();
            RefreshContent();
        }

        private void EnsureAccountUi()
        {
            if (sceneRefs.SheetImage == null)
            {
                return;
            }

            EnsureSessionAccountChrome();

            if (TryMountLoginAuthPrefab())
            {
                return;
            }

            var sheet = sceneRefs.SheetImage.transform;
            if (sceneRefs.AccountStatusText == null)
            {
                sceneRefs.AccountStatusText = CreateRuntimeText(
                    "Account Status",
                    sheet,
                    "Signing in…",
                    18,
                    FontStyle.Bold,
                    theme != null ? theme.mutedText : new Color(0.75f, 0.75f, 0.78f),
                    TextAnchor.MiddleCenter,
                    new Vector2(0.10f, 0.705f),
                    new Vector2(0.90f, 0.745f));
            }

            var accountRow = sheet.Find("Account Row");
            if (accountRow == null)
            {
                var rowGo = new GameObject("Account Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                accountRow = rowGo.transform;
                accountRow.SetParent(sheet, false);
                var rowRect = rowGo.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.10f, 0.655f);
                rowRect.anchorMax = new Vector2(0.90f, 0.700f);
                rowRect.offsetMin = Vector2.zero;
                rowRect.offsetMax = Vector2.zero;
                var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 12f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            if (sceneRefs.SignInGoogleButton == null)
            {
                sceneRefs.SignInGoogleButton = CreateRuntimeButton(
                    "Sign In Google",
                    accountRow,
                    "GOOGLE",
                    theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f),
                    Vector2.zero,
                    Vector2.one);
            }

            if (sceneRefs.SignInAppleButton == null)
            {
                sceneRefs.SignInAppleButton = CreateRuntimeButton(
                    "Sign In Apple",
                    accountRow,
                    "APPLE",
                    theme != null ? theme.panelStroke : new Color(0.35f, 0.36f, 0.40f),
                    Vector2.zero,
                    Vector2.one);
            }

            var emailPanel = sheet.Find("Email Panel");
            if (emailPanel == null)
            {
                var panelGo = new GameObject("Email Panel", typeof(RectTransform));
                emailPanel = panelGo.transform;
                emailPanel.SetParent(sheet, false);
                var panelRect = panelGo.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.10f, 0.600f);
                panelRect.anchorMax = new Vector2(0.90f, 0.650f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }

            if (sceneRefs.EmailInput == null)
            {
                sceneRefs.EmailInput = CreateRuntimeInputField(
                    "Email Input",
                    emailPanel,
                    "Email",
                    InputField.ContentType.EmailAddress,
                    new Vector2(0.00f, 0.05f),
                    new Vector2(0.38f, 0.95f));
            }

            if (sceneRefs.PasswordInput == null)
            {
                sceneRefs.PasswordInput = CreateRuntimeInputField(
                    "Password Input",
                    emailPanel,
                    "Password",
                    InputField.ContentType.Password,
                    new Vector2(0.40f, 0.05f),
                    new Vector2(0.72f, 0.95f));
            }

            if (sceneRefs.EmailRegisterButton == null)
            {
                sceneRefs.EmailRegisterButton = CreateRuntimeButton(
                    "Email Register",
                    emailPanel,
                    "CREATE",
                    theme != null ? theme.green : new Color(0.25f, 0.65f, 0.35f),
                    new Vector2(0.74f, 0.05f),
                    new Vector2(0.86f, 0.95f));
            }

            if (sceneRefs.EmailSignInButton == null)
            {
                sceneRefs.EmailSignInButton = CreateRuntimeButton(
                    "Email Sign In",
                    emailPanel,
                    "SIGN IN",
                    theme != null ? theme.gold : new Color(0.83f, 0.69f, 0.22f),
                    new Vector2(0.88f, 0.05f),
                    new Vector2(1.00f, 0.95f));
            }
        }

        private bool TryMountLoginAuthPrefab()
        {
            if (sceneRefs.LoginAuthPanelInstance != null)
            {
                sceneRefs.LoginAuthPanelInstance.ApplyToLobbyRefs(sceneRefs);
                BindContinueAsGuest(sceneRefs.LoginAuthPanelInstance);
                return HasMountedAuthControls();
            }

            var prefab = sceneRefs.LoginAuthPanelPrefab;
            if (prefab == null)
            {
                prefab = Resources.Load<BackyardLegendsLoginAuthView>("BackyardLegends/LoginAuthPanel");
            }

            if (prefab == null)
            {
                var prefabGo = Resources.Load<GameObject>("BackyardLegends/LoginAuthPanel");
                if (prefabGo != null)
                {
                    prefab = prefabGo.GetComponent<BackyardLegendsLoginAuthView>();
                }
            }

            if (prefab == null || sceneRefs.SheetImage == null)
            {
                return false;
            }

            var instance = Instantiate(prefab, sceneRefs.SheetImage.transform, false);
            instance.name = "LoginAuthPanel";
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.06f, 0.58f);
                rect.anchorMax = new Vector2(0.94f, 0.88f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            sceneRefs.LoginAuthPanelInstance = instance;
            instance.ApplyToLobbyRefs(sceneRefs);
            BindContinueAsGuest(instance);
            return HasMountedAuthControls();
        }

        private bool HasMountedAuthControls()
        {
            return sceneRefs.AccountStatusText != null
                   && sceneRefs.EmailInput != null
                   && sceneRefs.PasswordInput != null
                   && sceneRefs.EmailRegisterButton != null
                   && sceneRefs.EmailSignInButton != null;
        }

        private void BindContinueAsGuest(BackyardLegendsLoginAuthView view)
        {
            if (view == null || view.ContinueAsGuestButton == null)
            {
                return;
            }

            BindButtonFamily(view.ContinueAsGuestButton, () =>
            {
                PlayFeedback(FeedbackCue.Confirm, 0.9f);
                authGateCompleted = true;
                RefreshAccountUi();
            });
        }

        private void EnsureSessionAccountChrome()
        {
            var sheet = sceneRefs.SheetImage.transform;
            if (sceneRefs.SessionAccountLabel == null)
            {
                sceneRefs.SessionAccountLabel = CreateRuntimeText(
                    "Session Account",
                    sheet,
                    string.Empty,
                    18,
                    FontStyle.Bold,
                    theme != null ? theme.mutedText : new Color(0.75f, 0.75f, 0.78f),
                    TextAnchor.MiddleLeft,
                    new Vector2(0.06f, 0.925f),
                    new Vector2(0.62f, 0.985f));
            }

            if (sceneRefs.SignOutButton == null)
            {
                sceneRefs.SignOutButton = CreateRuntimeButton(
                    "Sign Out",
                    sheet,
                    "SIGN OUT",
                    theme != null ? theme.red : new Color(0.75f, 0.28f, 0.28f),
                    new Vector2(0.66f, 0.925f),
                    new Vector2(0.94f, 0.985f));
            }
        }

        private void RefreshAccountUi()
        {
            var user = session != null ? session.CurrentUser : AuthUserSnapshot.None;
            var auth = FirebaseAuthService.Instance;
            if (auth != null && auth.CurrentUser != null && auth.CurrentUser.IsSignedIn)
            {
                user = auth.CurrentUser;
            }

            if (sceneRefs.AccountStatusText != null)
            {
                var status = session != null ? session.AuthStatusMessage : "Signing in…";
                if (authActionInFlight)
                {
                    status = "Updating account…";
                }

                if (auth != null && !string.IsNullOrEmpty(auth.LastError) && !authActionInFlight)
                {
                    status = $"{status}\n{auth.LastError}";
                }

                sceneRefs.AccountStatusText.text = status;
            }

            if (sceneRefs.SessionAccountLabel != null)
            {
                if (authGateCompleted && user != null && user.IsSignedIn)
                {
                    sceneRefs.SessionAccountLabel.text = user.StatusLabel;
                    sceneRefs.SessionAccountLabel.gameObject.SetActive(true);
                }
                else
                {
                    sceneRefs.SessionAccountLabel.text = string.Empty;
                    sceneRefs.SessionAccountLabel.gameObject.SetActive(false);
                }
            }

            SetLoginAuthPanelVisible(!authGateCompleted);

            var showGoogle = false;
            var showApple = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            showGoogle = true;
#elif UNITY_IOS && !UNITY_EDITOR
            showApple = true;
#endif
#if UNITY_EDITOR
            showGoogle = true;
            showApple = true;
#endif

            var controlsInteractable = !authActionInFlight && !authGateCompleted;
            SetAuthButtonVisible(sceneRefs.SignInGoogleButton, showGoogle && !authGateCompleted, controlsInteractable);
            SetAuthButtonVisible(sceneRefs.SignInAppleButton, showApple && !authGateCompleted, controlsInteractable);
            SetAuthButtonVisible(sceneRefs.EmailRegisterButton, !authGateCompleted, controlsInteractable);
            SetAuthButtonVisible(sceneRefs.EmailSignInButton, !authGateCompleted, controlsInteractable);
            SetAuthButtonVisible(sceneRefs.SignOutButton, authGateCompleted, !authActionInFlight && authGateCompleted);

            if (sceneRefs.EmailInput != null)
            {
                sceneRefs.EmailInput.interactable = controlsInteractable;
            }

            if (sceneRefs.PasswordInput != null)
            {
                sceneRefs.PasswordInput.interactable = controlsInteractable;
            }

#if UNITY_EDITOR
            if (sceneRefs.SignInGoogleButton != null && sceneRefs.SignInGoogleButton.gameObject.activeSelf)
            {
                sceneRefs.SignInGoogleButton.interactable = false;
            }

            if (sceneRefs.SignInAppleButton != null && sceneRefs.SignInAppleButton.gameObject.activeSelf)
            {
                sceneRefs.SignInAppleButton.interactable = false;
            }
#endif
        }

        private void SetLoginAuthPanelVisible(bool visible)
        {
            if (sceneRefs.LoginAuthPanelInstance != null)
            {
                sceneRefs.LoginAuthPanelInstance.gameObject.SetActive(visible);
            }
        }

        private static void SetAuthButtonVisible(Button button, bool visible, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.interactable = visible && interactable;
        }

        private Text CreateRuntimeText(
            string name,
            Transform parent,
            string value,
            int size,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private Button CreateRuntimeButton(
            string name,
            Transform parent,
            string label,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var text = labelGo.GetComponent<Text>();
            text.text = label;
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelRect = text.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private InputField CreateRuntimeInputField(
            string name,
            Transform parent,
            string placeholder,
            InputField.ContentType contentType,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.15f, 0.95f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderGo.transform.SetParent(go.transform, false);
            var placeholderText = placeholderGo.GetComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 16;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(1f, 1f, 1f, 0.35f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = placeholder;
            var placeholderRect = placeholderText.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10f, 4f);
            placeholderRect.offsetMax = new Vector2(-10f, -4f);

            var input = go.GetComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = placeholderText;
            input.contentType = contentType;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private void ConfigureFeedbackAudio()
        {
            feedbackAudioSource = GetComponent<AudioSource>();
            if (feedbackAudioSource == null)
            {
                feedbackAudioSource = gameObject.AddComponent<AudioSource>();
            }

            feedbackAudioSource.playOnAwake = false;
            feedbackAudioSource.loop = false;
            feedbackAudioSource.spatialBlend = 0f;
            feedbackAudioSource.volume = 0.16f;

            hoverClip = BackyardLegendsStreetAudio.LoadSfx("Ui_Hover") ?? CreateToneClip("Lobby Hover Cue", 680f, 920f, 0.03f, 0.05f);
            selectClip = BackyardLegendsStreetAudio.LoadSfx("Ui_Select") ?? CreateToneClip("Lobby Select Cue", 500f, 760f, 0.05f, 0.09f);
            confirmClip = BackyardLegendsStreetAudio.LoadSfx("Ui_Confirm") ?? CreateToneClip("Lobby Confirm Cue", 400f, 620f, 0.11f, 0.13f);
        }

        private void ApplyTheme()
        {
            EnsureFallbackImage(
                sceneRefs.BackgroundImage,
                theme.tableBackgroundSprite != null
                    ? theme.tableBackgroundSprite
                    : ThemeSpriteFactory.CreateBackgroundSprite(theme.backgroundSecondary, theme.backgroundColor));
            EnsureFallbackImage(sceneRefs.SheetImage, ResolveSheetSprite());
            EnsureFallbackImage(sceneRefs.PreviewPanelImage, ResolveSoftPanelSprite());
            EnsureFallbackImage(sceneRefs.HeroCardImage, ResolveCardBackSprite());

            EnsureFont(sceneRefs.TitleText);
            EnsureFont(sceneRefs.SubtitleText);
            EnsureFont(sceneRefs.FlavorText);
            EnsureFont(sceneRefs.RuleSummaryText);
            EnsureFont(sceneRefs.SelectionSummaryText);
            EnsureFont(sceneRefs.AccountStatusText);
            EnsureButtonFont(sceneRefs.StartMatchButton);
            EnsureButtonFont(sceneRefs.SignInGoogleButton);
            EnsureButtonFont(sceneRefs.SignInAppleButton);
            EnsureButtonFont(sceneRefs.EmailRegisterButton);
            EnsureButtonFont(sceneRefs.EmailSignInButton);
            EnsureButtonFont(sceneRefs.SignOutButton);
            EnsureFont(sceneRefs.SessionAccountLabel);
            foreach (var button in modeButtons)
            {
                EnsureButtonFont(button);
            }

            foreach (var button in targetButtons)
            {
                EnsureButtonFont(button);
            }
        }

        private void ConfigureButtonFeedback()
        {
            buttonFeedback.Clear();

            ConfigureButtonFeedback(sceneRefs.StartMatchButton, true);
            ConfigureButtonFeedback(sceneRefs.SignInGoogleButton);
            ConfigureButtonFeedback(sceneRefs.SignInAppleButton);
            ConfigureButtonFeedback(sceneRefs.EmailRegisterButton);
            ConfigureButtonFeedback(sceneRefs.EmailSignInButton);
            ConfigureButtonFeedback(sceneRefs.SignOutButton);
            foreach (var button in modeButtons)
            {
                ConfigureButtonFeedback(button);
            }

            foreach (var button in targetButtons)
            {
                ConfigureButtonFeedback(button);
            }
        }

        private void RefreshContent()
        {
            var labels = session.GetModeLabels();
            for (var i = 0; i < modeButtons.Count; i++)
            {
                var label = GetButtonLabel(modeButtons[i]);
                if (label != null)
                {
                    label.text = i < labels.Length ? labels[i].ToUpperInvariant() : $"MODE {i + 1}";
                }
            }

            var targetOptions = session.GetTargetOptions();
            for (var i = 0; i < targetButtons.Count; i++)
            {
                var button = targetButtons[i];
                button.gameObject.SetActive(i < targetOptions.Length);
                if (i >= targetOptions.Length)
                {
                    continue;
                }

                var label = GetButtonLabel(button);
                if (label != null)
                {
                    label.text = targetOptions[i].ToString();
                }
            }

            var selectedRule = session.SelectedRule;
            if (sceneRefs.RuleSummaryText != null)
            {
                sceneRefs.RuleSummaryText.text =
                    $"{selectedRule.DisplayName} mode locks the core single-player loop first.\n" +
                    $"{(selectedRule.AllowSpadesAnytime ? "Spades can lead any time." : "Spades wait until broken.")}\n" +
                    $"{(selectedRule.FollowSuitRequired ? "Follow suit stays hot." : "Off-suit plays are allowed.")}";
            }

            if (sceneRefs.SelectionSummaryText != null)
            {
                sceneRefs.SelectionSummaryText.text =
                    $"Portrait-first | Human vs 3 AI | Race to {selectedRule.TargetScore}\n" +
                    "Dark table, gold calls, green wins, red pressure.";
            }

            if (sceneRefs.FlavorText != null)
            {
                sceneRefs.FlavorText.text =
                    "Backyard energy over casino polish. Short, sharp rounds with enough swagger to sell the street-table tone.";
            }

            RefreshAccountUi();
            UpdateSelectionButtons();
        }

        private void UpdateSelectionButtons()
        {
            for (var i = 0; i < modeButtons.Count; i++)
            {
                var isSelected = i == session.SelectedModeIndex;
                SyncButtonFeedback(modeButtons[i], isSelected);
                SetTransparentHitAreaSelected(modeButtons[i], isSelected);
                SetModeSelectionMarkerState(i, isSelected);
            }

            var options = session.GetTargetOptions();
            for (var i = 0; i < targetButtons.Count; i++)
            {
                var isSelected = i < options.Length && options[i] == session.SelectedTargetScore;
                SyncButtonFeedback(targetButtons[i], isSelected);
            }
        }

        private void EnsureFont(Text label)
        {
            if (label == null)
            {
                return;
            }

            if (label.font == null)
            {
                label.font = theme.ResolveFont();
            }
        }

        private void EnsureButtonFont(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
            EnsureFont(GetButtonLabel(button));
        }

        private void ConfigureButtonFeedback(Button button, bool isConfirmButton = false)
        {
            if (button == null || transparentHitAreaButtons.Contains(button))
            {
                return;
            }

            var feedback = button.GetComponent<BackyardLegendsLobbyButtonFeedback>();
            if (feedback == null)
            {
                feedback = button.gameObject.AddComponent<BackyardLegendsLobbyButtonFeedback>();
            }

            feedback.Initialize(theme);
            feedback.SetCallbacks(
                () => PlayFeedback(FeedbackCue.Hover, 0.55f),
                null);
            buttonFeedback[button] = feedback;
        }

        private void SyncButtonFeedback(Button button, bool isSelected)
        {
            if (button == null || !buttonFeedback.TryGetValue(button, out var feedback))
            {
                return;
            }

            feedback.SetSelected(isSelected, true);
        }

        private void PlayModeSelectionFeedback(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= modeButtons.Count)
            {
                return;
            }

            KickButton(modeButtons[selectedIndex], 0.9f);
        }

        private void PlayTargetSelectionFeedback(Button selectedButton)
        {
            KickButton(selectedButton, 0.75f);
        }

        private void KickButton(Button button, float intensity)
        {
            if (button != null && buttonFeedback.TryGetValue(button, out var feedback))
            {
                feedback.Kick(intensity);
            }
        }

        private void SetModeSelectionMarkerState(int index, bool isSelected)
        {
            if (index < 0 || index >= modeSelectionMarkers.Count || modeSelectionMarkers[index] == null)
            {
                return;
            }

            modeSelectionMarkers[index].gameObject.SetActive(isSelected);
        }

        private static Text GetButtonLabel(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var label = button.transform.Find("Label");
            return label != null
                ? label.GetComponent<Text>()
                : button.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name != "Selected Checkmark");
        }

        private void CacheConfiguredButtons(Button[] source, List<Button> destination, bool transparentWhenInactive)
        {
            if (source == null)
            {
                return;
            }

            foreach (var button in source.Where(button => button != null))
            {
                PrepareButtonForRuntime(button, transparentWhenInactive);
                destination.Add(button);
            }
        }

        private void PrepareButtonForRuntime(Button button, bool transparentWhenInactive)
        {
            if (button == null)
            {
                return;
            }

            var wasInactive = !button.gameObject.activeSelf;
            button.gameObject.SetActive(true);
            button.interactable = true;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                button.targetGraphic = image;
            }

            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (transparentWhenInactive && wasInactive)
            {
                transparentHitAreaButtons.Add(button);
                MakeTransparentHitArea(button);
            }
        }

        private void BindButtonFamily(Button rootButton, UnityEngine.Events.UnityAction onClick)
        {
            if (rootButton == null)
            {
                return;
            }

            foreach (var button in GetButtonFamily(rootButton))
            {
                PrepareButtonSurface(button);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }
        }

        private IEnumerable<Button> GetButtonFamily(Button rootButton)
        {
            if (rootButton == null)
            {
                yield break;
            }

            yield return rootButton;
            var nestedButtons = rootButton.GetComponentsInChildren<Button>(true);
            foreach (var nestedButton in nestedButtons)
            {
                if (nestedButton != null && nestedButton != rootButton)
                {
                    yield return nestedButton;
                }
            }
        }

        private void PrepareButtonSurface(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(true);
            button.interactable = true;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
                if (button.targetGraphic == null)
                {
                    button.targetGraphic = image;
                }
            }

            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        private static void MakeTransparentHitArea(Button button)
        {
            button.transition = Selectable.Transition.None;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 0f;
                image.color = color;
                image.raycastTarget = true;
            }

            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        private void SetTransparentHitAreaSelected(Button button, bool isSelected)
        {
            if (button == null || !transparentHitAreaButtons.Contains(button))
            {
                return;
            }

            var alpha = isSelected ? 1f : 0f;
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = alpha;
                image.color = color;
                image.raycastTarget = true;
            }

            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        private void ConfigureBackgroundEffects()
        {
            var background = sceneRefs.BackgroundImage != null ? sceneRefs.BackgroundImage.gameObject : null;
            if (background == null)
            {
                return;
            }

            var sharpenBackground = FindSceneObject("Background2") ?? background;
            backgroundDestroyedFx = FindComponentByTypeName(background, "_2dxFX_DestroyedFX");
            backgroundSharpenFx = FindComponentByTypeName(sharpenBackground, "_2dxFX_Sharpen");

            if (backgroundDestroyedRoutine != null)
            {
                StopCoroutine(backgroundDestroyedRoutine);
            }

            if (backgroundSharpenRoutine != null)
            {
                StopCoroutine(backgroundSharpenRoutine);
            }

            if (backgroundSharpenFx != null)
            {
                sharpenBackground.SetActive(true);
                SetFxBool(backgroundSharpenFx, "ActiveChange", true);
                SetFxBool(backgroundSharpenFx, "ActiveUpdate", true);
                SetFxFloat(backgroundSharpenFx, "_Alpha", 1f);
                SetFxFloat(backgroundSharpenFx, "Sharpen", BackgroundSharpenMin);
                SetFxEnabled(backgroundSharpenFx, false);
            }

            if (backgroundDestroyedFx != null)
            {
                SetFxBool(backgroundDestroyedFx, "ActiveChange", true);
                SetFxBool(backgroundDestroyedFx, "ActiveUpdate", true);
                SetFxFloat(backgroundDestroyedFx, "_Alpha", 1f);
                SetFxFloat(backgroundDestroyedFx, "Destroyed", 1f);
                SetFxEnabled(backgroundDestroyedFx, true);
                CallFxUpdate(backgroundDestroyedFx);
                backgroundDestroyedRoutine = StartCoroutine(AnimateBackgroundDestroyed());
            }

            if (backgroundSharpenFx != null)
            {
                backgroundSharpenRoutine = StartCoroutine(AnimateBackgroundSharpen(backgroundDestroyedFx != null ? BackgroundDestroyedRevealSeconds : 0f));
            }
        }

        private IEnumerator AnimateBackgroundDestroyed()
        {
            var elapsed = 0f;
            while (elapsed < BackgroundDestroyedRevealSeconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / BackgroundDestroyedRevealSeconds);
                SetFxFloat(backgroundDestroyedFx, "Destroyed", Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t)));
                CallFxUpdate(backgroundDestroyedFx);
                yield return null;
            }

            SetFxFloat(backgroundDestroyedFx, "Destroyed", 0f);
            CallFxUpdate(backgroundDestroyedFx);
            SetFxBool(backgroundDestroyedFx, "ActiveUpdate", false);
            SetFxEnabled(backgroundDestroyedFx, false);
            backgroundDestroyedRoutine = null;
        }

        private IEnumerator AnimateBackgroundSharpen(float delaySeconds)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            SetFxEnabled(backgroundSharpenFx, true);

            while (true)
            {
                SetFxFloat(backgroundSharpenFx, "Sharpen", BackgroundSharpenMin);
                CallFxUpdate(backgroundSharpenFx);
                yield return new WaitForSeconds(BackgroundSharpenMinHoldSeconds);
                yield return AnimateBackgroundSharpenRange(BackgroundSharpenMin, BackgroundSharpenMax);
                yield return AnimateBackgroundSharpenRange(BackgroundSharpenMax, BackgroundSharpenMin);
            }
        }

        private IEnumerator AnimateBackgroundSharpenRange(float from, float to)
        {
            var elapsed = 0f;
            while (elapsed < BackgroundSharpenCycleSeconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / BackgroundSharpenCycleSeconds);
                SetFxFloat(backgroundSharpenFx, "Sharpen", Mathf.Lerp(from, to, t));
                CallFxUpdate(backgroundSharpenFx);
                yield return null;
            }

            SetFxFloat(backgroundSharpenFx, "Sharpen", to);
            CallFxUpdate(backgroundSharpenFx);
        }

        private static Component FindComponentByTypeName(GameObject target, string typeName)
        {
            return target == null
                ? null
                : target.GetComponents<Component>()
                    .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
        }

        private GameObject FindSceneObject(string objectName)
        {
            return sceneRefs.transform.root
                .GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName)
                ?.gameObject;
        }

        private static void SetFxEnabled(Component component, bool enabled)
        {
            if (component is Behaviour behaviour)
            {
                behaviour.enabled = enabled;
            }
        }

        private static void SetFxBool(Component component, string fieldName, bool value)
        {
            var field = FindFxField(component, fieldName);
            field?.SetValue(component, value);
        }

        private static void SetFxFloat(Component component, string fieldName, float value)
        {
            var field = FindFxField(component, fieldName);
            field?.SetValue(component, value);
        }

        private static void CallFxUpdate(Component component)
        {
            component?.GetType()
                .GetMethod("CallUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(component, null);
        }

        private static FieldInfo FindFxField(Component component, string fieldName)
        {
            return component?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private void PlayFeedback(FeedbackCue cue, float volumeScale = 1f)
        {
            if (feedbackAudioSource == null)
            {
                return;
            }

            var clip = cue switch
            {
                FeedbackCue.Hover => hoverClip,
                FeedbackCue.Select => selectClip,
                FeedbackCue.Confirm => confirmClip,
                _ => null
            };

            if (clip != null)
            {
                feedbackAudioSource.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1f));
            }
        }

        private static AudioClip CreateToneClip(string clipName, float frequencyA, float frequencyB, float duration, float volume)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(duration * sampleRate);
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Sin(Mathf.Clamp01(i / (float)sampleCount) * Mathf.PI);
                var main = Mathf.Sin(2f * Mathf.PI * frequencyA * t);
                var harmonic = Mathf.Sin(2f * Mathf.PI * frequencyB * t) * 0.35f;
                samples[i] = (main + harmonic) * envelope * volume;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static Image ResolveModeSelectionMarker(Button button, int index)
        {
            if (button == null)
            {
                return null;
            }

            var preferredName = index == 0 ? "Selected1" : "Selected2";
            var marker = button.transform.Find(preferredName);
            if (marker == null)
            {
                marker = button.transform.Cast<Transform>().FirstOrDefault(child => child.name.StartsWith("Selected"));
            }

            return marker != null ? marker.GetComponent<Image>() : null;
        }

        private void EnsureFallbackImage(Image image, Sprite fallback)
        {
            if (image == null || image.sprite != null || fallback == null)
            {
                return;
            }

            image.sprite = fallback;
        }

        private Sprite ResolveSheetSprite()
        {
            return theme.sheetSprite != null
                ? theme.sheetSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.panelColor, theme.panelStroke, 768, 1024, 34);
        }

        private Sprite ResolveSoftPanelSprite()
        {
            return theme.softPanelSprite != null
                ? theme.softPanelSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(new Color(0f, 0f, 0f, 0.08f), theme.panelStroke, 512, 256, 22);
        }

        private Sprite ResolveCardBackSprite()
        {
            return theme.cardBackHeroSprite != null
                ? theme.cardBackHeroSprite
                : ThemeSpriteFactory.CreateRoundedRectSprite(theme.cardBack, theme.gold, 180, 260, 24);
        }

    }
}
