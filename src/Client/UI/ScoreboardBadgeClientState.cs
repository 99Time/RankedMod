using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace schrader
{
    internal static class ScoreboardBadgeClientState
    {
        private sealed class ScoreboardBadgeEntryState
        {
            public string BadgeText;
            public string ColorHex;
        }

        private static readonly object sync = new object();
        private static readonly Dictionary<string, ScoreboardBadgeEntryState> badgeByPlayerId = new Dictionary<string, ScoreboardBadgeEntryState>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, ScoreboardBadgeEntryState> badgeByClientId = new Dictionary<ulong, ScoreboardBadgeEntryState>();

        internal static void Reset()
        {
            lock (sync)
            {
                badgeByPlayerId.Clear();
                badgeByClientId.Clear();
            }
        }

        internal static void OnScoreboardBadgesReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<ScoreboardBadgeStateMessage>(ref reader) ?? ScoreboardBadgeStateMessage.Empty();
                DraftUIPlugin.Log($"[CLIENT][JOIN] Scoreboard badge state received. sender={senderClientId} players={(incomingState.Players?.Length ?? 0)}");
                ApplyState(incomingState);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive scoreboard badge state: {ex}");
            }
        }

        internal static bool TryGetBadge(Player player, out string badgeText, out string colorHex)
        {
            badgeText = null;
            colorHex = null;
            if (player == null)
            {
                return false;
            }

            var normalizedPlayerId = NormalizePlayerId(player.SteamId.Value.ToString());
            lock (sync)
            {
                if (!string.IsNullOrWhiteSpace(normalizedPlayerId)
                    && badgeByPlayerId.TryGetValue(normalizedPlayerId, out var playerEntry)
                    && playerEntry != null
                    && !string.IsNullOrWhiteSpace(playerEntry.BadgeText))
                {
                    badgeText = playerEntry.BadgeText;
                    colorHex = playerEntry.ColorHex;
                    return true;
                }

                if (badgeByClientId.TryGetValue(player.OwnerClientId, out var clientEntry)
                    && clientEntry != null
                    && !string.IsNullOrWhiteSpace(clientEntry.BadgeText))
                {
                    badgeText = clientEntry.BadgeText;
                    colorHex = clientEntry.ColorHex;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryGetBadge(string playerId, ulong clientId, out string badgeText, out string colorHex)
        {
            badgeText = null;
            colorHex = null;

            var normalizedPlayerId = NormalizePlayerId(playerId);
            lock (sync)
            {
                if (!string.IsNullOrWhiteSpace(normalizedPlayerId)
                    && badgeByPlayerId.TryGetValue(normalizedPlayerId, out var playerEntry)
                    && playerEntry != null
                    && !string.IsNullOrWhiteSpace(playerEntry.BadgeText))
                {
                    badgeText = playerEntry.BadgeText;
                    colorHex = playerEntry.ColorHex;
                    return true;
                }

                if (clientId != 0
                    && badgeByClientId.TryGetValue(clientId, out var clientEntry)
                    && clientEntry != null
                    && !string.IsNullOrWhiteSpace(clientEntry.BadgeText))
                {
                    badgeText = clientEntry.BadgeText;
                    colorHex = clientEntry.ColorHex;
                    return true;
                }
            }

            return false;
        }

        private static void ApplyState(ScoreboardBadgeStateMessage state)
        {
            var appliedEntries = 0;
            lock (sync)
            {
                badgeByPlayerId.Clear();
                badgeByClientId.Clear();

                foreach (var entry in state?.Players ?? Array.Empty<ScoreboardBadgeEntryMessage>())
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.BadgeText))
                    {
                        continue;
                    }

                    var storedEntry = new ScoreboardBadgeEntryState
                    {
                        BadgeText = entry.BadgeText.Trim(),
                        ColorHex = NormalizeColorHex(entry.ColorHex)
                    };

                    var normalizedPlayerId = NormalizePlayerId(entry.PlayerId);
                    if (!string.IsNullOrWhiteSpace(normalizedPlayerId))
                    {
                        badgeByPlayerId[normalizedPlayerId] = storedEntry;
                    }

                    if (entry.ClientId != 0)
                    {
                        badgeByClientId[entry.ClientId] = storedEntry;
                    }

                    appliedEntries++;
                }
            }

            DraftUIPlugin.Log($"[CLIENT][SCOREBOARD] Scoreboard badge cache applied. entries={appliedEntries}. Refreshing visible scoreboard rows.");
            ScoreboardStarClientState.RefreshVisibleScoreboard();
        }

        private static string NormalizePlayerId(string playerId)
        {
            return string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
        }

        private static string NormalizeColorHex(string colorHex)
        {
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                return "#f7c66b";
            }

            var trimmed = colorHex.Trim();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                trimmed = "#" + trimmed;
            }

            return trimmed.Length == 7 ? trimmed : "#f7c66b";
        }
    }
}