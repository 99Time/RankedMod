using System;
using System.Linq;
using UnityEngine;

namespace schrader
{
    // Server-side draft state bridge: never renders UI, only emits draft state to clients via chat.
    internal static class DraftStateBridge
    {
        private static string lastStateSignature;

        public static void EnsureInitialized()
        {
            if (Application.isBatchMode)
            {
                Debug.Log($"[{Constants.MOD_NAME}] Skipping UI: running on server (batch mode)");
            }
        }

        public static void PublishState(Server.RankedSystem.DraftOverlayState state)
        {
            if (!Application.isBatchMode) return;
            if (state == null) return;

            try
            {
                var signature = BuildSignature(state);
                if (string.Equals(signature, lastStateSignature, StringComparison.Ordinal)) return;
                lastStateSignature = signature;

                var payload = BuildPayload(state);
                try { UIChat.Instance.Server_SendSystemChatMessage(payload); } catch { }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to publish draft state payload: {ex.Message}");
            }
        }

        public static bool Toggle()
        {
            return false;
        }

        public static bool Show()
        {
            return false;
        }

        public static bool Hide()
        {
            return false;
        }

        public static bool CanRenderInCurrentProcess()
        {
            return !Application.isBatchMode;
        }

        public static string GetUnavailableReason()
        {
            return Application.isBatchMode
                ? "Draft UI is client-only and is not available on a dedicated server."
                : string.Empty;
        }

        public static bool IsTestModeEnabled()
        {
            return false;
        }

        public static bool IsVisible()
        {
            return false;
        }

        public static bool HasDisplayableState()
        {
            return false;
        }

        public static void Shutdown()
        {
        }

        private static string BuildPayload(Server.RankedSystem.DraftOverlayState state)
        {
            var players = string.Join(",", state.AvailablePlayers ?? Array.Empty<string>());
            var red = state.RedCaptainName ?? string.Empty;
            var blue = state.BlueCaptainName ?? string.Empty;
            var turn = state.CurrentTurnName ?? string.Empty;
            return string.Join("\n", new[]
            {
                "[DRAFT_STATE]",
                $"visible={(state.IsVisible ? "1" : "0")}",
                $"title={Escape(state.Title)}",
                $"captain_red={Escape(red)}",
                $"captain_blue={Escape(blue)}",
                $"turn={Escape(turn)}",
                $"status={(state.IsCompleted ? "complete" : "active")}",
                $"players={Escape(players)}",
                $"pending={state.PendingLateJoinerCount}",
                $"dummy={(state.DummyModeActive ? "1" : "0")}",
                $"footer={Escape(state.FooterText)}"
            });
        }

        private static string BuildSignature(Server.RankedSystem.DraftOverlayState state)
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

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\n", " ").Replace("\r", " ").Trim();
        }
    }
}
