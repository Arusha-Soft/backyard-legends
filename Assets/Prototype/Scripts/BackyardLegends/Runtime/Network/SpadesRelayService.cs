using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesRelayService
    {
        public const ushort DefaultDirectPort = 7777;
        public const string LocalJoinCode = "LOCAL";

        public async Task<(bool useDirect, string joinCode, string error)> HostAsync(UnityTransport transport, int maxPlayers = 4)
        {
            if (transport == null)
            {
                return (true, LocalJoinCode, "Transport missing.");
            }

            try
            {
                await EnsureUnityServicesAsync();
                var allocation = await RelayService.Instance.CreateAllocationAsync(Mathf.Max(1, maxPlayers - 1));
                var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                transport.SetRelayServerData(BuildRelayServerData(allocation));
                return (false, joinCode, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Relay host unavailable, falling back to direct transport. {ex.Message}");
                transport.SetConnectionData("127.0.0.1", DefaultDirectPort, "0.0.0.0");
                return (true, LocalJoinCode, ex.Message);
            }
        }

        public async Task<(bool useDirect, string error)> JoinAsync(UnityTransport transport, string joinCode)
        {
            if (transport == null)
            {
                return (true, "Transport missing.");
            }

            var code = (joinCode ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code) ||
                string.Equals(code, LocalJoinCode, StringComparison.OrdinalIgnoreCase) ||
                code == DefaultDirectPort.ToString())
            {
                transport.SetConnectionData("127.0.0.1", DefaultDirectPort);
                return (true, string.Empty);
            }

            if (TryParseDirectEndpoint(code, out var address, out var port))
            {
                transport.SetConnectionData(address, port);
                return (true, string.Empty);
            }

            try
            {
                await EnsureUnityServicesAsync();
                var allocation = await RelayService.Instance.JoinAllocationAsync(code.ToUpperInvariant());
                transport.SetRelayServerData(BuildRelayServerData(allocation));
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Relay join failed for '{code}', trying localhost. {ex.Message}");
                transport.SetConnectionData("127.0.0.1", DefaultDirectPort);
                return (true, ex.Message);
            }
        }

        private static RelayServerData BuildRelayServerData(Allocation allocation)
        {
            var endpoint = SelectRelayEndpoint(allocation.ServerEndpoints);
            return new RelayServerData(
                endpoint.Host,
                (ushort)endpoint.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,
                allocation.Key,
                endpoint.Secure);
        }

        private static RelayServerData BuildRelayServerData(JoinAllocation allocation)
        {
            var endpoint = SelectRelayEndpoint(allocation.ServerEndpoints);
            return new RelayServerData(
                endpoint.Host,
                (ushort)endpoint.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.HostConnectionData,
                allocation.Key,
                endpoint.Secure);
        }

        private static RelayServerEndpoint SelectRelayEndpoint(System.Collections.Generic.List<RelayServerEndpoint> endpoints)
        {
            var udp = endpoints?.FirstOrDefault(endpoint => endpoint.ConnectionType == "udp");
            if (udp != null)
            {
                return udp;
            }

            return endpoints != null && endpoints.Count > 0
                ? endpoints[0]
                : throw new InvalidOperationException("Relay allocation has no endpoints.");
        }

        private static async Task EnsureUnityServicesAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private static bool TryParseDirectEndpoint(string code, out string address, out ushort port)
        {
            address = "127.0.0.1";
            port = DefaultDirectPort;
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var parts = code.Split(':');
            if (parts.Length == 1 && ushort.TryParse(parts[0], out port))
            {
                address = "127.0.0.1";
                return true;
            }

            if (parts.Length == 2 && ushort.TryParse(parts[1], out port))
            {
                address = parts[0];
                return !string.IsNullOrWhiteSpace(address);
            }

            return false;
        }
    }
}
