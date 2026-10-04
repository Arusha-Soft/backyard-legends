using System.Text;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Network;
using UnityEngine;
using UnityEngine.UI;

namespace BackyardLegends.Runtime
{
    /// <summary>
    /// Pre-match waiting lobby overlay: seats, teams, ready, invite code, leave.
    /// </summary>
    public sealed class MultiplayerLobbyHud : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform root;
        private Text rosterText;
        private Text statusText;
        private Text codeText;
        private Button readyButton;
        private Button copyButton;
        private Button shareButton;
        private Button leaveButton;
        private Button startButton;
        private bool localReady;
        private string lastRosterMessage = string.Empty;

        public static MultiplayerLobbyHud Ensure()
        {
            var existing = FindFirstObjectByType<MultiplayerLobbyHud>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("Multiplayer Lobby HUD");
            DontDestroyOnLoad(go);
            return go.AddComponent<MultiplayerLobbyHud>();
        }

        private void Awake()
        {
            BuildUi();
        }

        private void OnEnable()
        {
            var table = SpadesTableNetwork.Instance;
            if (table != null)
            {
                table.LobbyStateChanged += Refresh;
                table.NetworkEventReceived += HandleNetworkEvent;
                table.MatchBound += Hide;
            }

            var session = SpadesNetworkSession.GetOrCreate();
            session.StateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            var table = SpadesTableNetwork.Instance;
            if (table != null)
            {
                table.LobbyStateChanged -= Refresh;
                table.NetworkEventReceived -= HandleNetworkEvent;
                table.MatchBound -= Hide;
            }

            if (SpadesNetworkSession.Instance != null)
            {
                SpadesNetworkSession.Instance.StateChanged -= Refresh;
            }
        }

        private void Update()
        {
            // Table may spawn after this HUD — rebind once.
            if (SpadesTableNetwork.Instance != null && root != null && root.gameObject.activeSelf)
            {
                Refresh();
            }
        }

        private void HandleNetworkEvent(SpadesNetworkEventPayload payload)
        {
            if (payload.Kind == (byte)SpadesNetworkEventKind.LobbyRoster)
            {
                lastRosterMessage = payload.Message ?? string.Empty;
            }

            if (payload.Kind == (byte)SpadesNetworkEventKind.TableReady ||
                payload.Kind == (byte)SpadesNetworkEventKind.MatchStarted)
            {
                Hide();
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            var session = SpadesNetworkSession.GetOrCreate();
            var table = SpadesTableNetwork.Instance;
            var show = session.IsOnline && (table == null || !table.MatchStarted);
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
                    ? "Invite code: —"
                    : $"Invite code: {session.JoinCode}";
            }

            if (statusText != null)
            {
                statusText.text = string.IsNullOrEmpty(session.StatusMessage)
                    ? "Waiting in lobby…"
                    : session.StatusMessage;
            }

            if (rosterText != null)
            {
                rosterText.text = BuildRosterLabel(table);
            }

            if (readyButton != null)
            {
                var label = readyButton.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = localReady ? "UNREADY" : "READY";
                }
            }

            if (startButton != null)
            {
                var isHost = session.Role == SpadesNetworkRole.Host;
                startButton.gameObject.SetActive(isHost);
                startButton.interactable = table != null && table.OccupiedCount >= 4 && table.ReadyCount >= 4;
            }
        }

        private string BuildRosterLabel(SpadesTableNetwork table)
        {
            if (!string.IsNullOrEmpty(lastRosterMessage))
            {
                var sb = new StringBuilder();
                sb.AppendLine("SEATS  (Home = Bottom+Top · Away = Left+Right)");
                foreach (var part in lastRosterMessage.Split('|'))
                {
                    var bits = part.Split(':');
                    if (bits.Length < 4)
                    {
                        continue;
                    }

                    var readyMark = bits[2] == "R" ? "[READY]" : "[----]";
                    sb.AppendLine($"{bits[0],-6} {readyMark}  {bits[1]}  ({bits[3]})");
                }

                return sb.ToString();
            }

            if (table == null)
            {
                return "Connecting to table…";
            }

            var builder = new StringBuilder();
            builder.AppendLine("SEATS  (Home = Bottom+Top · Away = Left+Right)");
            foreach (var seat in new[] { SeatId.Bottom, SeatId.Top, SeatId.Left, SeatId.Right })
            {
                var name = table.DisplayNames.TryGetValue(seat, out var n) ? n : "— empty —";
                var ready = table.IsSeatReady(seat) ? "[READY]" : "[----]";
                var team = seat == SeatId.Bottom || seat == SeatId.Top ? "Home" : "Away";
                builder.AppendLine($"{seat,-6} {ready}  {name}  ({team})");
            }

            return builder.ToString();
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

        private void HostStart()
        {
            SpadesTableNetwork.Instance?.HostRequestStartMatch();
        }

        private void BuildUi()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            root = CreatePanel(transform, "Lobby Panel", new Color(0.06f, 0.07f, 0.08f, 0.92f),
                new Vector2(0.06f, 0.18f), new Vector2(0.94f, 0.88f));

            codeText = CreateText(root, "Code", "Invite code: —", 28, FontStyle.Bold, TextAnchor.UpperCenter,
                new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.96f));
            statusText = CreateText(root, "Status", "Waiting in lobby…", 20, FontStyle.Normal, TextAnchor.UpperCenter,
                new Vector2(0.06f, 0.72f), new Vector2(0.94f, 0.82f));
            rosterText = CreateText(root, "Roster", "SEATS", 18, FontStyle.Normal, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.72f));

            readyButton = CreateButton(root, "Ready", "READY", new Vector2(0.08f, 0.06f), new Vector2(0.30f, 0.18f), ToggleReady);
            copyButton = CreateButton(root, "Copy", "COPY CODE", new Vector2(0.32f, 0.06f), new Vector2(0.52f, 0.18f), CopyCode);
            shareButton = CreateButton(root, "Share", "SHARE", new Vector2(0.54f, 0.06f), new Vector2(0.70f, 0.18f), ShareCode);
            leaveButton = CreateButton(root, "Leave", "LEAVE", new Vector2(0.72f, 0.06f), new Vector2(0.92f, 0.18f), LeaveRoom);
            startButton = CreateButton(root, "Start", "START", new Vector2(0.32f, 0.18f), new Vector2(0.68f, 0.28f), HostStart);
            startButton.gameObject.SetActive(false);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int size,
            FontStyle style,
            TextAnchor anchor,
            Vector2 min,
            Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 min,
            Vector2 max,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.28f, 1f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            CreateText(go.transform, "Label", label, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one);
            return button;
        }
    }
}
