using System;
using System.Collections.Generic;
using System.IO;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesNetworkSession : MonoBehaviour
    {
        private const string LocalPlayerIdPrefsKey = "BackyardLegends.LocalPlayerId";
        private const string TableIdPrefsKey = "BackyardLegends.TableId";
        private const string SessionKeyPrefsKey = "BackyardLegends.SessionKey";
        private const string HostUidPrefsKey = "BackyardLegends.LastHostUid";

        public static SpadesNetworkSession Instance { get; private set; }

        // Survives accidental session GameObject recreation across LoadScene.
        private static SpadesNetworkRole pendingRole = SpadesNetworkRole.Offline;
        private static string pendingJoinCode = string.Empty;
        private static RuleSetDefinition pendingRules;

        public SpadesNetworkRole Role { get; private set; } = SpadesNetworkRole.Offline;
        public bool IsOnline => Role != SpadesNetworkRole.Offline;
        public string JoinCode { get; private set; } = string.Empty;
        public string StatusMessage { get; private set; } = string.Empty;
        public SeatId LocalLogicalSeat { get; private set; } = SeatId.Bottom;
        public bool HasLocalSeat { get; private set; }
        public bool SeatAssigned { get; private set; }
        public bool UseDirectTransport { get; private set; } = true;
        public string DirectAddress { get; private set; } = "127.0.0.1";
        public ushort DirectPort { get; private set; } = 7777;
        public RuleSetDefinition PendingRules { get; private set; }
        public string LocalPlayerId { get; private set; } = string.Empty;
        public string LocalDisplayName { get; private set; } = "You";
        public bool MatchWasLive { get; set; }
        public bool AutoReconnectEnabled { get; set; }
        public string TableId { get; private set; } = string.Empty;
        public string SessionKey { get; private set; } = string.Empty;
        public string LastKnownHostUid { get; private set; } = string.Empty;
        public MatchState PendingRestoreState { get; private set; }
        public int PendingRestoreActionSeq { get; private set; }
        public Dictionary<SeatId, (string Uid, string DisplayName)> PendingSeatRoster { get; private set; }

        public event Action StateChanged;

        public static SpadesNetworkSession GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<SpadesNetworkSession>();
            if (existing != null)
            {
                existing.EnsureInitialized();
                return existing;
            }

            var go = new GameObject("Spades Network Session");
            return go.AddComponent<SpadesNetworkSession>();
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureLocalPlayerId();
            RestorePendingOnlineIntent();
        }

        public void RestorePendingOnlineIntent()
        {
            if (Role != SpadesNetworkRole.Offline)
            {
                return;
            }

            if (pendingRole == SpadesNetworkRole.Host)
            {
                BeginHost(pendingRules);
                return;
            }

            if (pendingRole == SpadesNetworkRole.Client && !string.IsNullOrWhiteSpace(pendingJoinCode))
            {
                BeginClient(pendingJoinCode, pendingRules);
            }
        }

        public void ConfigureOffline()
        {
            pendingRole = SpadesNetworkRole.Offline;
            pendingJoinCode = string.Empty;
            pendingRules = null;
            Role = SpadesNetworkRole.Offline;
            JoinCode = string.Empty;
            StatusMessage = string.Empty;
            HasLocalSeat = false;
            SeatAssigned = false;
            LocalLogicalSeat = SeatId.Bottom;
            PendingRules = null;
            MatchWasLive = false;
            AutoReconnectEnabled = false;
            TableId = string.Empty;
            SessionKey = string.Empty;
            LastKnownHostUid = string.Empty;
            PendingRestoreState = null;
            PendingRestoreActionSeq = 0;
            PendingSeatRoster = null;
            PlayerPrefs.DeleteKey(TableIdPrefsKey);
            PlayerPrefs.DeleteKey(SessionKeyPrefsKey);
            PlayerPrefs.DeleteKey(HostUidPrefsKey);
            RaiseChanged();
        }

        public void BeginHost(RuleSetDefinition rules)
        {
            pendingRole = SpadesNetworkRole.Host;
            pendingJoinCode = string.Empty;
            pendingRules = rules;
            Role = SpadesNetworkRole.Host;
            PendingRules = rules;
            HasLocalSeat = false;
            SeatAssigned = false;
            LocalLogicalSeat = SeatId.Bottom;
            MatchWasLive = false;
            AutoReconnectEnabled = false;
            StatusMessage = "Hosting table…";
            RaiseChanged();
        }

        public void BeginClient(string joinCode, RuleSetDefinition rules)
        {
            pendingRole = SpadesNetworkRole.Client;
            pendingJoinCode = joinCode?.Trim().ToUpperInvariant() ?? string.Empty;
            pendingRules = rules;
            Role = SpadesNetworkRole.Client;
            JoinCode = pendingJoinCode;
            PendingRules = rules;
            HasLocalSeat = false;
            SeatAssigned = false;
            MatchWasLive = false;
            AutoReconnectEnabled = true;
            StatusMessage = "Joining table…";
            RaiseChanged();
        }

        public void SetConnectionInfo(string joinCode, bool useDirect, string address, ushort port)
        {
            JoinCode = joinCode ?? string.Empty;
            UseDirectTransport = useDirect;
            DirectAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            DirectPort = port;
            RaiseChanged();
        }

        public void SetStatus(string message)
        {
            StatusMessage = message ?? string.Empty;
            RaiseChanged();
        }

        public void AssignLocalSeat(SeatId seat)
        {
            LocalLogicalSeat = seat;
            HasLocalSeat = true;
            SeatAssigned = true;
            StatusMessage = $"Seated {seat}";
            RaiseChanged();
        }

        public void MarkMatchLive()
        {
            MatchWasLive = true;
            if (Role == SpadesNetworkRole.Client)
            {
                AutoReconnectEnabled = true;
            }

            RaiseChanged();
        }

        public void SetTableSession(string tableId, string sessionKey, string hostUid)
        {
            TableId = tableId ?? string.Empty;
            SessionKey = sessionKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(hostUid))
            {
                LastKnownHostUid = hostUid.Trim();
            }

            if (!string.IsNullOrEmpty(TableId))
            {
                PlayerPrefs.SetString(TableIdPrefsKey, TableId);
            }

            if (!string.IsNullOrEmpty(SessionKey))
            {
                PlayerPrefs.SetString(SessionKeyPrefsKey, SessionKey);
            }

            if (!string.IsNullOrEmpty(LastKnownHostUid))
            {
                PlayerPrefs.SetString(HostUidPrefsKey, LastKnownHostUid);
            }

            PlayerPrefs.Save();
            RaiseChanged();
        }

        public void SetPendingRestoreState(MatchState state, int actionSeq)
        {
            PendingRestoreState = state;
            PendingRestoreActionSeq = actionSeq;
        }

        public void SetPendingSeatRoster(Dictionary<SeatId, (string Uid, string DisplayName)> roster)
        {
            PendingSeatRoster = roster;
        }

        public Dictionary<SeatId, (string Uid, string DisplayName)> ConsumePendingSeatRoster()
        {
            var roster = PendingSeatRoster;
            PendingSeatRoster = null;
            return roster;
        }

        public MatchState ConsumePendingRestoreState(out int actionSeq)
        {
            actionSeq = PendingRestoreActionSeq;
            var state = PendingRestoreState;
            PendingRestoreState = null;
            PendingRestoreActionSeq = 0;
            return state;
        }

        public void RestorePersistedTableIds()
        {
            if (string.IsNullOrEmpty(TableId))
            {
                TableId = PlayerPrefs.GetString(TableIdPrefsKey, string.Empty);
            }

            if (string.IsNullOrEmpty(SessionKey))
            {
                SessionKey = PlayerPrefs.GetString(SessionKeyPrefsKey, string.Empty);
            }

            if (string.IsNullOrEmpty(LastKnownHostUid))
            {
                LastKnownHostUid = PlayerPrefs.GetString(HostUidPrefsKey, string.Empty);
            }
        }

        public void DisableAutoReconnect()
        {
            AutoReconnectEnabled = false;
            RaiseChanged();
        }

        public void SetLocalPlayerIdentity(string uid, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(uid))
            {
                LocalPlayerId = uid.Trim();
                PlayerPrefs.SetString(LocalPlayerIdPrefsKey, LocalPlayerId);
                PlayerPrefs.Save();
            }
            else
            {
                EnsureLocalPlayerId();
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                LocalDisplayName = displayName.Trim();
            }

            RaiseChanged();
        }

        public void EnsureLocalPlayerId()
        {
            if (!string.IsNullOrWhiteSpace(LocalPlayerId))
            {
                return;
            }

            var stored = PlayerPrefs.GetString(LocalPlayerIdPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                LocalPlayerId = stored;
#if UNITY_EDITOR
                // ParrelSync clones can share PlayerPrefs; suffix so host/clone are distinct seats.
                var cloneSuffix = ResolveEditorCloneSuffix();
                if (!string.IsNullOrEmpty(cloneSuffix) &&
                    !LocalPlayerId.EndsWith(cloneSuffix, StringComparison.Ordinal))
                {
                    LocalPlayerId = stored + cloneSuffix;
                    PlayerPrefs.SetString(LocalPlayerIdPrefsKey, LocalPlayerId);
                    PlayerPrefs.Save();
                }
#endif
                return;
            }

            LocalPlayerId = "local-" + Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
            LocalPlayerId += ResolveEditorCloneSuffix();
#endif
            PlayerPrefs.SetString(LocalPlayerIdPrefsKey, LocalPlayerId);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public static string ApplyEditorCloneUidSuffix(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return uid;
            }

            var suffix = ResolveEditorCloneSuffix();
            if (string.IsNullOrEmpty(suffix) || uid.EndsWith(suffix, StringComparison.Ordinal))
            {
                return uid;
            }

            return uid.Trim() + suffix;
        }

        private static string ResolveEditorCloneSuffix()
        {
            try
            {
                var dataPath = Application.dataPath ?? string.Empty;
                if (dataPath.IndexOf("_clone_", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    var args = Environment.GetCommandLineArgs();
                    var looksLikeClone = false;
                    for (var i = 0; i < args.Length; i++)
                    {
                        if (args[i] != null &&
                            (args[i].IndexOf("clone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             string.Equals(args[i], "-cloneArg", StringComparison.OrdinalIgnoreCase)))
                        {
                            looksLikeClone = true;
                            break;
                        }
                    }

                    if (!looksLikeClone)
                    {
                        return string.Empty;
                    }
                }

                var folder = new DirectoryInfo(Application.dataPath).Parent?.Name ?? "clone";
                return "#clone-" + folder;
            }
            catch
            {
                return "#clone";
            }
        }
#endif

        private void RaiseChanged()
        {
            StateChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
