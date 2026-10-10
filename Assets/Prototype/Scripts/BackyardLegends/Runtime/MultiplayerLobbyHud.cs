using System.Collections.Generic;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    /// <summary>
    /// Pre-match waiting lobby overlay: seats, teams, ready, invite code, leave.
    /// Instantiate the authored prefab at Prototype/Prefabs/UI/MultiplayerLobbyHud
    /// via a scene reference (BackyardLegendsBootstrap). Falls back to a runtime layout if missing.
    /// </summary>
    public sealed class MultiplayerLobbyHud : MonoBehaviour
    {
        [System.Serializable]
        public sealed class SeatSlotRefs
        {
            public SeatId Seat;
            public TextMeshProUGUI NameText;
            public Image ReadyIcon;
            public Image CrownIcon;
            public Image AvatarIcon;
            public TextMeshProUGUI HostBadge;
        }

        [Header("Root")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI codeText;

        [Header("Actions")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Button headerCopyButton;
        [SerializeField] private Button headerShareButton;
        [SerializeField] private Button footerInviteButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button leaveButton;

        [Header("Seats")]
        [SerializeField] private List<SeatSlotRefs> seatSlots = new();

        private bool localReady;
        private bool pendingReadyRequest;
        private bool lobbyClosed;
        private string lastRosterMessage = string.Empty;
        private SpadesTableNetwork boundTable;
        [System.NonSerialized] private bool callbacksWired;
        private static TMP_FontAsset cachedFont;

        public static MultiplayerLobbyHud Ensure(MultiplayerLobbyHud prefab = null)
        {
            var existing = FindFirstObjectByType<MultiplayerLobbyHud>();
            if (existing != null)
            {
                existing.EnsureBoundToTable();
                return existing;
            }

            MultiplayerLobbyHud hud = null;
            if (prefab != null)
            {
                hud = Instantiate(prefab);
                hud.name = "Multiplayer Lobby HUD";
            }
            else
            {
                Debug.LogWarning(
                    "MultiplayerLobbyHud prefab is not assigned on BackyardLegendsBootstrap. " +
                    "Assign Assets/Prototype/Prefabs/UI/MultiplayerLobbyHud. Using runtime layout.");
                var go = new GameObject("Multiplayer Lobby HUD");
                hud = go.AddComponent<MultiplayerLobbyHud>();
            }

            DontDestroyOnLoad(hud.gameObject);
            return hud;
        }

        private void Awake()
        {
            if (root == null)
            {
                BuildDefaultLayout();
            }

            ResolveSeatSlotRefs();
            WireCallbacks();
        }

        private void OnEnable()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            session.StateChanged -= Refresh;
            session.StateChanged += Refresh;
            EnsureBoundToTable();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindTable();
            if (SpadesNetworkSession.Instance != null)
            {
                SpadesNetworkSession.Instance.StateChanged -= Refresh;
            }
        }

        private void Update()
        {
            EnsureBoundToTable();
            if (root != null && root.gameObject.activeSelf)
            {
                Refresh();
            }
        }

        private void EnsureBoundToTable()
        {
            var table = SpadesTableNetwork.Instance;
            if (table == boundTable)
            {
                return;
            }

            UnbindTable();
            boundTable = table;
            if (boundTable == null)
            {
                return;
            }

            boundTable.LobbyStateChanged += Refresh;
            boundTable.NetworkEventReceived += HandleNetworkEvent;
            boundTable.MatchBound += HandleMatchBound;
            Debug.Log("MultiplayerLobbyHud bound to SpadesTableNetwork");
        }

        private void UnbindTable()
        {
            if (boundTable == null)
            {
                return;
            }

            boundTable.LobbyStateChanged -= Refresh;
            boundTable.NetworkEventReceived -= HandleNetworkEvent;
            boundTable.MatchBound -= HandleMatchBound;
            boundTable = null;
        }

        private void HandleMatchBound()
        {
            lobbyClosed = true;
            Hide();
        }

        private void HandleNetworkEvent(SpadesNetworkEventPayload payload)
        {
            if (payload.Kind == (byte)SpadesNetworkEventKind.LobbyRoster)
            {
                lastRosterMessage = payload.Message ?? string.Empty;
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.SeatAssigned)
            {
                if (pendingReadyRequest || localReady)
                {
                    pendingReadyRequest = false;
                    localReady = true;
                    SpadesTableNetwork.Instance?.LocalSetLobbyReady(true);
                }

                Refresh();
                return;
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.ActionRejected)
            {
                var message = payload.Message ?? string.Empty;
                if (message.IndexOf("Seat not assigned", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pendingReadyRequest = localReady;
                }

                Refresh();
                return;
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.TableReady ||
                payload.Kind == (byte)SpadesNetworkEventKind.MatchStarted ||
                payload.Kind == (byte)SpadesNetworkEventKind.CatchUpState)
            {
                lobbyClosed = true;
                pendingReadyRequest = false;
                Hide();
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            var table = SpadesTableNetwork.Instance;
            var matchLive = lobbyClosed || (table != null && table.MatchStarted);
            var show = session.IsOnline && !matchLive;
            if (root != null)
            {
                root.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            if (codeText != null)
            {
                codeText.text = string.IsNullOrEmpty(session.JoinCode)
                    ? "Invite Code: —"
                    : $"Invite Code: {session.JoinCode}";
            }

            if (statusText != null)
            {
                var waiting = "Match starts when all 4 players are ready…";
                if (table != null)
                {
                    waiting = $"Match starts when all 4 are ready… ({table.ReadyCount}/4)";
                }

                if (!string.IsNullOrEmpty(session.StatusMessage) &&
                    session.StatusMessage.IndexOf("Waiting", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    waiting = $"{waiting}\n{session.StatusMessage}";
                }

                statusText.text = waiting;
            }

            RefreshSeatSlots(table, session);

            if (readyButton != null)
            {
                var label = readyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = localReady ? "UNREADY" : "READY";
                }
            }
        }

        private void RefreshSeatSlots(SpadesTableNetwork table, SpadesNetworkSession session)
        {
            var roster = ParseRoster(table);
            for (var i = 0; i < seatSlots.Count; i++)
            {
                var slot = seatSlots[i];
                if (slot == null)
                {
                    continue;
                }

                roster.TryGetValue(slot.Seat, out var info);
                var name = info.Name;
                var ready = info.Ready;
                var occupied = !string.IsNullOrEmpty(name) && name != "— empty —";

                if (slot.NameText != null)
                {
                    slot.NameText.text = occupied ? name : "Waiting…";
                    slot.NameText.color = occupied ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                }

                if (slot.ReadyIcon != null)
                {
                    slot.ReadyIcon.enabled = occupied && ready;
                }

                if (slot.AvatarIcon != null)
                {
                    slot.AvatarIcon.enabled = occupied;
                }

                var isHostSeat = occupied && session.Role == SpadesNetworkRole.Host && slot.Seat == session.LocalLogicalSeat;
                if (!isHostSeat && occupied && table != null && table.IsServer && slot.Seat == SeatId.Bottom)
                {
                    isHostSeat = true;
                }

                if (slot.CrownIcon != null)
                {
                    slot.CrownIcon.enabled = isHostSeat;
                }

                if (slot.HostBadge != null)
                {
                    var badgeRoot = slot.HostBadge.transform.parent != null
                        ? slot.HostBadge.transform.parent.gameObject
                        : slot.HostBadge.gameObject;
                    badgeRoot.SetActive(isHostSeat);
                }
            }
        }

        private Dictionary<SeatId, (string Name, bool Ready)> ParseRoster(SpadesTableNetwork table)
        {
            var map = new Dictionary<SeatId, (string, bool)>
            {
                [SeatId.Bottom] = ("— empty —", false),
                [SeatId.Top] = ("— empty —", false),
                [SeatId.Left] = ("— empty —", false),
                [SeatId.Right] = ("— empty —", false)
            };

            if (table != null && table.IsServer)
            {
                foreach (var seat in new[] { SeatId.Bottom, SeatId.Top, SeatId.Left, SeatId.Right })
                {
                    var name = table.DisplayNames.TryGetValue(seat, out var n) ? n : "— empty —";
                    map[seat] = (name, table.IsSeatReady(seat));
                }

                return map;
            }

            if (string.IsNullOrEmpty(lastRosterMessage))
            {
                return map;
            }

            foreach (var part in lastRosterMessage.Split('|'))
            {
                var bits = part.Split(':');
                if (bits.Length < 4)
                {
                    continue;
                }

                if (!System.Enum.TryParse(bits[0], true, out SeatId seat))
                {
                    continue;
                }

                map[seat] = (bits[1], bits[2] == "R");
            }

            return map;
        }

        private void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void ToggleReady()
        {
            localReady = !localReady;
            var session = SpadesNetworkSession.GetOrCreate();
            if (!session.SeatAssigned)
            {
                pendingReadyRequest = localReady;
                session.SetStatus(localReady ? "Ready queued — waiting for seat…" : "Unready");
                Refresh();
                return;
            }

            pendingReadyRequest = false;
            SpadesTableNetwork.Instance?.LocalSetLobbyReady(localReady);
            Refresh();
        }

        private void CopyCode()
        {
            var code = SpadesNetworkSession.GetOrCreate().JoinCode;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = code;
            SpadesNetworkSession.GetOrCreate().SetStatus($"Copied invite code {code}");
            Refresh();
        }

        private void ImportInviteCode()
        {
            var pasted = GUIUtility.systemCopyBuffer != null ? GUIUtility.systemCopyBuffer.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(pasted))
            {
                SpadesNetworkSession.GetOrCreate().SetStatus("Clipboard empty — copy an invite code first.");
                Refresh();
                return;
            }

            var parts = pasted.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            var code = parts.Length > 0 ? parts[parts.Length - 1] : pasted;
            code = code.Trim().Trim(':', '.', ',', '"', '\'').ToUpperInvariant();
            if (code.Length < 4)
            {
                SpadesNetworkSession.GetOrCreate().SetStatus("Clipboard does not look like an invite code.");
                Refresh();
                return;
            }

            GUIUtility.systemCopyBuffer = code;
            if (codeText != null)
            {
                codeText.text = $"Invite Code: {code}";
            }

            SpadesNetworkSession.GetOrCreate().SetStatus($"Invite code ready: {code}");
            Refresh();
        }

        private void ShareCode()
        {
            var code = SpadesNetworkSession.GetOrCreate().JoinCode;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            GUIUtility.systemCopyBuffer = code;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intentClass = new AndroidJavaClass("android.content.Intent"))
                using (var intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                    intent.Call<AndroidJavaObject>("setType", "text/plain");
                    intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"),
                        $"Join my Backyard Legends table with code: {code}");
                    using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    {
                        var activity = unity.GetStatic<AndroidJavaObject>("currentActivity");
                        var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share invite code");
                        activity.Call("startActivity", chooser);
                    }
                }

                SpadesNetworkSession.GetOrCreate().SetStatus($"Sharing invite {code}");
            }
            catch (System.Exception)
            {
                SpadesNetworkSession.GetOrCreate().SetStatus($"Invite code copied: {code}");
            }
#else
            SpadesNetworkSession.GetOrCreate().SetStatus($"Invite code copied: {code}");
#endif
            Refresh();
        }

        private void LeaveRoom()
        {
            var table = SpadesTableNetwork.Instance;
            if (table != null && table.IsSpawned)
            {
                table.LocalLeaveTable();
                return;
            }

            SpadesNetworkManagerHost.GetOrCreate().Shutdown();
            BackyardLegendsSession.GetOrCreateRuntimeInstance().LoadLobbyScene();
        }

        private void WireCallbacks()
        {
            if (callbacksWired)
            {
                return;
            }

            callbacksWired = true;
            Bind(readyButton, ToggleReady);
            Bind(headerCopyButton, CopyCode);
            Bind(headerShareButton, ShareCode);
            Bind(footerInviteButton, ImportInviteCode);
            Bind(copyButton, CopyCode);
            Bind(shareButton, ShareCode);
            Bind(leaveButton, LeaveRoom);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>
        /// Builds the default Private Table layout and assigns serialized refs.
        /// Used by the editor prefab authoring menu and as a runtime fallback.
        /// </summary>
        public void BuildDefaultLayout()
        {
            if (GetComponent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 80;
            }

            if (GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // Clear previous authored children when rebuilding from the editor menu.
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            seatSlots.Clear();
            callbacksWired = false;

            root = CreateImage(transform, "Lobby Panel", MultiplayerUiArt.Background, false,
                Vector2.zero, Vector2.one);
            var bgImage = root.GetComponent<Image>();
            if (bgImage.sprite == null)
            {
                bgImage.color = new Color(0.06f, 0.07f, 0.08f, 0.96f);
            }

            CreateImage(root, "Logo", MultiplayerUiArt.Logo, false,
                new Vector2(0.22f, 0.86f), new Vector2(0.78f, 0.98f));

            BuildPrivateTableStrip(root);
            BuildTeams(root);

            readyButton = CreateArtButton(root, "Ready", "READY", MultiplayerUiArt.BtnGold, true,
                new Vector2(0.22f, 0.18f), new Vector2(0.78f, 0.28f));
            statusText = CreateText(root, "Status", "Match starts when all 4 players are ready…", 28, FontStyles.Normal,
                TextAlignmentOptions.Center, new Vector2(0.08f, 0.13f), new Vector2(0.92f, 0.18f));
            statusText.color = new Color(1f, 1f, 1f, 0.75f);

            BuildFooter(root);
        }

        private void BuildPrivateTableStrip(RectTransform parent)
        {
            var strip = CreateImage(parent, "Private Table", MultiplayerUiArt.InvitePanel, true,
                new Vector2(0.08f, 0.78f), new Vector2(0.70f, 0.86f));
            CreateText(strip, "Title", "PRIVATE TABLE", 22, FontStyles.Bold, TextAlignmentOptions.Top,
                new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.95f)).color = new Color(0.95f, 0.82f, 0.35f, 1f);
            codeText = CreateText(strip, "Code", "Invite Code: —", 32, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.05f), new Vector2(0.78f, 0.60f));

            headerCopyButton = CreateIconButton(strip, "HeaderCopy", MultiplayerUiArt.Copy,
                new Vector2(0.78f, 0.15f), new Vector2(0.96f, 0.85f));

            headerShareButton = CreateArtButton(parent, "HeaderShare", "SHARE", MultiplayerUiArt.BtnRed, true,
                new Vector2(0.72f, 0.78f), new Vector2(0.92f, 0.86f));
            var shareIcon = CreateImage(headerShareButton.transform, "Icon", MultiplayerUiArt.Share, false,
                new Vector2(0.08f, 0.20f), new Vector2(0.32f, 0.80f));
            shareIcon.GetComponent<Image>().raycastTarget = false;
        }

        private void BuildTeams(RectTransform parent)
        {
            CreateText(parent, "Team1Header", "TEAM 1", 28, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.72f), new Vector2(0.42f, 0.77f)).color = new Color(0.95f, 0.25f, 0.25f, 1f);
            CreateText(parent, "Team2Header", "TEAM 2", 28, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.58f, 0.72f), new Vector2(0.92f, 0.77f)).color = new Color(0.95f, 0.82f, 0.35f, 1f);

            seatSlots.Add(CreateSeatSlot(parent, SeatId.Bottom, MultiplayerUiArt.FrameRed,
                new Vector2(0.06f, 0.50f), new Vector2(0.26f, 0.72f)));
            seatSlots.Add(CreateSeatSlot(parent, SeatId.Top, MultiplayerUiArt.FrameRed,
                new Vector2(0.26f, 0.50f), new Vector2(0.46f, 0.72f)));
            CreateImage(parent, "VS", MultiplayerUiArt.Vs, false,
                new Vector2(0.46f, 0.55f), new Vector2(0.54f, 0.67f));
            seatSlots.Add(CreateSeatSlot(parent, SeatId.Left, MultiplayerUiArt.FrameGold,
                new Vector2(0.54f, 0.50f), new Vector2(0.74f, 0.72f)));
            seatSlots.Add(CreateSeatSlot(parent, SeatId.Right, MultiplayerUiArt.FrameGold,
                new Vector2(0.74f, 0.50f), new Vector2(0.94f, 0.72f)));
        }

        private SeatSlotRefs CreateSeatSlot(RectTransform parent, SeatId seat, string frameSprite, Vector2 min, Vector2 max)
        {
            var frame = CreateImage(parent, $"Seat_{seat}", frameSprite, false, min, max);
            var crown = CreateImage(frame, "Crown", MultiplayerUiArt.Crown, false,
                new Vector2(0.30f, 0.78f), new Vector2(0.70f, 0.98f));
            crown.GetComponent<Image>().enabled = false;

            var avatar = FindSeatAvatarImage(frame);
            if (avatar != null)
            {
                avatar.enabled = false;
            }

            var name = CreateText(frame, "Name", "Waiting…", 24, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.32f));

            var ready = CreateImage(frame, "Ready", MultiplayerUiArt.Check, false,
                new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.52f));
            ready.GetComponent<Image>().enabled = false;

            var badgeRoot = CreateImage(frame, "HostBadge", MultiplayerUiArt.BtnGoldThin, true,
                new Vector2(0.18f, 0.50f), new Vector2(0.82f, 0.66f));
            badgeRoot.GetComponent<Image>().raycastTarget = false;
            var badge = CreateText(badgeRoot, "Label", "HOST", 18, FontStyles.Bold, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f));
            badge.color = new Color(0.18f, 0.12f, 0.05f, 1f);
            badgeRoot.gameObject.SetActive(false);

            return new SeatSlotRefs
            {
                Seat = seat,
                NameText = name,
                ReadyIcon = ready.GetComponent<Image>(),
                CrownIcon = crown.GetComponent<Image>(),
                AvatarIcon = avatar,
                HostBadge = badge
            };
        }

        private void ResolveSeatSlotRefs()
        {
            for (var i = 0; i < seatSlots.Count; i++)
            {
                var slot = seatSlots[i];
                if (slot == null)
                {
                    continue;
                }

                var frame = ResolveSeatFrame(slot);
                if (slot.NameText == null && frame != null)
                {
                    var nameTransform = frame.Find("Name");
                    if (nameTransform != null)
                    {
                        slot.NameText = nameTransform.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (slot.AvatarIcon == null)
                {
                    slot.AvatarIcon = FindSeatAvatarImage(frame);
                }

                if (slot.AvatarIcon != null)
                {
                    // Keep the authored object active; visibility is toggled via Image.enabled like Crown.
                    slot.AvatarIcon.gameObject.SetActive(true);
                    slot.AvatarIcon.enabled = false;
                }
            }
        }

        private static Transform ResolveSeatFrame(SeatSlotRefs slot)
        {
            if (slot.CrownIcon != null)
            {
                return slot.CrownIcon.transform.parent;
            }

            if (slot.ReadyIcon != null)
            {
                return slot.ReadyIcon.transform.parent;
            }

            if (slot.NameText != null)
            {
                return slot.NameText.transform.parent;
            }

            return null;
        }

        private static Image FindSeatAvatarImage(Transform frame)
        {
            if (frame == null)
            {
                return null;
            }

            for (var i = 0; i < frame.childCount; i++)
            {
                var child = frame.GetChild(i);
                if (child == null || string.IsNullOrEmpty(child.name))
                {
                    continue;
                }

                if (child.name.StartsWith("avatar", System.StringComparison.OrdinalIgnoreCase) &&
                    child.TryGetComponent<Image>(out var image))
                {
                    return image;
                }
            }

            return null;
        }

        private void BuildFooter(RectTransform parent)
        {
            footerInviteButton = CreateFooterButton(parent, "FooterInvite", "INVITE CODE", MultiplayerUiArt.Key,
                MultiplayerUiArt.BtnDark, new Vector2(0.04f, 0.02f), new Vector2(0.26f, 0.12f));
            copyButton = CreateFooterButton(parent, "FooterCopy", "COPY CODE", MultiplayerUiArt.Clipboard,
                MultiplayerUiArt.BtnDark, new Vector2(0.28f, 0.02f), new Vector2(0.50f, 0.12f));
            shareButton = CreateFooterButton(parent, "FooterShare", "SHARE", MultiplayerUiArt.Share,
                MultiplayerUiArt.BtnDark, new Vector2(0.52f, 0.02f), new Vector2(0.74f, 0.12f));
            leaveButton = CreateFooterButton(parent, "FooterLeave", "LEAVE", MultiplayerUiArt.LeaveDoor,
                MultiplayerUiArt.BtnRed, new Vector2(0.76f, 0.02f), new Vector2(0.96f, 0.12f));
        }

        private Button CreateFooterButton(
            RectTransform parent,
            string name,
            string label,
            string iconSprite,
            string buttonSprite,
            Vector2 min,
            Vector2 max)
        {
            var button = CreateArtButton(parent, name, label, buttonSprite, true, min, max);
            var labelText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (labelText != null)
            {
                var labelRect = labelText.rectTransform;
                labelRect.anchorMin = new Vector2(0.04f, 0.04f);
                labelRect.anchorMax = new Vector2(0.96f, 0.36f);
                labelText.fontSize = 30f;
                labelText.enableAutoSizing = true;
                labelText.fontSizeMin = 18f;
                labelText.fontSizeMax = 34f;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.textWrappingMode = TextWrappingModes.NoWrap;
                labelText.overflowMode = TextOverflowModes.Overflow;
            }

            var icon = CreateImage(button.transform, "Icon", iconSprite, false,
                new Vector2(0.22f, 0.38f), new Vector2(0.78f, 0.94f));
            icon.GetComponent<Image>().raycastTarget = false;
            return button;
        }

        private static RectTransform CreateImage(
            Transform parent,
            string name,
            string spriteName,
            bool sliced,
            Vector2 min,
            Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            MultiplayerUiArt.Apply(image, spriteName, sliced);
            if (image.sprite == null)
            {
                image.color = new Color(0.12f, 0.12f, 0.14f, 0.85f);
            }

            return rect;
        }

        private static Button CreateIconButton(
            Transform parent,
            string name,
            string spriteName,
            Vector2 min,
            Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            MultiplayerUiArt.Apply(image, spriteName, false);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Button CreateArtButton(
            Transform parent,
            string name,
            string label,
            string spriteName,
            bool sliced,
            Vector2 min,
            Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            MultiplayerUiArt.Apply(image, spriteName, sliced);
            if (image.sprite == null)
            {
                image.color = new Color(0.18f, 0.42f, 0.28f, 1f);
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var labelText = CreateText(go.transform, "Label", label, 36, FontStyles.Bold, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one);
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 22f;
            labelText.fontSizeMax = 44f;
            return button;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string value,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment,
            Vector2 min,
            Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<TextMeshProUGUI>();
            var font = ResolveFont();
            if (font != null)
            {
                text.font = font;
            }

            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(14f, size * 0.55f);
            text.fontSizeMax = Mathf.Max(size, 36f);
            return text;
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = TMP_Settings.defaultFontAsset;
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            return cachedFont;
        }
    }
}
