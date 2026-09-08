using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesNetworkManagerHost : MonoBehaviour
    {
        public static SpadesNetworkManagerHost Instance { get; private set; }

        [SerializeField] private GameObject tableNetworkPrefab;

        private readonly SpadesRelayService relayService = new();

        public NetworkManager NetworkManager => NetworkManager.Singleton;
        public UnityTransport Transport { get; private set; }
        public GameObject TablePrefab => tableNetworkPrefab;

        public static SpadesNetworkManagerHost GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<SpadesNetworkManagerHost>();
            if (existing != null)
            {
                existing.EnsureInitialized();
                return existing;
            }

            var go = new GameObject("Spades Network Manager Host");
            return go.AddComponent<SpadesNetworkManagerHost>();
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
            EnsureNetworkManager();
        }

        public void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null)
            {
                Transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (Transport == null)
                {
                    Transport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
                    NetworkManager.Singleton.NetworkConfig.NetworkTransport = Transport;
                }

                EnsureTablePrefabRegistered();
                return;
            }

            var go = gameObject;
            Transport = go.GetComponent<UnityTransport>();
            if (Transport == null)
            {
                Transport = go.AddComponent<UnityTransport>();
            }

            var nm = go.GetComponent<NetworkManager>();
            if (nm == null)
            {
                nm = go.AddComponent<NetworkManager>();
            }

            nm.NetworkConfig.NetworkTransport = Transport;
            nm.NetworkConfig.EnableSceneManagement = true;
            nm.NetworkConfig.ConnectionApproval = false;
            EnsureTablePrefabRegistered();
        }

        private void EnsureTablePrefabRegistered()
        {
            if (tableNetworkPrefab == null)
            {
                tableNetworkPrefab = Resources.Load<GameObject>("BackyardLegends/SpadesTableNetwork");
            }

            if (TablePrefab == null)
            {
                Debug.LogError("Missing Resources/BackyardLegends/SpadesTableNetwork prefab. Run Backyard Legends/Create Network Prefabs.");
                return;
            }

            if (NetworkManager.Singleton == null)
            {
                return;
            }

            try
            {
                NetworkManager.Singleton.AddNetworkPrefab(TablePrefab);
            }
            catch
            {
                // Already registered.
            }
        }

        public async Task<bool> StartHostAsync()
        {
            EnsureNetworkManager();
            var session = SpadesNetworkSession.GetOrCreate();
            session.SetStatus("Allocating connection…");

            var (useDirect, joinCode, error) = await relayService.HostAsync(Transport);
            session.SetConnectionInfo(joinCode, useDirect, "127.0.0.1", SpadesRelayService.DefaultDirectPort);
            if (!string.IsNullOrEmpty(error) && useDirect)
            {
                session.SetStatus($"Direct host ({joinCode}) — Relay unavailable");
            }
            else
            {
                session.SetStatus($"Hosting · code {joinCode}");
            }

            if (!NetworkManager.Singleton.StartHost())
            {
                session.SetStatus("Failed to start host.");
                return false;
            }

            return NetworkManager.Singleton.IsListening;
        }

        public async Task<bool> StartClientAsync(string joinCode)
        {
            EnsureNetworkManager();
            var session = SpadesNetworkSession.GetOrCreate();
            session.SetStatus($"Joining {joinCode}…");

            var (useDirect, error) = await relayService.JoinAsync(Transport, joinCode);
            session.SetConnectionInfo(joinCode, useDirect, "127.0.0.1", SpadesRelayService.DefaultDirectPort);
            if (!string.IsNullOrEmpty(error) && useDirect)
            {
                session.SetStatus($"Direct join fallback — {error}");
            }

            if (!NetworkManager.Singleton.StartClient())
            {
                session.SetStatus("Failed to start client.");
                return false;
            }

            var timeoutAt = Time.realtimeSinceStartup + 12f;
            while (!NetworkManager.Singleton.IsConnectedClient && Time.realtimeSinceStartup < timeoutAt)
            {
                await Task.Yield();
            }

            if (!NetworkManager.Singleton.IsConnectedClient)
            {
                session.SetStatus("Timed out connecting to host.");
                NetworkManager.Singleton.Shutdown();
                return false;
            }

            session.SetStatus("Connected");
            return true;
        }

        public void SpawnTableIfHost()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (SpadesTableNetwork.Instance != null)
            {
                return;
            }

            EnsureTablePrefabRegistered();
            var prefab = TablePrefab;
            if (prefab == null)
            {
                Debug.LogError("Spades table network prefab missing.");
                return;
            }

            var instance = Instantiate(prefab);
            instance.name = "Spades Table Network";
            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);
            var netObj = instance.GetComponent<NetworkObject>();
            netObj.Spawn(true);
        }

        public void Shutdown()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            SpadesNetworkSession.GetOrCreate().ConfigureOffline();
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
