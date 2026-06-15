using BackyardLegends.Core;
using BackyardLegends.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BackyardLegends.Editor
{
    public static class BackyardLegendsSceneAuthoring
    {
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
        private const string UiPrefabFolder = "Assets/Prefabs/UI";

        private static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static Font UiFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Backyard Legends/Rebuild Authored Scenes")]
        public static void RebuildAuthoredScenes()
        {
            BackyardLegendsAssetCreator.CreateDefaultAssets();
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(UiPrefabFolder);

            var theme = AssetDatabase.LoadAssetAtPath<ThemeConfig>("Assets/Resources/BackyardLegends/Theme_Default.asset");
            var seatPrefab = CreateSeatPanelPrefab(theme);
            var trickPrefab = CreateTrickSlotPrefab(theme);
            var cardPrefab = CreateCardButtonPrefab(theme);

            BuildLobbyScene(theme);
            BuildGameplayScene(theme, seatPrefab, trickPrefab, cardPrefab);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        }

        private static void BuildLobbyScene(ThemeConfig theme)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera(scene);
            var safeRoot = CreateCanvasScaffold(scene, "Backyard Legends Lobby Canvas", out var background);

            var sessionRoot = new GameObject("Backyard Legends Session", typeof(BackyardLegendsSession));
            SceneManager.MoveGameObjectToScene(sessionRoot, scene);

            var appRoot = new GameObject("Backyard Legends Lobby", typeof(RectTransform), typeof(BackyardLegendsLobbyPresenter), typeof(BackyardLegendsLobbySceneRefs));
            appRoot.transform.SetParent(safeRoot, false);
            var appRect = appRoot.GetComponent<RectTransform>();
            Stretch(appRect);
            var refs = appRoot.GetComponent<BackyardLegendsLobbySceneRefs>();

            refs.BackgroundImage = background;
            ApplySprite(background, theme.tableBackgroundSprite, Image.Type.Simple, Color.white);
            var sheet = CreatePanel("Lobby Sheet", appRoot.transform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f), theme.panelColor);
            refs.SheetImage = sheet;
            ApplySprite(sheet, theme.sheetSprite, Image.Type.Sliced, new Color(0.15f, 0.16f, 0.18f, 0.98f));
            refs.TitleText = CreateText("Title", sheet.transform, "BACKYARD LEGENDS", 48, FontStyle.Bold, theme.gold, TextAnchor.UpperCenter, new Vector2(0.08f, 0.89f), new Vector2(0.92f, 0.98f));
            refs.SubtitleText = CreateText("Subtitle", sheet.transform, "Single-player Phase 1. Portrait-first Spades with authored scenes and prefabbed UI.", 22, FontStyle.Normal, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.74f), new Vector2(0.88f, 0.84f));

            var previewPanel = CreatePanel("Preview Panel", sheet.transform, new Vector2(0.08f, 0.39f), new Vector2(0.92f, 0.69f), new Color(0f, 0f, 0f, 0.08f));
            refs.PreviewPanelImage = previewPanel;
            ApplySprite(previewPanel, theme.softPanelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.22f));
            refs.HeroCardImage = CreateImage("Hero Card", previewPanel.transform, theme.cardBack);
            SetAnchors(refs.HeroCardImage.rectTransform, new Vector2(0.04f, 0.14f), new Vector2(0.23f, 0.86f));
            ApplySprite(refs.HeroCardImage, theme.cardBackHeroSprite, Image.Type.Sliced, Color.white);
            CreateText("Preview Mark", refs.HeroCardImage.transform, "\u2660", 72, FontStyle.Bold, theme.gold, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            refs.FlavorText = CreateText("Flavor", previewPanel.transform, "Street-table tone over casino polish. Dark surface, gold calls, red pressure, green payoff.", 21, FontStyle.Normal, theme.mutedText, TextAnchor.UpperLeft, new Vector2(0.30f, 0.58f), new Vector2(0.90f, 0.82f));
            refs.RuleSummaryText = CreateText("Rule Summary", previewPanel.transform, "Classic locks the strict rules first. Street swaps in its looser variant once the core loop is stable.", 22, FontStyle.Normal, theme.primaryText, TextAnchor.UpperLeft, new Vector2(0.30f, 0.18f), new Vector2(0.90f, 0.54f));
            refs.SelectionSummaryText = CreateText("Selection Summary", sheet.transform, "Portrait-first | Human vs 3 AI", 20, FontStyle.Bold, theme.mutedText, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.31f), new Vector2(0.88f, 0.37f));

            var modeRow = CreateGroup("Mode Row", sheet.transform, new Vector2(0.08f, 0.19f), new Vector2(0.92f, 0.27f));
            refs.ModeButtons = new[]
            {
                CreateButton("Classic Mode", modeRow.transform, "CLASSIC", theme.gold),
                CreateButton("Street Mode", modeRow.transform, "STREET", theme.panelStroke)
            };

            var scoreRow = CreateGroup("Score Row", sheet.transform, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.18f));
            refs.TargetButtons = new[]
            {
                CreateButton("Score 100", scoreRow.transform, "100", theme.gold),
                CreateButton("Score 200", scoreRow.transform, "200", theme.panelStroke),
                CreateButton("Score 500", scoreRow.transform, "500", theme.panelStroke)
            };

            refs.StartMatchButton = CreateButton("Start Match", sheet.transform, "START MATCH", theme.green, new Vector2(0.18f, 0.03f), new Vector2(0.82f, 0.09f));
            foreach (var button in refs.ModeButtons)
            {
                ApplySprite(button.image, theme.buttonSprite, Image.Type.Sliced, button.image.color);
            }

            foreach (var button in refs.TargetButtons)
            {
                ApplySprite(button.image, theme.buttonSprite, Image.Type.Sliced, button.image.color);
            }

            ApplySprite(refs.StartMatchButton.image, theme.buttonSprite, Image.Type.Sliced, refs.StartMatchButton.image.color);

            AssignLobbyRefs(appRoot.GetComponent<BackyardLegendsLobbyPresenter>(), refs, theme);
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void BuildGameplayScene(Scene scene, ThemeConfig theme, GameObject seatPrefab, GameObject trickPrefab, GameObject cardPrefab)
        {
            CreateMainCamera(scene);
            var safeRoot = CreateCanvasScaffold(scene, "Backyard Legends Gameplay Canvas", out var background);

            var appRoot = new GameObject("Backyard Legends Gameplay", typeof(RectTransform), typeof(BackyardLegendsBootstrap), typeof(BackyardLegendsSceneRefs));
            appRoot.transform.SetParent(safeRoot, false);
            var appRect = appRoot.GetComponent<RectTransform>();
            Stretch(appRect);
            var refs = appRoot.GetComponent<BackyardLegendsSceneRefs>();

            refs.BackgroundImage = background;
            ApplySprite(background, theme.tableBackgroundSprite, Image.Type.Simple, Color.white);
            var hudPanel = CreatePanel("HUD", appRoot.transform, new Vector2(0.03f, 0.86f), new Vector2(0.97f, 0.985f), theme.panelColor);
            refs.HudPanel = hudPanel;
            ApplySprite(hudPanel, theme.panelSprite, Image.Type.Sliced, theme.panelColor);
            refs.BackButton = CreateButton("Back Button", hudPanel.transform, "MENU", theme.panelStroke, new Vector2(0.04f, 0.60f), new Vector2(0.18f, 0.92f));
            ApplySprite(refs.BackButton.image, theme.buttonSprite, Image.Type.Sliced, theme.panelStroke);
            refs.HudModeText = CreateText("Mode", hudPanel.transform, "CLASSIC | 100", 22, FontStyle.Bold, theme.primaryText, TextAnchor.UpperRight, new Vector2(0.66f, 0.60f), new Vector2(0.96f, 0.94f));
            refs.StatusText = CreateText("Status", hudPanel.transform, "Preparing the table.", 18, FontStyle.Normal, theme.mutedText, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.46f), new Vector2(0.62f, 0.66f));
            CreateText("Title", hudPanel.transform, "BACKYARD LEGENDS", 38, FontStyle.Bold, theme.gold, TextAnchor.UpperLeft, new Vector2(0.21f, 0.58f), new Vector2(0.60f, 0.96f));
            refs.HomeScoreText = CreateChipLabel(hudPanel.transform, "Home Chip", new Vector2(0.04f, 0.08f), new Vector2(0.40f, 0.34f), "HOME 0/100 | BID -- | BOOKS 0", theme.green, theme.backgroundColor);
            refs.AwayScoreText = CreateChipLabel(hudPanel.transform, "Away Chip", new Vector2(0.60f, 0.08f), new Vector2(0.96f, 0.34f), "AWAY 0/100 | BID -- | BOOKS 0", theme.red, theme.backgroundColor);
            ApplySprite(refs.HomeScoreText.transform.parent.GetComponent<Image>(), theme.chipSprite, Image.Type.Sliced, theme.green);
            ApplySprite(refs.AwayScoreText.transform.parent.GetComponent<Image>(), theme.chipSprite, Image.Type.Sliced, theme.red);
            refs.HomeDeltaText = CreateText("Home Delta", hudPanel.transform, string.Empty, 18, FontStyle.Bold, theme.green, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.33f), new Vector2(0.23f, 0.48f));
            refs.AwayDeltaText = CreateText("Away Delta", hudPanel.transform, string.Empty, 18, FontStyle.Bold, theme.red, TextAnchor.MiddleCenter, new Vector2(0.77f, 0.33f), new Vector2(0.96f, 0.48f));
            refs.TimerHookText = CreateText("Turn Clock", hudPanel.transform, "AUTO DEAL", 15, FontStyle.Bold, theme.mutedText, TextAnchor.MiddleCenter, new Vector2(0.40f, 0.08f), new Vector2(0.60f, 0.34f));

            var tablePanel = CreatePanel("Table", appRoot.transform, new Vector2(0.03f, 0.27f), new Vector2(0.97f, 0.84f), new Color(0.1f, 0.11f, 0.12f, 0.86f));
            refs.TablePanel = tablePanel;
            ApplySprite(tablePanel, theme.panelSprite, Image.Type.Sliced, new Color(0.18f, 0.19f, 0.21f, 0.9f));
            refs.CenterHintText = CreateText("Center Hint", tablePanel.transform, "Match loading.", 22, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.30f, 0.31f), new Vector2(0.70f, 0.39f));
            var feedPanel = CreatePanel("Feed Panel", tablePanel.transform, new Vector2(0.03f, 0.17f), new Vector2(0.23f, 0.34f), new Color(1f, 1f, 1f, 0.18f));
            refs.FeedPanel = feedPanel;
            ApplySprite(feedPanel, theme.softPanelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.18f));
            refs.FeedText = CreateText("Feed", feedPanel.transform, "TABLE FEED\nNo hands yet.", 14, FontStyle.Normal, theme.mutedText, TextAnchor.UpperLeft, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f));
            refs.DeckAnchorImage = CreatePanel("Deck Anchor", tablePanel.transform, new Vector2(0.03f, 0.36f), new Vector2(0.14f, 0.48f), new Color(0.13f, 0.14f, 0.16f, 0.94f));
            ApplySprite(refs.DeckAnchorImage, theme.softPanelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.24f));
            refs.DeckAnchorText = CreateText("Deck Label", refs.DeckAnchorImage.transform, string.Empty, 16, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f));
            refs.DeckAnchorImage.gameObject.SetActive(false);
            refs.DiscardAnchorImage = CreatePanel("Discard Anchor", tablePanel.transform, new Vector2(0.86f, 0.36f), new Vector2(0.97f, 0.48f), new Color(0.13f, 0.14f, 0.16f, 0.94f));
            ApplySprite(refs.DiscardAnchorImage, theme.softPanelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.24f));
            refs.DiscardAnchorText = CreateText("Discard Label", refs.DiscardAnchorImage.transform, string.Empty, 16, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f));
            refs.DiscardAnchorImage.gameObject.SetActive(false);
            refs.OpeningStackImage = CreatePanel("Opening Stack", tablePanel.transform, new Vector2(0.42f, 0.39f), new Vector2(0.58f, 0.60f), new Color(0.13f, 0.14f, 0.16f, 0.96f));
            ApplySprite(refs.OpeningStackImage, theme.cardBackHeroSprite != null ? theme.cardBackHeroSprite : theme.softPanelSprite, Image.Type.Sliced, Color.white);
            refs.OpeningStackText = CreateText("Opening Stack Label", refs.OpeningStackImage.transform, "52\nCARDS", 24, FontStyle.Bold, theme.gold, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f));
            CreateLastTrickDisplay(tablePanel.transform, refs, theme, cardPrefab);
            refs.LastTrickText = CreateText("Last Trick Fallback", tablePanel.transform, "Previous book: no tricks resolved yet.", 16, FontStyle.Normal, theme.mutedText, TextAnchor.MiddleCenter, new Vector2(0.31f, 0.45f), new Vector2(0.69f, 0.57f));
            refs.LastTrickText.gameObject.SetActive(false);
            refs.BannerText = CreateText("Banner", tablePanel.transform, string.Empty, 32, FontStyle.Bold, theme.gold, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.58f), new Vector2(0.82f, 0.7f));
            refs.DealButton = CreateButton("Deal Button", tablePanel.transform, "DEAL", theme.green, new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.34f));
            ApplySprite(refs.DealButton.image, theme.buttonSprite, Image.Type.Sliced, theme.green);
            refs.DealButton.gameObject.SetActive(false);

            refs.TopSeat = InstantiateSeat(seatPrefab, tablePanel.transform, new Vector2(0.35f, 0.81f), new Vector2(0.65f, 0.95f), "Top Seat");
            refs.LeftSeat = InstantiateSeat(seatPrefab, tablePanel.transform, new Vector2(0.02f, 0.50f), new Vector2(0.28f, 0.69f), "Left Seat");
            refs.RightSeat = InstantiateSeat(seatPrefab, tablePanel.transform, new Vector2(0.72f, 0.50f), new Vector2(0.98f, 0.69f), "Right Seat");
            refs.BottomSeat = InstantiateSeat(seatPrefab, tablePanel.transform, new Vector2(0.18f, 0.01f), new Vector2(0.82f, 0.16f), "Bottom Seat");
            refs.TopTrick = InstantiateTrick(trickPrefab, tablePanel.transform, new Vector2(0.43f, 0.57f), new Vector2(0.57f, 0.75f), "Top Trick");
            refs.LeftTrick = InstantiateTrick(trickPrefab, tablePanel.transform, new Vector2(0.17f, 0.36f), new Vector2(0.30f, 0.54f), "Left Trick");
            refs.RightTrick = InstantiateTrick(trickPrefab, tablePanel.transform, new Vector2(0.70f, 0.36f), new Vector2(0.83f, 0.54f), "Right Trick");
            refs.BottomTrick = InstantiateTrick(trickPrefab, tablePanel.transform, new Vector2(0.43f, 0.22f), new Vector2(0.57f, 0.40f), "Bottom Trick");

            var handPanel = CreatePanel("Hand Panel", appRoot.transform, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.25f), theme.panelColor);
            refs.HandPanel = handPanel;
            ApplySprite(handPanel, theme.panelSprite, Image.Type.Sliced, theme.panelColor);
            CreateText("Hand Label", handPanel.transform, "YOUR HAND", 26, FontStyle.Bold, theme.primaryText, TextAnchor.UpperLeft, new Vector2(0.04f, 0.72f), new Vector2(0.4f, 0.95f));
            refs.PlaySelectedButton = CreateButton("Play Selected", handPanel.transform, "PLAY SELECTED", theme.gold, new Vector2(0.62f, 0.72f), new Vector2(0.96f, 0.95f));
            ApplySprite(refs.PlaySelectedButton.image, theme.buttonSprite, Image.Type.Sliced, theme.gold);

            var handShelf = CreatePanel("Hand Shelf", handPanel.transform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.64f), new Color(0f, 0f, 0f, 0.08f));
            ApplySprite(handShelf, theme.softPanelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.3f));

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(handShelf.transform, false);
            refs.HandContent = content.GetComponent<RectTransform>();
            Stretch(refs.HandContent, 8f);
            refs.HandContent.anchorMin = new Vector2(0f, 0f);
            refs.HandContent.anchorMax = new Vector2(1f, 1f);
            refs.HandContent.pivot = new Vector2(0.5f, 0.5f);

            BuildGameplaySheets(appRoot.transform, refs, theme);
            refs.CardButtonPrefab = cardPrefab.GetComponent<CardButtonView>();
            AssignGameplayRefs(appRoot.GetComponent<BackyardLegendsBootstrap>(), refs, theme);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void BuildGameplayScene(ThemeConfig theme, GameObject seatPrefab, GameObject trickPrefab, GameObject cardPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildGameplayScene(scene, theme, seatPrefab, trickPrefab, cardPrefab);
        }

        private static void BuildGameplaySheets(Transform parent, BackyardLegendsSceneRefs refs, ThemeConfig theme)
        {
            refs.BidSheetImage = CreatePanel("Bid Sheet", parent, new Vector2(0.04f, 0.29f), new Vector2(0.96f, 0.50f), theme.panelColor);
            refs.BidSheet = refs.BidSheetImage.rectTransform;
            ApplySprite(refs.BidSheetImage, theme.sheetSprite, Image.Type.Sliced, new Color(0.15f, 0.16f, 0.18f, 0.98f));
            CreateText("Bid Title", refs.BidSheet, "CALL YOUR BID", 24, FontStyle.Bold, theme.gold, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.72f), new Vector2(0.28f, 0.96f));
            var bidGrid = new GameObject("Bid Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            bidGrid.transform.SetParent(refs.BidSheet, false);
            var bidGridRect = bidGrid.GetComponent<RectTransform>();
            SetAnchors(bidGridRect, new Vector2(0.30f, 0.30f), new Vector2(0.96f, 0.90f));
            var grid = bidGrid.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 7;
            grid.spacing = new Vector2(8f, 8f);
            grid.cellSize = new Vector2(76f, 58f);
            refs.ConfirmBidButton = CreateButton("Confirm Bid", refs.BidSheet, "CONFIRM BID", theme.green, new Vector2(0.36f, 0.05f), new Vector2(0.64f, 0.26f));
            ApplySprite(refs.ConfirmBidButton.image, theme.buttonSprite, Image.Type.Sliced, theme.green);
            refs.BidButtons = new Button[14];
            for (var bid = 0; bid <= 13; bid++)
            {
                refs.BidButtons[bid] = CreateButton($"Bid {bid}", bidGrid.transform, bid == 0 ? "NIL" : bid.ToString(), theme.panelStroke);
                ApplySprite(refs.BidButtons[bid].image, theme.buttonSprite, Image.Type.Sliced, refs.BidButtons[bid].image.color);
            }

            refs.RoundSheetImage = CreatePanel("Round Sheet", parent, new Vector2(0.04f, 0.55f), new Vector2(0.96f, 0.86f), theme.panelColor);
            refs.RoundSheet = refs.RoundSheetImage.rectTransform;
            ApplySprite(refs.RoundSheetImage, theme.sheetSprite, Image.Type.Sliced, new Color(0.15f, 0.16f, 0.18f, 0.98f));
            CreateText("Round Title", refs.RoundSheet, "ROUND WRAP", 36, FontStyle.Bold, theme.gold, TextAnchor.UpperCenter, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.95f));
            refs.RoundSummaryText = CreateText("Round Summary", refs.RoundSheet, "Round summary goes here.", 20, FontStyle.Normal, theme.primaryText, TextAnchor.UpperLeft, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.76f));
            refs.NextRoundButton = CreateButton("Next Round", refs.RoundSheet, "NEXT ROUND", theme.green, new Vector2(0.18f, 0.04f), new Vector2(0.82f, 0.16f));
            ApplySprite(refs.NextRoundButton.image, theme.buttonSprite, Image.Type.Sliced, theme.green);

            refs.EndSheetImage = CreatePanel("End Sheet", parent, new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.84f), theme.panelColor);
            refs.EndSheet = refs.EndSheetImage.rectTransform;
            ApplySprite(refs.EndSheetImage, theme.sheetSprite, Image.Type.Sliced, new Color(0.15f, 0.16f, 0.18f, 0.98f));
            CreateText("End Title", refs.EndSheet, "MATCH COMPLETE", 36, FontStyle.Bold, theme.gold, TextAnchor.UpperCenter, new Vector2(0.08f, 0.8f), new Vector2(0.92f, 0.95f));
            refs.EndSummaryText = CreateText("End Summary", refs.EndSheet, "Winner info", 20, FontStyle.Normal, theme.primaryText, TextAnchor.UpperLeft, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.78f));
            refs.ReturnToLobbyButton = CreateButton("Back To Lobby", refs.EndSheet, "BACK TO LOBBY", theme.panelStroke, new Vector2(0.08f, 0.06f), new Vector2(0.44f, 0.18f));
            refs.RematchButton = CreateButton("Rematch", refs.EndSheet, "RUN IT BACK", theme.green, new Vector2(0.56f, 0.06f), new Vector2(0.92f, 0.18f));
            ApplySprite(refs.ReturnToLobbyButton.image, theme.buttonSprite, Image.Type.Sliced, theme.panelStroke);
            ApplySprite(refs.RematchButton.image, theme.buttonSprite, Image.Type.Sliced, theme.green);

            refs.ExitPromptOverlayImage = CreatePanel("Exit Prompt Overlay", parent, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.64f));
            refs.ExitPromptOverlay = refs.ExitPromptOverlayImage.rectTransform;
            ApplySprite(refs.ExitPromptOverlayImage, theme.softPanelSprite, Image.Type.Sliced, new Color(0f, 0f, 0f, 0.64f));
            refs.ExitPromptPanelImage = CreatePanel("Exit Prompt Panel", refs.ExitPromptOverlay, new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.65f), theme.panelColor);
            ApplySprite(refs.ExitPromptPanelImage, theme.sheetSprite, Image.Type.Sliced, new Color(0.15f, 0.16f, 0.18f, 0.98f));
            refs.ExitPromptTitleText = CreateText("Exit Prompt Title", refs.ExitPromptPanelImage.transform, "LEAVE THE TABLE?", 32, FontStyle.Bold, theme.gold, TextAnchor.UpperCenter, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.94f));
            refs.ExitPromptBodyText = CreateText("Exit Prompt Body", refs.ExitPromptPanelImage.transform, "Current match progress will be lost if you go back to the lobby.", 23, FontStyle.Normal, theme.primaryText, TextAnchor.UpperLeft, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.68f));
            refs.ExitPromptCancelButton = CreateButton("Exit Prompt Cancel", refs.ExitPromptPanelImage.transform, "STAY HERE", theme.green, new Vector2(0.08f, 0.08f), new Vector2(0.44f, 0.22f));
            refs.ExitPromptConfirmButton = CreateButton("Exit Prompt Confirm", refs.ExitPromptPanelImage.transform, "GO TO LOBBY", theme.red, new Vector2(0.56f, 0.08f), new Vector2(0.92f, 0.22f));
            ApplySprite(refs.ExitPromptCancelButton.image, theme.buttonSprite, Image.Type.Sliced, theme.green);
            ApplySprite(refs.ExitPromptConfirmButton.image, theme.buttonSprite, Image.Type.Sliced, theme.red);
            refs.ExitPromptOverlay.gameObject.SetActive(false);
        }

        private static RectTransform CreateCanvasScaffold(Scene scene, string canvasName, out Image background)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            var canvasGo = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.85f;

            background = CreateImage("Background", canvasGo.transform, Color.white);
            Stretch(background.rectTransform);

            var safeRoot = new GameObject("Safe Area", typeof(RectTransform), typeof(SafeAreaPanel));
            safeRoot.transform.SetParent(canvasGo.transform, false);
            var safeRect = safeRoot.GetComponent<RectTransform>();
            Stretch(safeRect);
            return safeRect;
        }

        private static void CreateMainCamera(Scene scene)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            SceneManager.MoveGameObjectToScene(cameraGo, scene);
        }

        private static GameObject CreateSeatPanelPrefab(ThemeConfig theme)
        {
            var path = $"{UiPrefabFolder}/SeatPanel.prefab";
            var root = new GameObject("SeatPanel", typeof(RectTransform), typeof(Image), typeof(SeatPanelView));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 150f);
            ConfigureImage(root.GetComponent<Image>(), theme.panelColor);
            ApplySprite(root.GetComponent<Image>(), theme.panelSprite, Image.Type.Sliced, theme.panelColor);
            var view = root.GetComponent<SeatPanelView>();
            view.Panel = root.GetComponent<Image>();
            view.NameText = CreateText("Name", root.transform, "SEAT", 24, FontStyle.Bold, theme.gold, TextAnchor.UpperLeft, new Vector2(0.08f, 0.56f), new Vector2(0.92f, 0.9f));
            view.StatusText = CreateText("Status", root.transform, "Status", 18, FontStyle.Normal, theme.mutedText, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.56f));
            view.BidText = CreateText("Bid", root.transform, "Bid: --", 18, FontStyle.Normal, theme.primaryText, TextAnchor.LowerLeft, new Vector2(0.08f, 0.08f), new Vector2(0.45f, 0.32f));
            view.TricksText = CreateText("Books", root.transform, "Books: 0", 18, FontStyle.Normal, theme.primaryText, TextAnchor.LowerRight, new Vector2(0.5f, 0.08f), new Vector2(0.92f, 0.32f));
            var bubbleGo = new GameObject("Bid Callout", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            bubbleGo.transform.SetParent(root.transform, false);
            var bubbleRect = bubbleGo.GetComponent<RectTransform>();
            SetAnchors(bubbleRect, new Vector2(0.08f, 1.00f), new Vector2(0.92f, 1.42f));
            var bubbleImage = bubbleGo.GetComponent<Image>();
            ConfigureImage(bubbleImage, new Color(0.15f, 0.16f, 0.18f, 0.95f));
            ApplySprite(bubbleImage, theme.buttonSprite, Image.Type.Sliced, new Color(0.15f, 0.16f, 0.18f, 0.95f));
            var bubbleGroup = bubbleGo.GetComponent<CanvasGroup>();
            bubbleGroup.alpha = 0f;
            bubbleGroup.blocksRaycasts = false;
            bubbleGroup.interactable = false;
            view.BidCalloutPanel = bubbleImage;
            view.BidCalloutGroup = bubbleGroup;
            view.BidCalloutText = CreateText("Bubble Label", bubbleGo.transform, "I BID 3", 46, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.94f));
            view.BidCalloutText.resizeTextForBestFit = true;
            view.BidCalloutText.resizeTextMinSize = 34;
            view.BidCalloutText.resizeTextMaxSize = 46;
            return SavePrefab(path, root);
        }

        private static GameObject CreateTrickSlotPrefab(ThemeConfig theme)
        {
            var path = $"{UiPrefabFolder}/TrickSlot.prefab";
            var root = new GameObject("TrickSlot", typeof(RectTransform), typeof(Image), typeof(TrickSlotView));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(108f, 148f);
            ConfigureImage(root.GetComponent<Image>(), new Color(1f, 1f, 1f, 0.08f));
            ApplySprite(root.GetComponent<Image>(), theme.softPanelSprite, Image.Type.Sliced, Color.white);
            var view = root.GetComponent<TrickSlotView>();
            view.Panel = root.GetComponent<Image>();
            view.RankText = CreateText("Rank", root.transform, "--", 36, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.14f, 0.34f), new Vector2(0.86f, 0.78f));
            view.SuitText = CreateText("Suit", root.transform, "SEAT", 16, FontStyle.Bold, theme.mutedText, TextAnchor.MiddleCenter, new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.26f));
            return SavePrefab(path, root);
        }

        private static GameObject CreateCardButtonPrefab(ThemeConfig theme)
        {
            var path = $"{UiPrefabFolder}/CardButton.prefab";
            var root = new GameObject("CardButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup), typeof(CardButtonView));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(82f, 116f);
            ConfigureImage(root.GetComponent<Image>(), theme.cardFace);
            ApplySprite(root.GetComponent<Image>(), theme.cardFaceDefaultSprite, Image.Type.Sliced, Color.white);
            var button = root.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = root.GetComponent<Image>();
            var view = root.GetComponent<CardButtonView>();
            view.Panel = root.GetComponent<Image>();
            view.Button = button;
            view.CanvasGroup = root.GetComponent<CanvasGroup>();
            view.RankText = CreateText("Rank", root.transform, "A", 28, FontStyle.Bold, new Color(0.08f, 0.08f, 0.08f, 1f), TextAnchor.UpperLeft, new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.88f));
            view.SuitText = CreateText("Suit", root.transform, "\u2660", 32, FontStyle.Bold, theme.primaryText, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.14f), new Vector2(0.88f, 0.56f));
            return SavePrefab(path, root);
        }

        private static SeatPanelView InstantiateSeat(GameObject prefab, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string name)
        {
            var seat = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            seat.name = name;
            SetAnchors((RectTransform)seat.transform, anchorMin, anchorMax);
            return seat.GetComponent<SeatPanelView>();
        }

        private static TrickSlotView InstantiateTrick(GameObject prefab, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string name)
        {
            var trick = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            trick.name = name;
            SetAnchors((RectTransform)trick.transform, anchorMin, anchorMax);
            return trick.GetComponent<TrickSlotView>();
        }

        private static void CreateLastTrickDisplay(Transform parent, BackyardLegendsSceneRefs refs, ThemeConfig theme, GameObject cardPrefab)
        {
            var panel = CreatePanel("Last Hand Played", parent, new Vector2(0.31f, 0.45f), new Vector2(0.69f, 0.57f), new Color(1f, 1f, 1f, 0.13f));
            ApplySprite(panel, theme.softPanelSprite, Image.Type.Sliced, new Color(1f, 1f, 1f, 0.13f));
            panel.raycastTarget = false;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            refs.LastTrickPanel = panel;
            refs.LastTrickGroup = group;

            refs.LastTrickTitleText = CreateText("Title", panel.transform, "LAST HAND PLAYED", 13, FontStyle.Bold, theme.mutedText, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.94f));
            refs.LastTrickTitleText.resizeTextForBestFit = true;
            refs.LastTrickTitleText.resizeTextMinSize = 9;
            refs.LastTrickTitleText.resizeTextMaxSize = 13;

            var cardsRoot = new GameObject("Cards", typeof(RectTransform));
            cardsRoot.transform.SetParent(panel.transform, false);
            refs.LastTrickCardsRoot = cardsRoot.GetComponent<RectTransform>();
            SetAnchors(refs.LastTrickCardsRoot, new Vector2(0.04f, 0.07f), new Vector2(0.96f, 0.70f));

            refs.LastTrickCards = new CardButtonView[4];
            var cardSize = new Vector2(82f, 116f) * 0.54f;
            var spacing = 8f;
            var totalWidth = refs.LastTrickCards.Length * cardSize.x + (refs.LastTrickCards.Length - 1) * spacing;
            for (var index = 0; index < refs.LastTrickCards.Length; index++)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, refs.LastTrickCardsRoot);
                card.name = $"Last Hand Card {index + 1}";
                var view = card.GetComponent<CardButtonView>();
                var rect = view.Root;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = cardSize;
                rect.anchoredPosition = new Vector2(-totalWidth * 0.5f + cardSize.x * 0.5f + index * (cardSize.x + spacing), 0f);
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, 3f, index / 3f));

                view.Button.onClick.RemoveAllListeners();
                view.Button.enabled = false;
                view.Panel.raycastTarget = false;
                view.RankText.raycastTarget = false;
                view.SuitText.raycastTarget = false;
                view.CanvasGroup.alpha = 0.92f;
                view.CanvasGroup.blocksRaycasts = false;
                view.CanvasGroup.interactable = false;
                view.FaceImage = CreateImage("Face Art", card.transform, Color.clear);
                view.FaceImage.transform.SetSiblingIndex(0);
                view.FaceImage.raycastTarget = false;
                view.FaceImage.preserveAspect = true;
                view.FaceImage.enabled = false;
                SetAnchors(view.FaceImage.rectTransform, Vector2.zero, Vector2.one);
                refs.LastTrickCards[index] = view;
            }

            panel.gameObject.SetActive(false);
        }

        private static void AssignLobbyRefs(BackyardLegendsLobbyPresenter presenter, BackyardLegendsLobbySceneRefs refs, ThemeConfig theme)
        {
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("sceneRefs").objectReferenceValue = refs;
            serialized.FindProperty("themeOverride").objectReferenceValue = theme;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static void AssignGameplayRefs(BackyardLegendsBootstrap bootstrap, BackyardLegendsSceneRefs refs, ThemeConfig theme)
        {
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("sceneRefs").objectReferenceValue = refs;
            serialized.FindProperty("themeOverride").objectReferenceValue = theme;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static GameObject SavePrefab(string path, GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var split = path.Split('/');
            var current = split[0];
            for (var i = 1; i < split.Length; i++)
            {
                var next = $"{current}/{split[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = CreateImage(name, parent, color);
            SetAnchors(image.rectTransform, anchorMin, anchorMax);
            return image;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            ConfigureImage(image, color);
            return image;
        }

        private static void ConfigureImage(Image image, Color color)
        {
            image.sprite = UiSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static void ApplySprite(Image image, Sprite sprite, Image.Type type, Color color)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = type;
            image.color = color;
        }

        private static Text CreateText(string name, Transform parent, string text, int fontSize, FontStyle fontStyle, Color color, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = UiFont;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            SetAnchors(label.rectTransform, anchorMin, anchorMax);
            return label;
        }

        private static Text CreateChipLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string text, Color accent, Color labelColor)
        {
            var chip = CreatePanel(name, parent, anchorMin, anchorMax, accent);
            var label = CreateText("Label", chip.transform, text, 14, FontStyle.Bold, labelColor, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            Stretch(label.rectTransform, 8f);
            return label;
        }

        private static Button CreateButton(string name, Transform parent, string text, Color tint)
        {
            var button = CreateButton(name, parent, text, tint, Vector2.zero, Vector2.one);
            ((RectTransform)button.transform).offsetMin = Vector2.zero;
            ((RectTransform)button.transform).offsetMax = Vector2.zero;
            return button;
        }

        private static Button CreateButton(string name, Transform parent, string text, Color tint, Vector2 anchorMin, Vector2 anchorMax)
        {
            var buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
            var rect = buttonGo.GetComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            var image = buttonGo.GetComponent<Image>();
            ConfigureImage(image, tint);
            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            var label = CreateText("Label", buttonGo.transform, text, 22, FontStyle.Bold, new Color(0.07f, 0.08f, 0.09f, 1f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            Stretch(label.rectTransform, 8f);
            return button;
        }

        private static HorizontalLayoutGroup CreateGroup(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        private static void Stretch(RectTransform rectTransform, float inset = 0f)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private static void SetAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
