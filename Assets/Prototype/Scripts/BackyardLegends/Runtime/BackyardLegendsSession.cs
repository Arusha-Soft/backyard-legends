using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Firebase;
using BackyardLegends.Runtime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackyardLegends.Runtime
{
    public sealed class BackyardLegendsSession : MonoBehaviour
    {
        private const string ThemeResourcePath = "BackyardLegends/Theme_Default";
        private const string ClassicResourcePath = "BackyardLegends/Rules_Classic";
        private const string StreetResourcePath = "BackyardLegends/Rules_Street";

        [SerializeField] private ThemeConfig themeOverride;
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string gameplaySceneName = "GameplayScene";

        private RuleSetConfig[] ruleConfigs;
        private Task authTask;

        public static BackyardLegendsSession Instance { get; private set; }

        public ThemeConfig Theme { get; private set; }
        public int SelectedModeIndex { get; private set; }
        public int SelectedTargetScore { get; private set; } = 100;
        public IReadOnlyList<RuleSetConfig> RuleConfigs => ruleConfigs;
        public AuthUserSnapshot CurrentUser { get; private set; } = AuthUserSnapshot.None;
        public bool IsAuthReady { get; private set; }
        /// <summary>Lobby auth wall dismissed (guest / signed-in). Survives lobby reloads.</summary>
        public bool AuthGatePassed { get; private set; }
        public string AuthStatusMessage { get; private set; } = "Signing in…";

        public RuleSetDefinition SelectedRule => GetSelectedRuleDefinition();

        private void Awake()
        {
            Initialize();
        }

        public static BackyardLegendsSession GetOrCreateRuntimeInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<BackyardLegendsSession>();
            if (existing != null)
            {
                existing.Initialize();
                return existing;
            }

            var sessionObject = new GameObject("Backyard Legends Session");
            var session = sessionObject.AddComponent<BackyardLegendsSession>();
            session.Initialize();
            return session;
        }

        public string[] GetModeLabels()
        {
            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                return new[] { "Classic", "Street" };
            }

            return ruleConfigs.Select(config => config != null ? config.DisplayName : "Mode").ToArray();
        }

        public int[] GetTargetOptions()
        {
            var config = GetSelectedConfig();
            if (config != null && config.TargetScoreOptions != null && config.TargetScoreOptions.Length > 0)
            {
                return config.TargetScoreOptions.ToArray();
            }

            return new[] { 100, 200, 500 };
        }

        public void SelectMode(int modeIndex)
        {
            SelectedModeIndex = Mathf.Max(0, modeIndex);
            ClampSelections();
        }

        public void SelectTarget(int targetScore)
        {
            SelectedTargetScore = targetScore;
            ClampSelections();
        }

        public void LoadGameplayScene()
        {
            SpadesNetworkSession.GetOrCreate().ConfigureOffline();
            LoadSceneOrThrow(gameplaySceneName);
        }

        public async Task HostOnlineTableAsync()
        {
            await EnsureAuthReadyForOnlineAsync();
            var networkSession = SpadesNetworkSession.GetOrCreate();
            networkSession.BeginHost(SelectedRule, privateRoom: true, matchmade: false);
            ApplyLocalNetworkIdentity(networkSession);
            var host = SpadesNetworkManagerHost.GetOrCreate();
            var started = await host.StartHostAsync();
            if (!started)
            {
                var message = string.IsNullOrWhiteSpace(networkSession.StatusMessage)
                    ? "Failed to host table."
                    : networkSession.StatusMessage;
                networkSession.ConfigureOffline();
                throw new InvalidOperationException(message);
            }

            LoadSceneOrThrow(gameplaySceneName);
        }

        public async Task JoinOnlineTableAsync(string joinCode)
        {
            await EnsureAuthReadyForOnlineAsync();
            var code = string.IsNullOrWhiteSpace(joinCode)
                ? string.Empty
                : joinCode.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Enter an invite code to join.");
            }

            // Resolve Firestore table first when available (fresh Relay code + table id).
            TableSessionRecord table = null;
            if (TableSessionService.IsAvailable || FirebaseBootstrap.IsAvailable)
            {
                await FirebaseBootstrap.EnsureInitializedAsync();
                table = await TableSessionService.FindByJoinCodeAsync(code);
                if (table != null)
                {
                    if (string.Equals(table.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(table.Status, "abandoned", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("That room is closed. Ask the host for a new code.");
                    }

                    if (!string.IsNullOrWhiteSpace(table.JoinCode))
                    {
                        code = table.JoinCode;
                    }
                }
            }

            var networkSession = SpadesNetworkSession.GetOrCreate();
            networkSession.BeginClient(code, SelectedRule, matchmade: false);
            ApplyLocalNetworkIdentity(networkSession);
            if (table != null)
            {
                networkSession.SetTableSession(table.TableId, table.SessionKey, table.HostUid);
            }

            networkSession.SetStatus($"Joining {code}…");
            Debug.Log($"Join → load gameplay then connect code={code} role={networkSession.Role}");
            LoadSceneOrThrow(gameplaySceneName);
        }

        public async Task QuickMatchAsync()
        {
            await EnsureAuthReadyForOnlineAsync();
            ApplyLocalNetworkIdentity(SpadesNetworkSession.GetOrCreate());
            var networkSession = SpadesNetworkSession.GetOrCreate();
            // Firestore rules key matchQueue/{uid} by Firebase Auth uid.
            // Editor clone suffixes are for NGO seats only and must not be used here.
            var authUid = ResolveFirebaseAuthUid();
            if (string.IsNullOrWhiteSpace(authUid))
            {
                throw new InvalidOperationException("Sign in required for Quick Match.");
            }

            networkSession.SetStatus("Queuing for Quick Match…");
            await MatchmakingService.QueueForMatchAsync(authUid, networkSession.LocalDisplayName, SelectedRule);
            var mode = SelectedRule?.DisplayName ?? "Classic";
            var target = SelectedRule?.TargetScore ?? 100;
            var bucketId = $"{mode}_{target}_auto".Replace(" ", string.Empty);

            // Poll until matched (up to ~60s). Re-run matcher without resetting tickets.
            var timeoutAt = Time.realtimeSinceStartup + 60f;
            MatchmakingService.QueueTicket ticket = null;
            while (Time.realtimeSinceStartup < timeoutAt)
            {
                ticket = await MatchmakingService.GetTicketAsync(authUid);
                if (ticket != null && string.Equals(ticket.Status, "matched", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (ticket != null && string.Equals(ticket.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Quick Match cancelled.");
                }

                // Only the eventual host can create the table under current rules
                // (hostUid must equal request.auth.uid). Non-hosts keep polling.
                await MatchmakingService.TryFormMatchForBucketAsync(bucketId, mode, target, SelectedRule);
                await Task.Delay(750);
            }

            ticket = ticket ?? await MatchmakingService.GetTicketAsync(authUid);
            if (ticket == null || !string.Equals(ticket.Status, "matched", StringComparison.OrdinalIgnoreCase))
            {
                await MatchmakingService.CancelQueueAsync(authUid);
                throw new InvalidOperationException("Quick Match timed out — try again or create a private room.");
            }

            var table = await TableSessionService.GetTableAsync(ticket.TableId);
            if (table == null)
            {
                throw new InvalidOperationException("Matched table missing.");
            }

            if (ticket.IsHost)
            {
                networkSession.BeginHost(SelectedRule, privateRoom: false, matchmade: true);
                ApplyLocalNetworkIdentity(networkSession);
                networkSession.SetTableSession(table.TableId, table.SessionKey, authUid);
                networkSession.SetStatus("Matched — starting host…");
                var host = SpadesNetworkManagerHost.GetOrCreate();
                var started = await host.StartHostAsync();
                if (!started)
                {
                    await MatchmakingService.CancelQueueAsync(authUid);
                    throw new InvalidOperationException(networkSession.StatusMessage);
                }

                // StartHostAsync already wrote joinCode onto the matchmade table.
                // Leave LobbyScene immediately so the host is not stuck on "searching"
                // while clients join and ready. Ticket joinCode copies are best-effort.
                LoadSceneOrThrow(gameplaySceneName);
                try
                {
                    await PublishMatchJoinCodeAsync(ticket.TableId, networkSession.JoinCode);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Quick Match ticket joinCode publish skipped: {ex.Message}");
                }

                return;
            }

            // Non-host: wait briefly for host to publish join code on the table.
            networkSession.SetStatus("Matched — waiting for host…");
            var code = table.JoinCode;
            var waitUntil = Time.realtimeSinceStartup + 25f;
            while (string.IsNullOrWhiteSpace(code) && Time.realtimeSinceStartup < waitUntil)
            {
                await Task.Delay(500);
                table = await TableSessionService.GetTableAsync(ticket.TableId);
                code = table?.JoinCode;
                var refreshed = await MatchmakingService.GetTicketAsync(authUid);
                if (refreshed != null && !string.IsNullOrWhiteSpace(refreshed.JoinCode))
                {
                    code = refreshed.JoinCode;
                }
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Host did not publish a Relay code in time.");
            }

            networkSession.BeginClient(code, SelectedRule, matchmade: true);
            ApplyLocalNetworkIdentity(networkSession);
            networkSession.SetTableSession(table.TableId, table.SessionKey, table.HostUid);
            LoadSceneOrThrow(gameplaySceneName);
        }

        public async Task CancelQuickMatchAsync()
        {
            var uid = ResolveFirebaseAuthUid();
            if (string.IsNullOrWhiteSpace(uid))
            {
                uid = SpadesNetworkSession.GetOrCreate().LocalPlayerId;
            }
            if (string.IsNullOrWhiteSpace(uid) && CurrentUser != null)
            {
                uid = CurrentUser.Uid;
            }

            await MatchmakingService.CancelQueueAsync(uid);
            SpadesNetworkSession.GetOrCreate().SetStatus("Quick Match cancelled.");
        }

        private static async Task PublishMatchJoinCodeAsync(string tableId, string joinCode)
        {
            if (!TableSessionService.IsAvailable || string.IsNullOrWhiteSpace(tableId))
            {
                return;
            }

            var db = FirebaseBootstrap.GetFirestore();
            var table = await TableSessionService.GetTableAsync(tableId);
            if (table?.Seats == null)
            {
                return;
            }

            foreach (var pair in table.Seats)
            {
                var seatUid = pair.Value?.Uid;
                if (string.IsNullOrWhiteSpace(seatUid))
                {
                    continue;
                }

                await db.Collection(MatchmakingService.QueueCollection).Document(seatUid).UpdateAsync(
                    new Dictionary<string, object>
                    {
                        ["joinCode"] = (joinCode ?? string.Empty).Trim().ToUpperInvariant(),
                        ["updatedAt"] = global::Firebase.Firestore.FieldValue.ServerTimestamp
                    });
            }
        }

        public string GameplaySceneName => gameplaySceneName;

        private async Task EnsureAuthReadyForOnlineAsync()
        {
            await WaitForAuthAsync();
            var auth = FirebaseAuthService.GetOrCreate();
            var user = await auth.EnsureSignedInAsync();
            ApplyAuthUser(user, auth.LastError);
        }

        private string ResolveFirebaseAuthUid()
        {
            try
            {
                var authUser = FirebaseBootstrap.GetAuth()?.CurrentUser;
                if (authUser != null && !string.IsNullOrWhiteSpace(authUser.UserId))
                {
                    return authUser.UserId;
                }
            }
            catch
            {
                // Firebase may be unavailable in editor without config.
            }

            if (CurrentUser != null && CurrentUser.IsSignedIn && !string.IsNullOrWhiteSpace(CurrentUser.Uid))
            {
                return CurrentUser.Uid;
            }

            return string.Empty;
        }

        private void ApplyLocalNetworkIdentity(SpadesNetworkSession networkSession)
        {
            networkSession.EnsureLocalPlayerId();
            var firebaseUid = ResolveFirebaseAuthUid();

            var uid = !string.IsNullOrWhiteSpace(firebaseUid)
                ? firebaseUid
                : networkSession.LocalPlayerId;
#if UNITY_EDITOR
            uid = SpadesNetworkSession.ApplyEditorCloneUidSuffix(uid);
#endif
            var displayName = CurrentUser != null && !string.IsNullOrWhiteSpace(CurrentUser.DisplayName)
                ? CurrentUser.DisplayName
                : "You";
            networkSession.SetLocalPlayerIdentity(uid, displayName);
        }

        public void LoadLobbyScene()
        {
            if (SpadesNetworkManagerHost.Instance != null)
            {
                SpadesNetworkManagerHost.Instance.Shutdown();
            }
            else
            {
                SpadesNetworkSession.GetOrCreate().ConfigureOffline();
            }

            LoadSceneOrThrow(lobbySceneName);
        }

        /// <summary>
        /// Unity 6 + Multiplayer Play Mode can fail LoadScene(name) if the virtual player's
        /// build-profile scene list is stale. Prefer name, then fall back to build index.
        /// </summary>
        private static void LoadSceneOrThrow(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new InvalidOperationException("Scene name is empty.");
            }

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            var count = SceneManager.sceneCountInBuildSettings;
            for (var i = 0; i < count; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(file, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    SceneManager.LoadScene(i);
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Scene '{sceneName}' is not in the active Build Profile / shared scene list " +
                $"(build scenes={count}). Open File → Build Profiles, add LobbyScene + GameplayScene, " +
                "then disable and re-enable Multiplayer Play Mode virtual players.");
        }

        public Task WaitForAuthAsync()
        {
            return authTask ?? Task.CompletedTask;
        }

        private void Initialize()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (Theme == null)
            {
                Theme = themeOverride != null
                    ? themeOverride
                    : Resources.Load<ThemeConfig>(ThemeResourcePath) ?? ThemeConfig.CreateFallback();
            }

            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                ruleConfigs = LoadRuleConfigs();
            }

            ClampSelections();
            BeginAuth();
        }

        private void BeginAuth()
        {
            if (authTask != null)
            {
                return;
            }

            authTask = EnsureAuthAsync();
        }

        private async Task EnsureAuthAsync()
        {
            IsAuthReady = false;
            AuthStatusMessage = "Signing in…";

            var auth = FirebaseAuthService.GetOrCreate();
            auth.StateChanged -= HandleAuthStateChanged;
            auth.StateChanged += HandleAuthStateChanged;

            var user = await auth.EnsureSignedInAsync();
            ApplyAuthUser(user, auth.LastError);
        }

        private void HandleAuthStateChanged(AuthUserSnapshot snapshot)
        {
            ApplyAuthUser(snapshot, FirebaseAuthService.Instance != null ? FirebaseAuthService.Instance.LastError : string.Empty);
        }

        private void ApplyAuthUser(AuthUserSnapshot snapshot, string error)
        {
            CurrentUser = snapshot ?? AuthUserSnapshot.None;
            IsAuthReady = true;
            if (!CurrentUser.IsSignedIn)
            {
                AuthStatusMessage = string.IsNullOrEmpty(error)
                    ? "Offline guest (Firebase unavailable)"
                    : $"Auth unavailable · {error}";
                // Firebase down still allows local/ParrelSync play past the gate.
                if (string.IsNullOrEmpty(error) || error.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    AuthGatePassed = true;
                }
            }
            else
            {
                AuthStatusMessage = CurrentUser.StatusLabel;
                // Anonymous guest from EnsureSignedIn counts as past the account wall.
                AuthGatePassed = true;
            }
        }

        public void DismissAuthGate()
        {
            AuthGatePassed = true;
        }

        public void ClearAuthGate()
        {
            AuthGatePassed = false;
        }

        private void ClampSelections()
        {
            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                SelectedModeIndex = Mathf.Clamp(SelectedModeIndex, 0, 1);
                return;
            }

            SelectedModeIndex = Mathf.Clamp(SelectedModeIndex, 0, ruleConfigs.Length - 1);
            var targets = GetTargetOptions();
            if (targets.Length == 0)
            {
                SelectedTargetScore = 100;
                return;
            }

            if (!targets.Contains(SelectedTargetScore))
            {
                SelectedTargetScore = targets[0];
            }
        }

        private RuleSetDefinition GetSelectedRuleDefinition()
        {
            var config = GetSelectedConfig();
            if (config == null)
            {
                return SelectedModeIndex == 0
                    ? RuleSetConfig.CreateClassic(SelectedTargetScore)
                    : RuleSetConfig.CreateStreet(SelectedTargetScore);
            }

            var definition = config.ToDefinition(SelectedTargetScore);
            if (config.DisplayName.Contains("Street"))
            {
                definition.DisplayName = "Street";
                definition.AllowSpadesAnytime = true;
                definition.SpadesMustBeBroken = false;
                definition.FollowSuitRequired = true;
                definition.RenegePenaltyEnabled = true;
            }
            else
            {
                definition.DisplayName = "Classic";
                definition.AllowSpadesAnytime = false;
                definition.SpadesMustBeBroken = true;
                definition.FollowSuitRequired = true;
                definition.RenegePenaltyEnabled = false;
            }

            return definition;
        }

        private RuleSetConfig GetSelectedConfig()
        {
            if (ruleConfigs == null || ruleConfigs.Length == 0)
            {
                return null;
            }

            return ruleConfigs[Mathf.Clamp(SelectedModeIndex, 0, ruleConfigs.Length - 1)];
        }

        private static RuleSetConfig[] LoadRuleConfigs()
        {
            var configs = new List<RuleSetConfig>();
            var classic = Resources.Load<RuleSetConfig>(ClassicResourcePath);
            var street = Resources.Load<RuleSetConfig>(StreetResourcePath);
            if (classic != null)
            {
                configs.Add(classic);
            }

            if (street != null)
            {
                configs.Add(street);
            }

            return configs.ToArray();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (FirebaseAuthService.Instance != null)
                {
                    FirebaseAuthService.Instance.StateChanged -= HandleAuthStateChanged;
                }

                Instance = null;
            }
        }
    }
}
