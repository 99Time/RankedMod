using System;
using UnityEngine;

namespace schrader
{
    internal static class DraftUIController
    {
        private static string lastSignature;

        public static void Initialize()
        {
            lastSignature = null;
        }

        public static void ApplyState(DraftOverlayStateMessage state)
        {
            if (state == null) return;

            Debug.Log("APPLY STATE CALLED");

            var signature = BuildSignature(state);
            if (string.Equals(signature, lastSignature, StringComparison.Ordinal)) return;

            lastSignature = signature;

            DraftUI.UpdateDraftUI(state);
        }

        private static string BuildSignature(DraftOverlayStateMessage state)
        {
            return string.Join("|",
                state.IsVisible,
                state.IsCompleted,
                state.Title ?? "",
                state.RedCaptainName ?? "",
                state.BlueCaptainName ?? "",
                state.CurrentTurnName ?? "",
                state.PendingLateJoinerCount,
                state.DummyModeActive,
                string.Join(",", state.AvailablePlayers ?? Array.Empty<string>()),
                string.Join(",", state.RedPlayers ?? Array.Empty<string>()),
                string.Join(",", state.BluePlayers ?? Array.Empty<string>()),
                state.FooterText ?? "");
        }
    }
}