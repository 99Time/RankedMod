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
            RefreshRankedParticipantsFromLiveState();

            var scoreboardStats = CaptureScoreboardStats();
            var computations = rankedParticipants
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
                Players = orderedPlayers
            };
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
            var goals = scoreboard != null && scoreboard.HasGoals
                ? scoreboard.Goals
                : GetTrackedGoalsForParticipant(participant, resolvedId);
            var assists = scoreboard != null && scoreboard.HasAssists ? scoreboard.Assists : 0;
            var saves = scoreboard != null && scoreboard.HasSaves ? scoreboard.Saves : 0;
            var shots = scoreboard != null && scoreboard.HasShots ? scoreboard.Shots : 0;
            var username = ResolvePreferredParticipantDisplayName(participant, scoreboard?.DisplayName, resolvedId);
            var mmrBefore = GetAuthoritativeMmrOrDefault(participant, resolvedId, out var canonicalMmrKey);
            if (!string.IsNullOrWhiteSpace(canonicalMmrKey))
            {
                resolvedId = canonicalMmrKey;
            }
            var performanceScore = ComputePerformanceScore(goals, assists, saves, shots, team, winner);

            return new MatchResultComputation
            {
                Participant = participant,
                PerformanceScore = performanceScore,
                Message = new MatchResultPlayerMessage
                {
                    Id = resolvedId,
                    Username = username,
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
            lock (playerGoalLock)
            {
                if (!string.IsNullOrWhiteSpace(resolvedId) && playerGoalCounts.TryGetValue(resolvedId, out var resolvedGoals))
                {
                    return resolvedGoals;
                }

                if (!string.IsNullOrWhiteSpace(participant?.playerId) && playerGoalCounts.TryGetValue(participant.playerId, out var playerGoals))
                {
                    return playerGoals;
                }
            }

            return 0;
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
            return (goals * 5) + (assists * 3) + (saves * 3) + shots + winBonus;
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
                if (participant == null || IsDummyParticipant(participant))
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
                var performanceModifier = ComputePerformanceModifier(computation, computations.Where(item => item?.Message?.Team == team).ToList());
                var requestedDelta = baseDelta + performanceModifier;
                var appliedDelta = ApplyPersistentMmrDelta(computation.Message.Id, requestedDelta, didWin);

                computation.Message.MmrDelta = appliedDelta;
                computation.Message.MmrAfter = computation.Message.MmrBefore + appliedDelta;
            }

            SaveMmr();
            Debug.Log($"[{Constants.MOD_NAME}] MMR applied. Winner={winner} Players={computations.Count(computation => computation?.Message != null)}");
        }

        private static int GetTeamAverageMmr(IEnumerable<MatchResultComputation> computations, TeamResult team)
        {
            var teamPlayers = (computations ?? Enumerable.Empty<MatchResultComputation>())
                .Where(computation => computation?.Message != null && computation.Message.Team == team)
                .Select(computation => computation.Message.MmrBefore)
                .ToArray();

            if (teamPlayers.Length == 0)
            {
                return Constants.DEFAULT_MMR;
            }

            return Mathf.RoundToInt((float)teamPlayers.Average());
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

        private static int ApplyPersistentMmrDelta(string resolvedId, int requestedDelta, bool didWin)
        {
            if (string.IsNullOrWhiteSpace(resolvedId))
            {
                return 0;
            }

            lock (mmrLock)
            {
                if (!mmrFile.players.TryGetValue(resolvedId, out var entry))
                {
                    entry = new MmrEntry();
                    mmrFile.players[resolvedId] = entry;
                }

                var oldMmr = entry.mmr;
                var newMmr = Math.Max(0, oldMmr + requestedDelta);
                entry.mmr = newMmr;
                if (didWin)
                {
                    entry.wins++;
                }
                else
                {
                    entry.losses++;
                }
                entry.lastUpdated = DateTime.UtcNow.ToString("o");
                return newMmr - oldMmr;
            }
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