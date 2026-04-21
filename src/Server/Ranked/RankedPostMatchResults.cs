using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private const double MatchResultMmrKFactor = 32d;
        private const int MatchResultPerformanceModifierCap = 4;
        private const int MatchResultPerformanceDivider = 4;
        private const int MatchResultWinBonusScore = 2;
        private static readonly string[] ScoreboardSaveNames = { "Saves", "saves", "SaveCount", "SavesCount", "SavesText", "GoalieSaves", "Blocks" };
        private static readonly string[] ScoreboardShotNames = { "Shots", "shots", "ShotCount", "ShotsCount", "ShotsText", "ShotsOnGoal", "TotalShots" };

        private sealed class MatchResultComputation
        {
            public RankedParticipant Participant;
            public MatchResultPlayerMessage Message;
            public int PerformanceScore;
            public int PerformanceModifier;
            public bool WasLateJoiner;
            public bool ExcludedFromMmr;
        }

        private sealed class ScoreboardPlayerStatsSnapshot
        {
            public string ResolvedId;
            public ulong ClientId;
            public string DisplayName;
            public TeamResult Team;
            public int Goals;
            public int Assists;
            public int Saves;
            public int Shots;
            public bool HasGoals;
            public bool HasAssists;
            public bool HasSaves;
            public bool HasShots;
        }

        private static MatchResultMessage BuildMatchResultMessage(TeamResult winner)
        {
            var usePublicPresentation = IsPublicServerMode(GetBackendConfig());
            var finalParticipants = ResolveFinalMatchResultParticipants();

            var scoreboardStats = CaptureScoreboardStats();
            var computations = finalParticipants
                .Where(participant => participant != null)
                .Select(participant => BuildMatchResultComputation(participant, scoreboardStats, winner))
                .Where(computation => computation != null)
                .ToList();

            if (computations.Count == 0)
            {
                return MatchResultMessage.Hidden();
            }

            MarkTeamMvps(computations, winner);
            ApplyMmrResults(computations, winner);

            var orderedPlayers = computations
                .OrderBy(computation => TeamSortOrder(computation.Message.Team))
                .ThenByDescending(computation => computation.Message.IsMVP)
                .ThenByDescending(computation => computation.PerformanceScore)
                .ThenByDescending(computation => computation.Message.MmrBefore)
                .ThenBy(computation => computation.Message.Username ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(computation => computation.Message.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(computation => computation.Message)
                .ToArray();

            return new MatchResultMessage
            {
                IsVisible = true,
                WinningTeam = winner,
                UsePublicPresentation = usePublicPresentation,
                Players = orderedPlayers
            };
        }

        private static List<RankedParticipant> ResolveFinalMatchResultParticipants()
        {
            var finalParticipantsByKey = new Dictionary<string, RankedParticipant>(StringComparer.OrdinalIgnoreCase);
            var liveKeyByClientId = new Dictionary<ulong, string>();

            foreach (var livePlayer in GetAllPlayers())
            {
                if (!TryBuildConnectedPlayerSnapshot(livePlayer, out var liveParticipant) || liveParticipant == null)
                {
                    continue;
                }

                var identityKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(liveParticipant) ?? liveParticipant.playerId);
                if (string.IsNullOrWhiteSpace(identityKey))
                {
                    continue;
                }

                liveKeyByClientId[liveParticipant.clientId] = identityKey;
                var previousTeam = ResolveHistoricalParticipantTeamForLogs(identityKey, liveParticipant.clientId);
                var exists = finalParticipantsByKey.TryGetValue(identityKey, out var existingParticipant);
                var shouldReplace = !exists || ShouldReplaceFinalMatchResultParticipant(existingParticipant, liveParticipant);
                Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH-DEBUG] identity={identityKey} currentTeam={liveParticipant.team} previousTeam={previousTeam} exists={exists.ToString().ToLowerInvariant()} action={(exists ? (shouldReplace ? "replace" : "ignore") : "add")}");
                if (shouldReplace)
                {
                    finalParticipantsByKey[identityKey] = CloneParticipant(liveParticipant);
                }
            }

            lock (rankedLock)
            {
                foreach (var historicalParticipant in rankedParticipants.Where(participant => participant != null))
                {
                    var historicalKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(historicalParticipant) ?? historicalParticipant.playerId);
                    if (string.IsNullOrWhiteSpace(historicalKey))
                    {
                        continue;
                    }

                    if (historicalParticipant.isDummy || BotManager.IsBotKey(historicalKey) || IsDummyKey(historicalKey))
                    {
                        if (!finalParticipantsByKey.ContainsKey(historicalKey))
                        {
                            finalParticipantsByKey[historicalKey] = CloneParticipant(historicalParticipant);
                            Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH-DEBUG] identity={historicalKey} currentTeam={historicalParticipant.team} previousTeam={historicalParticipant.team} exists=false action=retain-bot");
                        }

                        continue;
                    }

                    if (historicalParticipant.clientId != 0 && liveKeyByClientId.TryGetValue(historicalParticipant.clientId, out var reboundKey))
                    {
                        if (!string.Equals(historicalKey, reboundKey, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH-DEBUG] identity={historicalKey} currentTeam={historicalParticipant.team} previousTeam={historicalParticipant.team} exists=true action=ignore-rebased replacedBy={reboundKey}");
                        }

                        continue;
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH-DEBUG] disconnected identity={historicalKey} team={historicalParticipant.team} excludedFromResults=true excludedFromMmr=true");
                }
            }

            return OrderParticipantsForDeterminism(finalParticipantsByKey.Values);
        }

        private static TeamResult ResolveHistoricalParticipantTeamForLogs(string identityKey, ulong clientId)
        {
            lock (rankedLock)
            {
                foreach (var participant in rankedParticipants.Where(participant => participant != null))
                {
                    var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant.playerId);
                    if (!string.IsNullOrWhiteSpace(identityKey)
                        && !string.IsNullOrWhiteSpace(participantKey)
                        && string.Equals(participantKey, identityKey, StringComparison.OrdinalIgnoreCase))
                    {
                        return participant.team;
                    }

                    if (clientId != 0 && participant.clientId == clientId)
                    {
                        return participant.team;
                    }
                }
            }

            return TeamResult.Unknown;
        }

        private static bool ShouldReplaceFinalMatchResultParticipant(RankedParticipant existingParticipant, RankedParticipant candidateParticipant)
        {
            if (candidateParticipant == null)
            {
                return false;
            }

            if (existingParticipant == null)
            {
                return true;
            }

            var existingIsPlayableTeam = existingParticipant.team == TeamResult.Red || existingParticipant.team == TeamResult.Blue;
            var candidateIsPlayableTeam = candidateParticipant.team == TeamResult.Red || candidateParticipant.team == TeamResult.Blue;
            if (candidateIsPlayableTeam != existingIsPlayableTeam)
            {
                return candidateIsPlayableTeam;
            }

            if (existingParticipant.clientId == 0 && candidateParticipant.clientId != 0)
            {
                return true;
            }

            return false;
        }

        private static MatchResultComputation BuildMatchResultComputation(RankedParticipant participant, List<ScoreboardPlayerStatsSnapshot> scoreboardStats, TeamResult winner)
        {
            if (participant == null)
            {
                return null;
            }

            var provisionalResolvedId = ResolveMatchResultParticipantId(participant);
            var scoreboard = ResolveScoreboardStatsForParticipant(participant, provisionalResolvedId, scoreboardStats);
            var resolvedId = ResolveAuthoritativeParticipantKey(participant, scoreboard, provisionalResolvedId);
            var team = participant.team != TeamResult.Unknown
                ? participant.team
                : (scoreboard != null ? scoreboard.Team : TeamResult.Unknown);
            var trackedGoals = GetTrackedGoalsForParticipant(participant, resolvedId);
            var trackedPrimaryAssists = GetTrackedPrimaryAssistsForParticipant(participant, resolvedId);
            var trackedSecondaryAssists = GetTrackedSecondaryAssistsForParticipant(participant, resolvedId);
            var trackedAssists = trackedPrimaryAssists + trackedSecondaryAssists;

            var hasAuthoritativeGoals = TryGetAuthoritativeLivePlayerStat(participant, resolvedId, "Goals", out var liveGoals);
            var hasAuthoritativeAssists = TryGetAuthoritativeLivePlayerStat(participant, resolvedId, "Assists", out var liveAssists);

            var goals = hasAuthoritativeGoals
                ? liveGoals
                : (trackedGoals > 0
                    ? trackedGoals
                    : (scoreboard != null && scoreboard.HasGoals ? scoreboard.Goals : 0));
            var assists = hasAuthoritativeAssists
                ? liveAssists
                : (trackedAssists > 0
                    ? trackedAssists
                    : (scoreboard != null && scoreboard.HasAssists ? scoreboard.Assists : 0));
            var saves = 0;
            var shots = 0;
            var username = ResolvePreferredParticipantDisplayName(participant, scoreboard?.DisplayName, resolvedId);
            var liveSteamId = ResolveParticipantSteamIdForUi(participant, resolvedId);
            var mmrBefore = GetAuthoritativeMmrOrDefault(participant, resolvedId, out var canonicalMmrKey);
            if (!string.IsNullOrWhiteSpace(canonicalMmrKey))
            {
                resolvedId = canonicalMmrKey;
            }
            var isSharedGoalie = IsSharedGoalieParticipantForResults(participant, resolvedId);
            var isGoalieParticipant = isSharedGoalie || IsGoalieParticipantForResults(participant, resolvedId);
            var excludedFromMmr = isGoalieParticipant;
            var performanceScore = ComputePerformanceScore(goals, assists, saves, shots, team, winner);
            var wasLateJoiner = WasApprovedLateJoinForCurrentMatch(participant, resolvedId);

            if (hasAuthoritativeAssists && trackedAssists > 0 && liveAssists != trackedAssists)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [STATS] Assist total mismatch detected. identity={resolvedId ?? "none"} liveAssists={liveAssists} trackedAssists={trackedAssists} primaryTracked={trackedPrimaryAssists} secondaryTracked={trackedSecondaryAssists}");
            }

            Debug.Log($"[{Constants.MOD_NAME}] [STATS] Final player totals prepared. identity={resolvedId ?? "none"} username={username ?? "none"} steamId={liveSteamId ?? "none"} team={team} goals={goals} goalSource={(hasAuthoritativeGoals ? "player-networkvar" : (trackedGoals > 0 ? "tracked-goal-events" : "scoreboard-fallback"))} assists={assists} assistSource={(hasAuthoritativeAssists ? "player-networkvar" : (trackedAssists > 0 ? "tracked-goal-events" : "scoreboard-fallback"))} primaryAssistsTracked={trackedPrimaryAssists} secondaryAssistsTracked={trackedSecondaryAssists} shots={shots} authoritativeShots=false saves={saves} authoritativeSaves=false");

            Debug.Log($"[{Constants.MOD_NAME}] [POSTMATCH-DEBUG] resultEntry identity={resolvedId ?? "none"} currentTeam={team} previousTeam={ResolveHistoricalParticipantTeamForLogs(resolvedId, participant.clientId)} steamId={liveSteamId ?? "none"} exists=false");

            return new MatchResultComputation
            {
                Participant = participant,
                PerformanceScore = performanceScore,
                WasLateJoiner = wasLateJoiner,
                ExcludedFromMmr = excludedFromMmr,
                Message = new MatchResultPlayerMessage
                {
                    Id = resolvedId,
                    SteamId = liveSteamId,
                    Username = username,
                    PlayerNumber = ResolveParticipantPlayerNumber(participant),
                    IsSharedGoalie = isSharedGoalie,
                    ExcludedFromMmr = excludedFromMmr,
                    Team = team,
                    Goals = goals,
                    Assists = assists,
                    Saves = saves,
                    Shots = shots,
                    MmrBefore = mmrBefore,
                    MmrAfter = mmrBefore,
                    MmrDelta = 0,
                    IsMVP = false
                }
            };
        }

        private static List<ScoreboardPlayerStatsSnapshot> CaptureScoreboardStats()
        {
            var snapshots = new List<ScoreboardPlayerStatsSnapshot>();
            if (!TryGetScoreboardEntries(out var entries) || entries == null)
            {
                return snapshots;
            }

            foreach (var entry in entries)
            {
                try
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    TryGetEntryStringValue(entry, ScoreboardSteamIdNames, out var rawId);
                    TryGetEntryStringValue(entry, ScoreboardNameFields, out var displayName);
                    TryGetEntryClientId(entry, out var clientId);
                    TryGetEntryTeam(entry, out var team);

                    var resolvedId = ResolveScoreboardEntryId(rawId, clientId, displayName);
                    var hasGoals = TryGetEntryIntValue(entry, ScoreboardGoalNames, out var goals);
                    var hasAssists = TryGetEntryIntValue(entry, ScoreboardAssistNames, out var assists);
                    var hasSaves = TryGetEntryIntValue(entry, ScoreboardSaveNames, out var saves);
                    var hasShots = TryGetEntryIntValue(entry, ScoreboardShotNames, out var shots);

                    snapshots.Add(new ScoreboardPlayerStatsSnapshot
                    {
                        ResolvedId = resolvedId,
                        ClientId = clientId,
                        DisplayName = displayName,
                        Team = team,
                        Goals = goals,
                        Assists = assists,
                        Saves = saves,
                        Shots = shots,
                        HasGoals = hasGoals,
                        HasAssists = hasAssists,
                        HasSaves = hasSaves,
                        HasShots = hasShots
                    });
                }
                catch { }
            }

            return snapshots;
        }

        private static ScoreboardPlayerStatsSnapshot ResolveScoreboardStatsForParticipant(RankedParticipant participant, string resolvedId, IEnumerable<ScoreboardPlayerStatsSnapshot> scoreboardStats)
        {
            var playerId = participant?.playerId;
            var displayName = participant?.displayName;
            var clientId = participant?.clientId ?? 0;

            return (scoreboardStats ?? Enumerable.Empty<ScoreboardPlayerStatsSnapshot>())
                .FirstOrDefault(snapshot =>
                    snapshot != null &&
                    ((!string.IsNullOrWhiteSpace(resolvedId) && string.Equals(snapshot.ResolvedId, resolvedId, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(playerId) && string.Equals(snapshot.ResolvedId, playerId, StringComparison.OrdinalIgnoreCase))
                    || (clientId != 0 && snapshot.ClientId == clientId)
                    || (!string.IsNullOrWhiteSpace(displayName) && string.Equals(snapshot.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))));
        }

        private static string ResolveScoreboardEntryId(string rawId, ulong clientId, string displayName)
        {
            if (!string.IsNullOrWhiteSpace(rawId))
            {
                var resolved = ResolveStoredIdToSteam(rawId);
                return !string.IsNullOrWhiteSpace(resolved) ? resolved : rawId;
            }

            if (clientId != 0)
            {
                var clientKey = $"clientId:{clientId}";
                var resolved = ResolveStoredIdToSteam(clientKey);
                return !string.IsNullOrWhiteSpace(resolved) ? resolved : clientKey;
            }

            return displayName;
        }

        private static string ResolveMatchResultParticipantId(RankedParticipant participant)
        {
            var resolvedId = ResolveParticipantIdToKey(participant);
            if (!string.IsNullOrWhiteSpace(resolvedId))
            {
                return resolvedId;
            }

            if (!string.IsNullOrWhiteSpace(participant?.playerId))
            {
                return participant.playerId;
            }

            if (!string.IsNullOrWhiteSpace(participant?.displayName))
            {
                return participant.displayName;
            }

            return $"clientId:{participant?.clientId ?? 0}";
        }

        private static string ResolveAuthoritativeParticipantKey(RankedParticipant participant, ScoreboardPlayerStatsSnapshot scoreboard, string fallbackId)
        {
            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant));
            if (!string.IsNullOrWhiteSpace(participantKey) && !IsClientIdFallbackKey(participantKey))
            {
                return participantKey;
            }

            var scoreboardKey = NormalizeResolvedPlayerKey(scoreboard?.ResolvedId);
            var scoreboardDisplayName = NormalizeVisiblePlayerName(scoreboard?.DisplayName);
            if (!string.IsNullOrWhiteSpace(scoreboardKey)
                && (string.IsNullOrWhiteSpace(scoreboardDisplayName)
                    || !string.Equals(scoreboardKey, scoreboardDisplayName, StringComparison.OrdinalIgnoreCase)))
            {
                return scoreboardKey;
            }

            if (!string.IsNullOrWhiteSpace(participantKey))
            {
                return participantKey;
            }

            var fallbackKey = NormalizeResolvedPlayerKey(fallbackId);
            if (!string.IsNullOrWhiteSpace(fallbackKey))
            {
                return fallbackKey;
            }

            return participant != null && participant.clientId != 0
                ? $"clientId:{participant.clientId}"
                : null;
        }

        private static string ResolvePreferredParticipantDisplayName(RankedParticipant participant, string scoreboardDisplayName, string resolvedId)
        {
            var participantName = NormalizeVisiblePlayerName(participant?.displayName);
            if (!string.IsNullOrWhiteSpace(participantName))
            {
                return participantName;
            }

            var scoreboardName = NormalizeVisiblePlayerName(scoreboardDisplayName);
            if (!string.IsNullOrWhiteSpace(scoreboardName))
            {
                return scoreboardName;
            }

            var liveName = TryResolveLivePlayerDisplayName(participant, resolvedId);
            if (!string.IsNullOrWhiteSpace(liveName))
            {
                return liveName;
            }

            if (participant != null && participant.clientId != 0)
            {
                return $"Player {participant.clientId}";
            }

            var fallbackName = NormalizeVisiblePlayerName(resolvedId);
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                return fallbackName;
            }

            return "Player";
        }

        private static string TryResolveLivePlayerDisplayName(RankedParticipant participant, string resolvedId)
        {
            if (participant != null && participant.clientId != 0 && TryGetPlayerByClientId(participant.clientId, out var player))
            {
                var liveName = NormalizeVisiblePlayerName(TryGetPlayerName(player));
                if (!string.IsNullOrWhiteSpace(liveName))
                {
                    return liveName;
                }
            }

            var normalizedResolvedId = NormalizeResolvedPlayerKey(resolvedId);
            if (string.IsNullOrWhiteSpace(normalizedResolvedId))
            {
                return null;
            }

            foreach (var livePlayer in GetAllPlayers())
            {
                var playerKey = ResolvePlayerObjectKey(livePlayer, 0);
                if (string.IsNullOrWhiteSpace(playerKey)
                    || !string.Equals(playerKey, normalizedResolvedId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var liveName = NormalizeVisiblePlayerName(TryGetPlayerName(livePlayer));
                if (!string.IsNullOrWhiteSpace(liveName))
                {
                    return liveName;
                }
            }

            return null;
        }

        private static string NormalizeResolvedPlayerKey(string candidate)
        {
            var clean = StripRichTextTags(candidate)?.Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return null;
            }

            var resolved = ResolveStoredIdToSteam(clean);
            return string.IsNullOrWhiteSpace(resolved) ? clean : resolved;
        }

        private static bool IsClientIdFallbackKey(string candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate)
                && candidate.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeIdentityKey(string candidate)
        {
            var clean = StripRichTextTags(candidate)?.Trim();
            if (string.IsNullOrWhiteSpace(clean))
            {
                return false;
            }

            if (clean.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase)
                || clean.StartsWith("steam:", StringComparison.OrdinalIgnoreCase)
                || clean.StartsWith("steam_", StringComparison.OrdinalIgnoreCase)
                || clean.StartsWith("bot:", StringComparison.OrdinalIgnoreCase)
                || clean.StartsWith("dummy:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (clean.IndexOf(' ') >= 0)
            {
                return false;
            }

            return clean.Length >= 6 && clean.All(char.IsDigit);
        }

        private static int GetTrackedGoalsForParticipant(RankedParticipant participant, string resolvedId)
        {
            return GetTrackedCountForParticipant(playerGoalLock, playerGoalCounts, participant, resolvedId);
        }

        private static int GetTrackedPrimaryAssistsForParticipant(RankedParticipant participant, string resolvedId)
        {
            return GetTrackedCountForParticipant(playerAssistLock, playerPrimaryAssistCounts, participant, resolvedId);
        }

        private static int GetTrackedSecondaryAssistsForParticipant(RankedParticipant participant, string resolvedId)
        {
            return GetTrackedCountForParticipant(playerAssistLock, playerSecondaryAssistCounts, participant, resolvedId);
        }

        private static int GetTrackedCountForParticipant(object syncRoot, Dictionary<string, int> counts, RankedParticipant participant, string resolvedId)
        {
            lock (syncRoot)
            {
                if (counts == null)
                {
                    return 0;
                }

                if (!string.IsNullOrWhiteSpace(resolvedId) && counts.TryGetValue(resolvedId, out var resolvedCount))
                {
                    return resolvedCount;
                }

                var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant?.playerId);
                if (!string.IsNullOrWhiteSpace(participantKey) && counts.TryGetValue(participantKey, out var participantCount))
                {
                    return participantCount;
                }

                if (!string.IsNullOrWhiteSpace(participant?.playerId) && counts.TryGetValue(participant.playerId, out var playerCount))
                {
                    return playerCount;
                }
            }

            return 0;
        }

        private static bool TryGetAuthoritativeLivePlayerStat(RankedParticipant participant, string resolvedId, string memberName, out int value)
        {
            value = 0;

            var livePlayer = TryResolveLivePlayerForParticipant(participant, resolvedId);
            return livePlayer != null
                && TryGetEntryMemberValue(livePlayer, memberName, out var memberValue)
                && TryConvertToInt(memberValue, out value);
        }

        private static object TryResolveLivePlayerForParticipant(RankedParticipant participant, string resolvedId)
        {
            if (participant != null && participant.clientId != 0 && TryGetPlayerByClientId(participant.clientId, out var playerByClientId) && playerByClientId != null)
            {
                return playerByClientId;
            }

            var normalizedResolvedId = NormalizeResolvedPlayerKey(resolvedId);
            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant?.playerId);
            foreach (var livePlayer in GetAllPlayers() ?? new List<object>())
            {
                var liveKey = NormalizeResolvedPlayerKey(ResolvePlayerObjectKey(livePlayer, 0UL));
                if ((!string.IsNullOrWhiteSpace(normalizedResolvedId) && string.Equals(liveKey, normalizedResolvedId, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(participantKey) && string.Equals(liveKey, participantKey, StringComparison.OrdinalIgnoreCase)))
                {
                    return livePlayer;
                }
            }

            return null;
        }

        private static int GetStoredParticipantMmr(string resolvedId)
        {
            if (string.IsNullOrWhiteSpace(resolvedId))
            {
                return Constants.DEFAULT_MMR;
            }

            return TryGetMmrValue(resolvedId, out var mmr)
                ? mmr
                : Constants.DEFAULT_MMR;
        }

        private static int ComputePerformanceScore(int goals, int assists, int saves, int shots, TeamResult team, TeamResult winner)
        {
            var winBonus = team != TeamResult.Unknown && team == winner ? MatchResultWinBonusScore : 0;
            return (goals * 5) + (assists * 3) + winBonus;
        }

        private static void MarkTeamMvps(List<MatchResultComputation> computations, TeamResult winner)
        {
            MarkTeamMvp(computations, TeamResult.Red, winner);
            MarkTeamMvp(computations, TeamResult.Blue, winner);
        }

        private static void MarkTeamMvp(List<MatchResultComputation> computations, TeamResult team, TeamResult winner)
        {
            var teamMvp = (computations ?? new List<MatchResultComputation>())
                .Where(computation => computation?.Message != null && computation.Message.Team == team)
                .Where(computation => !computation.Message.ExcludedFromMmr)
                .OrderByDescending(computation => computation.PerformanceScore)
                .ThenByDescending(computation => computation.Message.Goals)
                .ThenByDescending(computation => computation.Message.Assists)
                .ThenByDescending(computation => computation.Message.Saves)
                .ThenByDescending(computation => computation.Message.Shots)
                .ThenByDescending(computation => computation.Message.MmrBefore)
                .ThenBy(computation => computation.Message.Username ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(computation => computation.Message.Id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (teamMvp?.Message != null)
            {
                teamMvp.Message.IsMVP = true;
                teamMvp.PerformanceScore = ComputePerformanceScore(teamMvp.Message.Goals, teamMvp.Message.Assists, teamMvp.Message.Saves, teamMvp.Message.Shots, team, winner);
            }
        }

        private static void ApplyMmrResults(List<MatchResultComputation> computations, TeamResult winner)
        {
            var redAverageMmr = GetTeamAverageMmr(computations, TeamResult.Red);
            var blueAverageMmr = GetTeamAverageMmr(computations, TeamResult.Blue);

            foreach (var computation in computations)
            {
                if (computation?.Message == null)
                {
                    continue;
                }

                var participant = computation.Participant;
                if (participant == null || IsDummyParticipant(participant) || computation.ExcludedFromMmr)
                {
                    computation.Message.MmrAfter = computation.Message.MmrBefore;
                    computation.Message.MmrDelta = 0;
                    continue;
                }

                var team = computation.Message.Team;
                if (team == TeamResult.Unknown || winner == TeamResult.Unknown)
                {
                    computation.Message.MmrAfter = computation.Message.MmrBefore;
                    computation.Message.MmrDelta = 0;
                    continue;
                }

                var didWin = team == winner;
                var opponentAverageMmr = team == TeamResult.Red ? blueAverageMmr : redAverageMmr;
                var baseDelta = ComputeBaseMmrDelta(computation.Message.MmrBefore, opponentAverageMmr, didWin);
                var performanceModifier = ComputePerformanceModifier(computation, computations.Where(item => item?.Message?.Team == team && !item.Message.ExcludedFromMmr).ToList());
                computation.PerformanceModifier = performanceModifier;
                var requestedDelta = baseDelta + performanceModifier;
                var appliedDelta = ApplyPersistentMmrDelta(computation, requestedDelta, didWin);

                computation.Message.MmrDelta = appliedDelta;
                computation.Message.MmrAfter = computation.Message.MmrBefore + appliedDelta;
            }

            SaveMmr();
            PublishScoreboardStarState();
            Debug.Log($"[{Constants.MOD_NAME}] MMR applied. Winner={winner} Players={computations.Count(computation => computation?.Message != null)}");
        }

        private static int GetTeamAverageMmr(IEnumerable<MatchResultComputation> computations, TeamResult team)
        {
            var teamPlayers = (computations ?? Enumerable.Empty<MatchResultComputation>())
                .Where(computation => computation?.Message != null && computation.Message.Team == team && !computation.Message.ExcludedFromMmr)
                .Select(computation => computation.Message.MmrBefore)
                .ToArray();

            if (teamPlayers.Length == 0)
            {
                return Constants.DEFAULT_MMR;
            }

            return Mathf.RoundToInt((float)teamPlayers.Average());
        }

        private static bool IsSharedGoalieParticipantForResults(RankedParticipant participant, string resolvedId)
        {
            if (!singleGoalieEnabled || participant == null)
            {
                return false;
            }

            var participantKey = NormalizeResolvedPlayerKey(ResolveParticipantIdToKey(participant) ?? participant.playerId ?? resolvedId);
            var trackedKey = NormalizeResolvedPlayerKey(singleGoaliePlayerKey);
            if (!string.IsNullOrWhiteSpace(participantKey)
                && !string.IsNullOrWhiteSpace(trackedKey)
                && string.Equals(participantKey, trackedKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return participant.clientId != 0 && singleGoaliePlayerClientId != 0 && participant.clientId == singleGoaliePlayerClientId;
        }

        private static bool IsGoalieParticipantForResults(RankedParticipant participant, string resolvedId)
        {
            if (participant == null)
            {
                return false;
            }

            try
            {
                if (TryResolveConnectedPlayer(ResolveParticipantIdToKey(participant) ?? participant.playerId ?? resolvedId, participant.clientId, out var player, out _, out _)
                    && TryIsGoalie(player, out var isGoalie))
                {
                    return isGoalie;
                }
            }
            catch { }

            return false;
        }

        private static int ComputeBaseMmrDelta(int mmrBefore, int opponentAverageMmr, bool didWin)
        {
            var expectedScore = 1d / (1d + Math.Pow(10d, (opponentAverageMmr - mmrBefore) / 400d));
            var actualScore = didWin ? 1d : 0d;
            return (int)Math.Round(MatchResultMmrKFactor * (actualScore - expectedScore), MidpointRounding.AwayFromZero);
        }

        private static int ComputePerformanceModifier(MatchResultComputation computation, List<MatchResultComputation> teamComputations)
        {
            if (computation == null || teamComputations == null || teamComputations.Count <= 1)
            {
                return 0;
            }

            var teamAverageScore = teamComputations.Average(item => item?.PerformanceScore ?? 0);
            var modifier = (int)Math.Round((computation.PerformanceScore - teamAverageScore) / MatchResultPerformanceDivider, MidpointRounding.AwayFromZero);
            return Mathf.Clamp(modifier, -MatchResultPerformanceModifierCap, MatchResultPerformanceModifierCap);
        }

        private static int ApplyPersistentMmrDelta(MatchResultComputation computation, int requestedDelta, bool didWin)
        {
            var resolvedId = computation?.Message?.Id;
            if (string.IsNullOrWhiteSpace(resolvedId))
            {
                return 0;
            }

            Debug.Log($"[{Constants.MOD_NAME}] [MMR-DEBUG] identity={resolvedId} team={computation?.Message?.Team} requestedDelta={requestedDelta} didWin={didWin.ToString().ToLowerInvariant()} queued=true");

            lock (mmrLock)
            {
                if (!mmrFile.players.TryGetValue(resolvedId, out var entry))
                {
                    entry = new MmrEntry();
                    mmrFile.players[resolvedId] = entry;
                }

                var adjustedDelta = AdjustRequestedDeltaForStarSystem(resolvedId, computation, requestedDelta, didWin);
                var oldMmr = entry.mmr;
                var newMmr = Math.Max(0, oldMmr + adjustedDelta);
                entry.mmr = newMmr;
                if (didWin)
                {
                    entry.wins++;
                }
                else
                {
                    entry.losses++;
                }

                UpdateStoredStarsForPlayerKey(resolvedId, didWin, computation?.PerformanceModifier ?? 0, computation?.Message?.IsMVP ?? false, computation?.WasLateJoiner ?? false, MaxStarPoints);
                entry.lastUpdated = DateTime.UtcNow.ToString("o");
                return newMmr - oldMmr;
            }
        }

        private static int AdjustRequestedDeltaForStarSystem(string playerKey, MatchResultComputation computation, int requestedDelta, bool didWin)
        {
            if (string.IsNullOrWhiteSpace(playerKey) || didWin || requestedDelta >= 0)
            {
                return requestedDelta;
            }

            var starShield = Mathf.Min(GetStoredStarLevel(playerKey), 3);
            var performanceShield = computation != null && computation.PerformanceModifier > 0 ? 1 : 0;
            var mvpShield = computation?.Message?.IsMVP == true ? 1 : 0;
            var lateJoinShield = computation?.WasLateJoiner == true ? 2 : 0;
            var totalShield = Mathf.Clamp(starShield + performanceShield + mvpShield + lateJoinShield, 0, MaxLossStarProtection);
            var adjustedDelta = requestedDelta + totalShield;
            if (adjustedDelta < 0)
            {
                return adjustedDelta;
            }

            var qualifiesForTinyReverse = GetStoredStarLevel(playerKey) >= 4
                && (computation?.WasLateJoiner == true || computation?.Message?.IsMVP == true || (computation?.PerformanceModifier ?? 0) >= 2);
            return qualifiesForTinyReverse ? 1 : 0;
        }

        private static int TeamSortOrder(TeamResult team)
        {
            switch (team)
            {
                case TeamResult.Red:
                    return 0;
                case TeamResult.Blue:
                    return 1;
                default:
                    return 2;
            }
        }
    }
}