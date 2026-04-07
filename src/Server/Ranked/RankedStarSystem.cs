using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const int MaxStarPoints = 5;
        private const int MaxLossStarProtection = 5;
        private static float rankedMatchLiveStartedAt = -999f;
        private static readonly HashSet<string> currentMatchApprovedLateJoinKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<ulong> currentMatchApprovedLateJoinClientIds = new HashSet<ulong>();

        private static void MarkRankedMatchLiveStarted()
        {
            rankedMatchLiveStartedAt = Time.unscaledTime;
            currentMatchApprovedLateJoinKeys.Clear();
            currentMatchApprovedLateJoinClientIds.Clear();
        }

        private static void ClearRankedLiveParticipationTracking()
        {
            rankedMatchLiveStartedAt = -999f;
            currentMatchApprovedLateJoinKeys.Clear();
            currentMatchApprovedLateJoinClientIds.Clear();
        }

        private static bool IsRankedMatchLive()
        {
            return rankedActive && !draftActive && rankedMatchLiveStartedAt >= 0f;
        }

        private static void RecordApprovedLateJoinForCurrentMatch(TeamApprovalRequest request)
        {
            if (request == null || request.Kind != TeamApprovalRequestKind.LateJoin || !IsRankedMatchLive())
            {
                return;
            }

            var normalizedKey = NormalizeResolvedPlayerKey(request.PlayerId);
            if (!string.IsNullOrWhiteSpace(normalizedKey))
            {
                currentMatchApprovedLateJoinKeys.Add(normalizedKey);
            }

            if (request.ClientId != 0)
            {
                currentMatchApprovedLateJoinClientIds.Add(request.ClientId);
                currentMatchApprovedLateJoinKeys.Add($"clientId:{request.ClientId}");
            }
        }

        private static bool WasApprovedLateJoinForCurrentMatch(RankedParticipant participant, string resolvedId)
        {
            var normalizedResolvedId = NormalizeResolvedPlayerKey(resolvedId);
            if (!string.IsNullOrWhiteSpace(normalizedResolvedId)
                && currentMatchApprovedLateJoinKeys.Contains(normalizedResolvedId))
            {
                return true;
            }

            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant?.playerId);
            if (!string.IsNullOrWhiteSpace(participantKey)
                && currentMatchApprovedLateJoinKeys.Contains(participantKey))
            {
                return true;
            }

            return participant != null
                && participant.clientId != 0
                && currentMatchApprovedLateJoinClientIds.Contains(participant.clientId);
        }

        private static int GetStoredStarLevel(string playerKey)
        {
            return Mathf.Clamp(GetStoredStarPointsForPlayerKey(playerKey), 0, MaxStarPoints);
        }

        public static string BuildChatStarPrefix(object player)
        {
            var starLevel = ResolveConnectedPlayerStarLevel(player);
            if (starLevel <= 0)
            {
                return string.Empty;
            }

            return $"<b><color={ResolveStarColorHex(starLevel)}>★</color></b> ";
        }

        private static ScoreboardStarStateMessage BuildScoreboardStarState()
        {
            EnsureStarProgressLoaded();

            var entryMap = new Dictionary<string, ScoreboardStarEntryMessage>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in GetAllPlayers() ?? new List<object>())
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot) || snapshot == null)
                {
                    continue;
                }

                var preferredKey = ResolveParticipantIdToKey(snapshot) ?? snapshot.playerId;
                var canonicalKey = ResolveCanonicalMmrKey(snapshot, preferredKey, out _)
                    ?? NormalizeResolvedPlayerKey(preferredKey);
                var starLevel = GetStoredStarLevel(canonicalKey);
                if (starLevel <= 0 && snapshot.clientId != 0)
                {
                    starLevel = GetStoredStarLevel($"clientId:{snapshot.clientId}");
                }

                if (starLevel <= 0)
                {
                    continue;
                }

                var dedupeKey = !string.IsNullOrWhiteSpace(canonicalKey)
                    ? canonicalKey
                    : $"clientId:{snapshot.clientId}";
                entryMap[dedupeKey] = new ScoreboardStarEntryMessage
                {
                    PlayerId = canonicalKey,
                    ClientId = snapshot.clientId,
                    StarLevel = starLevel
                };
            }

            return new ScoreboardStarStateMessage
            {
                Players = entryMap.Values
                    .OrderByDescending(entry => entry.StarLevel)
                    .ThenBy(entry => entry.PlayerId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.ClientId)
                    .ToArray()
            };
        }

        private static void PublishScoreboardStarState()
        {
            RankedOverlayNetwork.PublishScoreboardStars(BuildScoreboardStarState());
        }

        public static ScoreboardStarStateMessage GetScoreboardStarStateForClient(ulong clientId)
        {
            return BuildScoreboardStarState();
        }

        private static int ResolveConnectedPlayerStarLevel(object player)
        {
            if (player == null)
            {
                return 0;
            }

            EnsureMmrLoaded();

            try
            {
                if (player is Player livePlayer)
                {
                    var steamId = NormalizeResolvedPlayerKey(livePlayer.SteamId.Value.ToString());
                    var starLevel = GetStoredStarLevel(steamId);
                    if (starLevel > 0)
                    {
                        return starLevel;
                    }

                    if (livePlayer.OwnerClientId != 0)
                    {
                        return GetStoredStarLevel($"clientId:{livePlayer.OwnerClientId}");
                    }

                    return 0;
                }

                var playerType = player.GetType();
                var steamIdProp = playerType.GetProperty("SteamId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var ownerClientIdProp = playerType.GetProperty("OwnerClientId", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var rawSteamId = NormalizeResolvedPlayerKey(ExtractSimpleValueToString(steamIdProp?.GetValue(player)));
                if (!string.IsNullOrWhiteSpace(rawSteamId))
                {
                    var starLevel = GetStoredStarLevel(rawSteamId);
                    if (starLevel > 0)
                    {
                        return starLevel;
                    }
                }

                var rawClientId = ExtractSimpleValueToString(ownerClientIdProp?.GetValue(player));
                if (ulong.TryParse(rawClientId, out var ownerClientId) && ownerClientId != 0)
                {
                    return GetStoredStarLevel($"clientId:{ownerClientId}");
                }
            }
            catch
            {
            }

            return 0;
        }

        private static string ResolveStarColorHex(int starLevel)
        {
            switch (Mathf.Clamp(starLevel, 1, 5))
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
    }
}