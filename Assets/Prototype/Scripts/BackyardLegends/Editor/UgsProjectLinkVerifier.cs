using UnityEditor;
using UnityEngine;

namespace BackyardLegends.Editor
{
    /// <summary>
    /// Step 0 gate: confirm Team UGS / Relay project link before online testing.
    /// Linking itself is done in Edit → Project Settings → Services (requires Dashboard access).
    /// </summary>
    public static class UgsProjectLinkVerifier
    {
        private const string MenuPath = "Backyard Legends/Verify UGS Project Link";

        [MenuItem(MenuPath)]
        public static void Verify()
        {
            var cloudId = Application.cloudProjectId;
            var hasCloudId = !string.IsNullOrWhiteSpace(cloudId);

            if (!hasCloudId)
            {
                Debug.LogWarning(
                    "UGS NOT LINKED — cloudProjectId empty.\n" +
                    "1) Edit → Project Settings → Services\n" +
                    "2) Sign in with Team UGS Backyard access\n" +
                    "3) Link the existing Backyard cloud project\n" +
                    "4) Dashboard → Multiplayer → Relay enabled\n" +
                    "5) Re-run this menu, then Host Table (code must not be LOCAL)");
                EditorUtility.DisplayDialog(
                    "UGS Not Linked",
                    "cloudProjectId is empty.\n\nLink this Unity project to the Team UGS Backyard project under Project Settings → Services, then re-run this check.",
                    "OK");
                return;
            }

            Debug.Log($"UGS linked — cloudProjectId={cloudId}. Host a table and confirm join code is Relay (not LOCAL).");
            EditorUtility.DisplayDialog(
                "UGS Linked",
                $"cloudProjectId = {cloudId}\n\nHost a table and confirm the join code is a Relay code (not LOCAL).",
                "OK");
        }
    }
}
