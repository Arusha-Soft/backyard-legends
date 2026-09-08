using System;
using BackyardLegends.Core;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesNetworkSession : MonoBehaviour
    {
        public static SpadesNetworkSession Instance { get; private set; }

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
        }

        public void ConfigureOffline()
        {
            Role = SpadesNetworkRole.Offline;
            JoinCode = string.Empty;
            StatusMessage = string.Empty;
            HasLocalSeat = false;
            SeatAssigned = false;
            LocalLogicalSeat = SeatId.Bottom;
            PendingRules = null;
            RaiseChanged();
        }

        public void BeginHost(RuleSetDefinition rules)
        {
            Role = SpadesNetworkRole.Host;
            PendingRules = rules;
            HasLocalSeat = false;
            SeatAssigned = false;
            LocalLogicalSeat = SeatId.Bottom;
            StatusMessage = "Hosting table…";
            RaiseChanged();
        }

        public void BeginClient(string joinCode, RuleSetDefinition rules)
        {
            Role = SpadesNetworkRole.Client;
            JoinCode = joinCode?.Trim().ToUpperInvariant() ?? string.Empty;
            PendingRules = rules;
            HasLocalSeat = false;
            SeatAssigned = false;
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
