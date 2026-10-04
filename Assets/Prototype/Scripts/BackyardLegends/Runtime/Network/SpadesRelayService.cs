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

        /// <summary>
        /// Host via Unity Relay. When <paramref name="requireRelay"/> is true, never fall back to LOCAL —
        /// fail so the lobby can show an actionable error (UGS not linked, offline, etc.).
        /// </summary>
        public async Task<(bool useDirect, string joinCode, string error)> HostAsync(
            UnityTransport transport,
            int maxPlayers = 4,
            bool requireRelay = false)
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
                Debug.Log($"Unity Relay host allocation ok — joinCode={joinCode}");
                return (false, joinCode, string.Empty);
            }
            catch (Exception ex)
            {
                var message = BuildRelayFailureMessage(ex, host: true);
                if (requireRelay)
                {
                    Debug.LogError(message);
                    return (true, LocalJoinCode, message);
                }

                Debug.LogWarning($"Relay host unavailable, falling back to direct transport. {ex.Message}");
                transport.SetConnectionData("127.0.0.1", DefaultDirectPort, "0.0.0.0");
                return (true, LocalJoinCode, message);
            }
        }

        /// <summary>
        /// Join via Unity Relay. Explicit LOCAL / ip:port still use direct transport.
        /// When <paramref name="requireRelay"/> is true, Relay failures do not silently fall back to localhost.
        /// </summary>
        public async Task<(bool useDirect, string error)> JoinAsync(
            UnityTransport transport,
            string joinCode,
            bool requireRelay = false)
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
                if (requireRelay &&
                    (string.IsNullOrEmpty(code) ||
                     string.Equals(code, LocalJoinCode, StringComparison.OrdinalIgnoreCase)))
                {
                    return (true, "Online join requires a Relay invite code (not LOCAL). Host with UGS linked, then share that code.");
                }

                transport.SetConnectionData("127.0.0.1", DefaultDirectPort);
                return (true, string.Empty);
            }

            if (TryParseDirectEndpoint(code, out var address, out var port))
            {
                if (requireRelay)
                {
                    return (true, "Online join expects a Relay code, not an IP:port. Use the host invite code.");
                }

                transport.SetConnectionData(address, port);
                return (true, string.Empty);
            }

            try
            {
                await EnsureUnityServicesAsync();
                var allocation = await RelayService.Instance.JoinAllocationAsync(code.ToUpperInvariant());
                transport.SetRelayServerData(BuildRelayServerData(allocation));
                Debug.Log($"Unity Relay join ok — code={code.ToUpperInvariant()}");
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                var message = BuildRelayFailureMessage(ex, host: false);
                if (requireRelay)
                {
                    Debug.LogError(message);
                    return (true, message);
                }

                Debug.LogWarning($"Relay join failed for '{code}', trying localhost. {ex.Message}");
                transport.SetConnectionData("127.0.0.1", DefaultDirectPort);
                return (true, message);
            }
        }

        private static string BuildRelayFailureMessage(Exception ex, bool host)
        {
            var cloudId = Application.cloudProjectId;
            var linked = !string.IsNullOrWhiteSpace(cloudId);
            var verb = host ? "create" : "join";
            if (!linked)
            {
                return
                    $"Relay {verb} failed — Unity project is not linked to Team UGS (cloudProjectId empty). " +
                    "Edit → Project Settings → Services → link Backyard, then Backyard Legends/Verify UGS Project Link. " +
                    $"Detail: {ex.Message}";
            }

            return $"Relay {verb} failed (project {cloudId}). Check Dashboard → Relay is enabled and you are online. Detail: {ex.Message}";
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
