using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackyardLegends.Runtime.Firebase;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BackyardLegends.Runtime.Network
{
    public sealed class SpadesNetworkManagerHost : MonoBehaviour
    {
        private const int MaxReconnectAttempts = 5;
        private const float ReconnectBackoffSeconds = 2f;

        public static SpadesNetworkManagerHost Instance { get; private set; }

        [SerializeField] private GameObject tableNetworkPrefab;

        private readonly SpadesRelayService relayService = new();
        private bool reconnectInFlight;
        private Coroutine reconnectRoutine;
        private bool clientDisconnectHooked;

        public Unity.Netcode.NetworkManager NetManager => Unity.Netcode.NetworkManager.Singleton;
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
            var singleton = Unity.Netcode.NetworkManager.Singleton;
            if (singleton != null)
            {
                Transport = singleton.GetComponent<UnityTransport>();
                if (Transport == null)
                {
                    Transport = singleton.gameObject.AddComponent<UnityTransport>();
                }

                ApplyNetworkConfig(singleton);
                EnsureTablePrefabRegistered();
                HookClientDisconnect();
                return;
            }

            var go = gameObject;
            Transport = go.GetComponent<UnityTransport>();
            if (Transport == null)
            {
                Transport = go.AddComponent<UnityTransport>();
            }

            var nm = go.GetComponent<Unity.Netcode.NetworkManager>();
            if (nm == null)
            {
                nm = go.AddComponent<Unity.Netcode.NetworkManager>();
            }

            if (nm == null)
            {
                Debug.LogError("Failed to create Unity Netcode NetworkManager.");
                return;
            }

            ApplyNetworkConfig(nm);
            EnsureTablePrefabRegistered();
            HookClientDisconnect();
        }

        private void ApplyNetworkConfig(Unity.Netcode.NetworkManager nm)
        {
            if (nm == null)
            {
                return;
            }

            if (nm.NetworkConfig == null)
            {
                nm.NetworkConfig = new NetworkConfig();
            }

            nm.NetworkConfig.NetworkTransport = Transport;
            // Manual SceneManager loads in Host/Join — NGO scene sync fights ParrelSync joins.
            nm.NetworkConfig.EnableSceneManagement = false;
            nm.NetworkConfig.ConnectionApproval = false;
        }

        private void HookClientDisconnect()
        {
            var singleton = Unity.Netcode.NetworkManager.Singleton;
            if (clientDisconnectHooked || singleton == null)
            {
                return;
            }

            singleton.OnClientDisconnectCallback += HandleLocalClientDisconnected;
            clientDisconnectHooked = true;
        }

        private void HandleLocalClientDisconnected(ulong clientId)
        {
            if (reconnectInFlight)
            {
                return;
            }

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.IsServer)
            {
                return;
            }

            if (clientId != nm.LocalClientId)
            {
                return;
            }

            var session = SpadesNetworkSession.GetOrCreate();
            if (!session.AutoReconnectEnabled ||
                session.Role != SpadesNetworkRole.Client ||
                !session.MatchWasLive ||
                string.IsNullOrWhiteSpace(session.JoinCode))
            {
                return;
            }

            if (reconnectRoutine != null)
            {
                StopCoroutine(reconnectRoutine);
            }

            reconnectRoutine = StartCoroutine(ReconnectClientRoutine(session.JoinCode));
        }

        private IEnumerator ReconnectClientRoutine(string joinCode)
        {
            reconnectInFlight = true;
            var session = SpadesNetworkSession.GetOrCreate();
            session.SetStatus("Disconnected — reconnecting…");

            for (var attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
            {
                if (!session.AutoReconnectEnabled || session.Role != SpadesNetworkRole.Client)
                {
                    break;
                }

                session.SetStatus($"Reconnect attempt {attempt}/{MaxReconnectAttempts}…");
                ShutdownNetworkOnly();

                var task = StartClientAsync(joinCode, preserveSession: true);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                if (task.Status == TaskStatus.RanToCompletion && task.Result)
                {
                    session.SetStatus("Reconnected");
                    reconnectInFlight = false;
                    reconnectRoutine = null;
                    yield break;
                }

                yield return new WaitForSecondsRealtime(ReconnectBackoffSeconds * attempt);
            }

            session.SetStatus("Reconnect failed — attempting host failover…");
            session.DisableAutoReconnect();
            SpadesHostFailover.GetOrCreate().NotifyPossibleHostLoss();
            reconnectInFlight = false;
            reconnectRoutine = null;
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

            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm?.NetworkConfig == null)
            {
                return;
            }

            var prefabs = nm.NetworkConfig.Prefabs;
            if (prefabs != null && prefabs.Contains(TablePrefab))
            {
                return;
            }

            try
            {
                nm.AddNetworkPrefab(TablePrefab);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SpadesTableNetwork prefab already registered: {ex.Message}");
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

            if (!Unity.Netcode.NetworkManager.Singleton.StartHost())
            {
                session.SetStatus("Failed to start host.");
                return false;
            }

            // Spawn before gameplay LoadScene so late joiners never miss the table object.
            SpawnTableIfHost();
            await EnsureFirestoreTableForHostAsync(session);
            return Unity.Netcode.NetworkManager.Singleton.IsListening;
        }

        private static async Task EnsureFirestoreTableForHostAsync(SpadesNetworkSession session)
        {
            await FirebaseBootstrap.EnsureInitializedAsync();
            if (!TableSessionService.IsAvailable)
            {
                Debug.LogWarning("Firebase unavailable — host migration will not run for this table.");
                return;
            }

            // Rules require hostUid == request.auth.uid.
            var authUid = string.Empty;
            try
            {
                authUid = FirebaseBootstrap.GetAuth()?.CurrentUser?.UserId ?? string.Empty;
            }
            catch
            {
                authUid = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(authUid))
            {
                var auth = FirebaseAuthService.GetOrCreate();
                var user = await auth.EnsureSignedInAsync();
                authUid = user != null && user.IsSignedIn ? user.Uid : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(authUid))
            {
                Debug.LogWarning("No Firebase Auth uid — cannot create Firestore table.");
                return;
            }

            session.SetLocalPlayerIdentity(authUid, session.LocalDisplayName);

            if (!string.IsNullOrEmpty(session.TableId) && session.PendingRestoreState != null)
            {
                await TableSessionService.WriteRelayAsync(session.TableId, session.JoinCode, authUid);
                return;
            }

            var rules = session.PendingRules ?? BackyardLegendsSession.GetOrCreateRuntimeInstance().SelectedRule;
            var sessionKey = string.IsNullOrEmpty(session.SessionKey)
                ? System.Guid.NewGuid().ToString("N")
                : session.SessionKey;

            try
            {
                var table = await TableSessionService.CreateTableAsync(
                    authUid,
                    session.LocalDisplayName,
                    rules,
                    session.JoinCode,
                    sessionKey);
                if (table != null)
                {
                    session.SetTableSession(table.TableId, table.SessionKey, authUid);
                    SpadesHostFailover.GetOrCreate().BeginWatchingTable(table.TableId);
                    Debug.Log($"Firestore table ready: {table.TableId} join={session.JoinCode}");

                    // If gameplay table already spawned, push tableId to connected clients now.
                    if (SpadesTableNetwork.Instance != null && SpadesTableNetwork.Instance.IsServer)
                    {
                        SpadesTableNetwork.Instance.SyncTableSessionToClients();
                    }
                }
                else
                {
                    Debug.LogWarning("CreateTableAsync returned null.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create Firestore table: {ex.Message}");
            }
        }

        public Task<bool> StartClientAsync(string joinCode)
        {
            return StartClientAsync(joinCode, preserveSession: false);
        }

        public async Task<bool> StartClientAsync(string joinCode, bool preserveSession)
        {
            EnsureNetworkManager();
            var session = SpadesNetworkSession.GetOrCreate();
            if (!preserveSession)
            {
                session.SetStatus($"Joining {joinCode}…");
            }

            var (useDirect, error) = await relayService.JoinAsync(Transport, joinCode);
            session.SetConnectionInfo(joinCode, useDirect, "127.0.0.1", SpadesRelayService.DefaultDirectPort);
            if (!string.IsNullOrEmpty(error) && useDirect)
            {
                session.SetStatus($"Direct join fallback — {error}");
            }

            if (!Unity.Netcode.NetworkManager.Singleton.StartClient())
            {
                session.SetStatus("Failed to start client.");
                return false;
            }

            var timeoutAt = Time.realtimeSinceStartup + 20f;
            while (!Unity.Netcode.NetworkManager.Singleton.IsConnectedClient && Time.realtimeSinceStartup < timeoutAt)
            {
                await Task.Yield();
            }

            if (!Unity.Netcode.NetworkManager.Singleton.IsConnectedClient)
            {
                session.SetStatus("Timed out connecting to host.");
                Debug.LogWarning($"StartClient timed out for joinCode={joinCode}. Is host running on LOCAL/127.0.0.1:7777?");
                ShutdownNetworkOnly();
                return false;
            }

            if (!preserveSession)
            {
                session.SetStatus("Connected");
            }

            return true;
        }

        public void SpawnTableIfHost()
        {
            if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer)
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
            // Survive client/host SceneManager.LoadScene(Single) after connect.
            DontDestroyOnLoad(instance);
            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj.IsSpawned)
            {
                return;
            }

            netObj.Spawn(true);
            Debug.Log($"Spawned SpadesTableNetwork netId={netObj.NetworkObjectId} (DDOL)");
        }

        public void ShutdownNetworkKeepingSession()
        {
            if (reconnectRoutine != null)
            {
                StopCoroutine(reconnectRoutine);
                reconnectRoutine = null;
            }

            reconnectInFlight = false;
            ShutdownNetworkOnly();
        }

        public void Shutdown()
        {
            SpadesHostFailover.GetOrCreate().StopWatching();
            if (reconnectRoutine != null)
            {
                StopCoroutine(reconnectRoutine);
                reconnectRoutine = null;
            }

            reconnectInFlight = false;
            ShutdownNetworkOnly();
            SpadesNetworkSession.GetOrCreate().ConfigureOffline();
        }

        private void ShutdownNetworkOnly()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
        }

        private void OnDestroy()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && clientDisconnectHooked)
            {
                Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= HandleLocalClientDisconnected;
                clientDisconnectHooked = false;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
