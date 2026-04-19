using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine.UIElements;

namespace schrader
{
    internal static class ScoreboardStarClientState
    {
        private static readonly object sync = new object();
        private static readonly FieldInfo playerVisualElementMapField = typeof(UIScoreboard)
            .GetField("playerVisualElementMap", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<string, int> starLevelByPlayerId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<ulong, int> starLevelByClientId = new Dictionary<ulong, int>();

        internal static void Reset()
        {
            lock (sync)
            {
                starLevelByPlayerId.Clear();
                starLevelByClientId.Clear();
            }

            ScoreboardBadgeClientState.Reset();
        }

        internal static void OnScoreboardStarsReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<ScoreboardStarStateMessage>(ref reader) ?? ScoreboardStarStateMessage.Empty();
                DraftUIPlugin.Log($"[CLIENT][JOIN] Scoreboard star state received. sender={senderClientId} players={(incomingState.Players?.Length ?? 0)}");
                ApplyState(incomingState);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive scoreboard star state: {ex}");
            }
        }

        private static void ApplyState(ScoreboardStarStateMessage state)
        {
            lock (sync)
            {
                starLevelByPlayerId.Clear();
                starLevelByClientId.Clear();

                foreach (var entry in state?.Players ?? Array.Empty<ScoreboardStarEntryMessage>())
                {
                    if (entry == null || entry.StarLevel <= 0)
                    {
                        continue;
                    }

                    var normalizedPlayerId = NormalizePlayerId(entry.PlayerId);
                    if (!string.IsNullOrWhiteSpace(normalizedPlayerId))
                    {
                        starLevelByPlayerId[normalizedPlayerId] = entry.StarLevel;
                    }

                    if (entry.ClientId != 0)
                    {
                        starLevelByClientId[entry.ClientId] = entry.StarLevel;
                    }
                }
            }

            RefreshVisibleScoreboard();
        }

        internal static void RefreshVisibleScoreboard()
        {
            var uiManager = UIManager.Instance;
            if (uiManager == null || uiManager.Scoreboard == null)
            {
                return;
            }

            if (!(playerVisualElementMapField?.GetValue(uiManager.Scoreboard) is Dictionary<Player, VisualElement> playerMap) || playerMap.Count == 0)
            {
                return;
            }

            foreach (var player in playerMap.Keys.ToArray())
            {
                if (player == null)
                {
                    continue;
                }

                uiManager.Scoreboard.UpdatePlayer(player);
            }
        }

        internal static void ApplyToScoreboardRow(UIScoreboard scoreboard, Player player)
        {
            if (scoreboard == null || player == null)
            {
                return;
            }

            if (!(playerVisualElementMapField?.GetValue(scoreboard) is Dictionary<Player, VisualElement> playerMap)
                || !playerMap.TryGetValue(player, out var row)
                || row == null)
            {
                return;
            }

            var hasStar = TryGetStarLevel(player, out var starLevel) && starLevel > 0;
            var hasBadge = ScoreboardBadgeClientState.TryGetBadge(player, out var badgeText, out var badgeColorHex);
            if (!hasStar && !hasBadge)
            {
                return;
            }

            var label = row.Q<Label>("UsernameLabel");
            if (label == null)
            {
                return;
            }

            var adminPrefix = BuildAdminPrefix(player);
            var starPrefix = hasStar ? $"<b><color={ResolveStarColorHex(starLevel)}>★</color></b>" : string.Empty;
            var badgePrefix = hasBadge ? $"<b><color={badgeColorHex}>{EscapeBadgeText(badgeText)}</color></b> " : string.Empty;
            label.text = $"{adminPrefix}{starPrefix}{(hasStar ? " " : string.Empty)}{badgePrefix}<noparse>#{player.Number.Value} {player.Username.Value}</noparse>";
        }

        private static bool TryGetStarLevel(Player player, out int starLevel)
        {
            starLevel = 0;
            if (player == null)
            {
                return false;
            }

            var steamId = NormalizePlayerId(player.SteamId.Value.ToString());
            lock (sync)
            {
                if (!string.IsNullOrWhiteSpace(steamId) && starLevelByPlayerId.TryGetValue(steamId, out starLevel))
                {
                    return true;
                }

                if (starLevelByClientId.TryGetValue(player.OwnerClientId, out starLevel))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePlayerId(string playerId)
        {
            return string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim();
        }

        private static string BuildAdminPrefix(Player player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            if (player.AdminLevel.Value == 1)
            {
                return "<b><color=#206694>*</color></b>";
            }

            if (player.AdminLevel.Value == 2)
            {
                return "<b><color=#992d22>*</color></b>";
            }

            return player.AdminLevel.Value > 2
                ? "<b><color=#71368a>*</color></b>"
                : string.Empty;
        }

        private static string ResolveStarColorHex(int starLevel)
        {
            switch (Math.Max(1, Math.Min(5, starLevel)))
            {
                case 1:
                    return "#8bd3ff";
                case 2:
                    return "#7dffb2";
                case 3:
                    return "#ffd166";
                case 4:
                    return "#ff9f43";
                default:
                    return "#ff5d8f";
            }
        }

        private static string EscapeBadgeText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        [HarmonyPatch(typeof(UIScoreboard), "UpdatePlayer")]
        private static class UiScoreboardUpdatePlayerPatch
        {
            [HarmonyPostfix]
            private static void Postfix(UIScoreboard __instance, Player player)
            {
                ApplyToScoreboardRow(__instance, player);
            }
        }
    }
}