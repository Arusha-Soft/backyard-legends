using System.Threading.Tasks;
using BackyardLegends.Core;
using BackyardLegends.Runtime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BackyardLegends.Runtime.Firebase
{
    /// <summary>
    /// Milestone 2 session cleanup: abandon Firestore table, clear local session, return to lobby.
    /// </summary>
    public static class MultiplayerSessionCleanup
    {
        public static async Task LeaveAndReturnToLobbyAsync(string reason = "left")
        {
            var session = SpadesNetworkSession.GetOrCreate();
            var tableId = session.TableId;
            var uid = session.LocalPlayerId;
            var isHost = session.Role == SpadesNetworkRole.Host;

            session.DisableAutoReconnect();

            if (!string.IsNullOrEmpty(tableId) && TableSessionService.IsAvailable)
            {
                try
                {
                    if (isHost || string.Equals(session.LastKnownHostUid, uid, System.StringComparison.Ordinal))
                    {
                        await TableSessionService.MarkAbandonedAsync(tableId, reason);
                    }
                    else if (session.HasLocalSeat)
                    {
                        await TableSessionService.LeaveOrAbandonAsync(tableId, uid, false, session.LocalLogicalSeat);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Session cleanup Firestore update failed: {ex.Message}");
                }
            }

            try
            {
                await MatchmakingService.CancelQueueAsync(uid);
            }
            catch
            {
                // Ignore queue cancel errors during teardown.
            }

            if (SpadesNetworkManagerHost.Instance != null)
            {
                SpadesNetworkManagerHost.Instance.Shutdown();
            }
            else
            {
                session.ConfigureOffline();
            }

            SceneManager.LoadScene("LobbyScene");
        }

        public static async Task MarkCompletedAndReturnAsync(TeamId? winner)
        {
            var session = SpadesNetworkSession.GetOrCreate();
            if (!string.IsNullOrEmpty(session.TableId) && TableSessionService.IsAvailable)
            {
                try
                {
                    await TableSessionService.MarkCompletedAsync(session.TableId, winner);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"Mark completed failed: {ex.Message}");
                }
            }

            session.DisableAutoReconnect();
            if (SpadesNetworkManagerHost.Instance != null)
            {
                SpadesNetworkManagerHost.Instance.Shutdown();
            }
            else
            {
                session.ConfigureOffline();
            }
        }
    }
}
