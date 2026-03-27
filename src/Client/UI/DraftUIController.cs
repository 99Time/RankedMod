using System;

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

            var signature = BuildSignature(state);
            if (string.Equals(signature, lastSignature, StringComparison.Ordinal)) return;
            lastSignature = signature;

            //DraftUI.UpdateDraftUI(state);
        }

        private static string BuildSignature(DraftOverlayStateMessage state)
        {
            return string.Join("|",
                state.IsVisible,
                state.IsCompleted,
                state.Title ?? string.Empty,
                state.RedCaptainName ?? string.Empty,
                state.BlueCaptainName ?? string.Empty,
                state.CurrentTurnName ?? string.Empty,
                state.PendingLateJoinerCount,
                state.DummyModeActive,
                string.Join(",", state.AvailablePlayers ?? Array.Empty<string>()),
                string.Join(",", state.RedPlayers ?? Array.Empty<string>()),
                string.Join(",", state.BluePlayers ?? Array.Empty<string>()),
                state.FooterText ?? string.Empty);
        }
    }
}