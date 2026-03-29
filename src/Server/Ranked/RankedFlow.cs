using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private static void RefreshRankedParticipantsFromLiveState()
        {
            try
            {
                if (!TryGetEligiblePlayers(out var liveParticipants, out _)) return;
                if (liveParticipants == null || liveParticipants.Count == 0) return;

                var merged = new Dictionary<ulong, RankedParticipant>();
                foreach (var participant in rankedParticipants)
                {
                    if (participant == null) continue;
                    if (participant.clientId == 0) continue;
                    merged[participant.clientId] = participant;
                }

                foreach (var participant in liveParticipants)
                {
                    if (participant == null) continue;
                    if (participant.clientId == 0) continue;
                    merged[participant.clientId] = participant;
                }

                if (merged.Count > 0)
                {
                    rankedParticipants = merged.Values.ToList();
                    lock (draftLock)
                    {
                        foreach (var participant in rankedParticipants)
                        {
                            var participantKey = ResolveParticipantIdToKey(participant);
                            if (!string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam))
                            {
                                participant.team = assignedTeam;
                            }
                        }
                    }
                    Debug.Log($"[{Constants.MOD_NAME}] Ranked participants refreshed before MMR. Count={rankedParticipants.Count}");
                }

                lock (dummyLock)
                {
                    foreach (var dummy in activeDraftDummies.Values)
                    {
                        if (dummy == null) continue;
                        var dummyParticipant = CreateParticipantFromDummy(dummy);
                        if (dummyParticipant == null) continue;
                        if (rankedParticipants.Any(p => string.Equals(ResolveParticipantIdToKey(p), dummyParticipant.playerId, StringComparison.OrdinalIgnoreCase))) continue;
                        rankedParticipants.Add(dummyParticipant);
                    }
                }
            }
            catch { }
        }

        private static bool TryStartCaptainDraft(List<RankedParticipant> participants, bool forcedByAdmin)
        {
            try
            {
                if (participants == null) participants = new List<RankedParticipant>();

                var combinedParticipants = new List<RankedParticipant>(participants.Select(CloneParticipant).Where(p => p != null));
                combinedParticipants.AddRange(ConsumeQueuedDummyParticipants());
                EnsureDraftPoolHasMinimumPlayers(combinedParticipants, 2);
                if (combinedParticipants.Count < 2) return false;
                rankedParticipants = combinedParticipants.Select(CloneParticipant).Where(p => p != null).ToList();

                var pool = combinedParticipants
                    .Where(p => p != null && (p.clientId != 0 || p.isDummy))
                    .Where(p => p != null && !string.IsNullOrEmpty(ResolveParticipantIdToKey(p)))
                    .GroupBy(p => ResolveParticipantIdToKey(p), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (pool.Count < 2) return false;

                var realCaptainPool = pool
                    .Where(p => p != null)
                    .Where(p => !IsDummyParticipant(p))
                    .Where(p =>
                    {
                        var key = ResolveParticipantIdToKey(p);
                        return !string.IsNullOrEmpty(key) && !BotManager.IsBotKey(key);
                    })
                    .ToList();

                RankedParticipant captainProxy = null;
                if (realCaptainPool.Count < 2)
                {
                    if (realCaptainPool.Count != 1) return false;

                    var singlePlayerCaptain = realCaptainPool[0];
                    captainProxy = CreateSinglePlayerDummyCaptain();
                    if (captainProxy == null) return false;

                    combinedParticipants.Add(captainProxy);
                    rankedParticipants = combinedParticipants.Select(CloneParticipant).Where(p => p != null).ToList();
                    pool.Add(captainProxy);

                    var singlePlayerName = singlePlayerCaptain.displayName ?? "Player";
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Ranked</color> auto-added bot captain for {singlePlayerName}.</size>");
                }

                RankedParticipant redCaptain = null;
                RankedParticipant blueCaptain = null;
                if (controlledRankedTestModeActive)
                {
                    var forcedCaptain = realCaptainPool.FirstOrDefault(p =>
                    {
                        if (p == null) return false;
                        if (rankedVoteStartedByClientId != 0 && p.clientId == rankedVoteStartedByClientId) return true;

                        var participantKey = ResolveParticipantIdToKey(p);
                        return !string.IsNullOrEmpty(participantKey)
                            && !string.IsNullOrEmpty(rankedVoteStartedByKey)
                            && string.Equals(participantKey, rankedVoteStartedByKey, StringComparison.OrdinalIgnoreCase);
                    }) ?? realCaptainPool.FirstOrDefault();

                    if (forcedCaptain != null)
                    {
                        redCaptain = forcedCaptain;
                        var forcedCaptainKey = ResolveParticipantIdToKey(forcedCaptain);
                        Debug.Log($"[{Constants.MOD_NAME}] User forced as captain: {forcedCaptain.displayName} ({forcedCaptainKey ?? forcedCaptain.clientId.ToString()})");

                        if (captainProxy != null)
                        {
                            blueCaptain = captainProxy;
                        }
                        else
                        {
                            blueCaptain = pool
                                .Where(p => p != null)
                                .FirstOrDefault(p =>
                                {
                                    var key = ResolveParticipantIdToKey(p);
                                    return !string.IsNullOrEmpty(key) && !string.Equals(key, forcedCaptainKey, StringComparison.OrdinalIgnoreCase);
                                });
                        }
                    }
                }

                if (redCaptain == null || blueCaptain == null)
                {
                    if (realCaptainPool.Count == 1)
                    {
                        redCaptain = realCaptainPool[0];
                        blueCaptain = captainProxy ?? pool
                            .Where(p => p != null)
                            .FirstOrDefault(p =>
                            {
                                var key = ResolveParticipantIdToKey(p);
                                if (string.IsNullOrEmpty(key)) return false;
                                return !string.Equals(key, ResolveParticipantIdToKey(redCaptain), StringComparison.OrdinalIgnoreCase);
                            });
                        if (blueCaptain == null) return false;
                    }
                    else
                    {
                        var rng = new System.Random();
                        var shuffled = realCaptainPool.OrderBy(_ => rng.Next()).ToList();
                        redCaptain = shuffled[0];
                        blueCaptain = shuffled[1];
                    }
                }
                var redCaptainKey = ResolveParticipantIdToKey(redCaptain);
                var blueCaptainKey = ResolveParticipantIdToKey(blueCaptain);
                if (string.IsNullOrEmpty(redCaptainKey) || string.IsNullOrEmpty(blueCaptainKey)) return false;

                lock (draftLock)
                {
                    draftActive = true;
                    draftTeamLockActive = true;
                    redCaptainId = redCaptainKey;
                    blueCaptainId = blueCaptainKey;
                    currentCaptainTurnId = redCaptainKey;
                    draftAvailablePlayerIds = pool
                        .Select(ResolveParticipantIdToKey)
                        .Where(pid => !string.IsNullOrEmpty(pid) && !string.Equals(pid, redCaptainKey, StringComparison.OrdinalIgnoreCase) && !string.Equals(pid, blueCaptainKey, StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    draftAssignedTeams.Clear();
                    pendingLateJoiners.Clear();
                    announcedLateJoinerIds.Clear();
                    draftAssignedTeams[redCaptainKey] = TeamResult.Red;
                    draftAssignedTeams[blueCaptainKey] = TeamResult.Blue;
                }
                Debug.Log("DRAFT STARTED");

                ResetDraftAnnouncementState();
                PublishDraftOverlayState();

                ApplyDraftTeamsToParticipants();
                ForceApplyDraftTeam(redCaptainKey, TeamResult.Red);
                ForceApplyDraftTeam(blueCaptainKey, TeamResult.Blue);

                var intro = forcedByAdmin
                    ? "<size=14><color=#00ff00>Ranked</color> forced by admin. Starting captain draft.</size>"
                    : "<size=14><color=#00ff00>Ranked</color> vote accepted. Starting captain draft.</size>";
                SendSystemChatToAll(intro);

                if (!draftAvailablePlayerIds.Any())
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Draft completion condition met: no available players");
                    Debug.Log($"[{Constants.MOD_NAME}] Draft completed — starting match");
                    CompleteDraftAndStartMatch();
                    return true;
                }

                AnnounceDraftTurn();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] TryStartCaptainDraft failed: {ex.Message}");
            }

            return false;
        }

        private static RankedParticipant CloneParticipant(RankedParticipant participant)
        {
            if (participant == null) return null;
            return new RankedParticipant
            {
                clientId = participant.clientId,
                playerId = participant.playerId,
                displayName = participant.displayName,
                team = participant.team,
                isDummy = participant.isDummy
            };
        }

        private static void EnsureDraftPoolHasMinimumPlayers(List<RankedParticipant> combinedParticipants, int minimumPlayers)
        {
            try
            {
                if (combinedParticipants == null) return;
                if (minimumPlayers < 2) minimumPlayers = 2;

                while (combinedParticipants.Count < minimumPlayers)
                {
                    var redCount = combinedParticipants.Count(p => p != null && p.team == TeamResult.Red);
                    var blueCount = combinedParticipants.Count(p => p != null && p.team == TeamResult.Blue);
                    var preferredTeam = redCount <= blueCount ? TeamResult.Red : TeamResult.Blue;

                    var botParticipant = SpawnTrackedBotParticipant(preferredTeam, false);
                    if (botParticipant == null) break;
                    combinedParticipants.Add(botParticipant);
                }
            }
            catch { }
        }

        private static RankedParticipant CreateSinglePlayerDummyCaptain()
        {
            try
            {
                string captainName;
                lock (dummyLock)
                {
                    captainName = $"DummyCaptain{nextDummySequence}";
                    nextDummySequence++;
                }

                return SpawnTrackedBotParticipant(TeamResult.Blue, false, captainName);
            }
            catch { }

            return null;
        }

        private static void ApplyDraftTeamsToParticipants()
        {
            lock (draftLock)
            {
                foreach (var participant in rankedParticipants)
                {
                    if (participant == null) continue;
                    var participantKey = ResolveParticipantIdToKey(participant);
                    if (!string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam))
                    {
                        participant.team = assignedTeam;
                    }
                }
            }
        }

        private static void HandleDraftPick(object player, ulong clientId, string rawTarget)
        {
            try
            {
                if (!draftActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> is not active.</size>", clientId);
                    return;
                }

                var actorKey = ResolvePlayerObjectKey(player, clientId) ?? $"clientId:{clientId}";
                if (string.IsNullOrEmpty(actorKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify your player.</size>", clientId);
                    return;
                }

                TeamResult captainTeam;
                var usingSoloDummyProxy = false;
                if (!TryGetCaptainTeam(actorKey, out captainTeam))
                {
                    if (!CanUseSoloDummyCaptainProxy(player, clientId) || !TryGetCurrentCaptainTeam(out captainTeam))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> only captains can use /pick.</size>", clientId);
                        return;
                    }
                    usingSoloDummyProxy = true;
                }

                if (!usingSoloDummyProxy && !string.Equals(currentCaptainTurnId, actorKey, StringComparison.OrdinalIgnoreCase))
                {
                    var currentCaptainName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "the other captain";
                    SendSystemChatToClient($"<size=14><color=#ff6666>Draft</color> it is not your turn. Current turn: {currentCaptainName}.</size>", clientId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(rawTarget))
                {
                    SendSystemChatToClient("<size=14>Usage: /pick <player></size>", clientId);
                    return;
                }

                List<RankedParticipant> availablePlayers;
                lock (draftLock)
                {
                    availablePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAvailablePlayerIds.Contains(participantKey, StringComparer.OrdinalIgnoreCase);
                        })
                        .Select(CloneParticipant)
                        .ToList();
                }

                if (!TryResolveParticipantFromCommand(rawTarget, availablePlayers, out var pickedParticipant))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> player not found in the available draft pool.</size>", clientId);
                    return;
                }

                var pickedKey = ResolveParticipantIdToKey(pickedParticipant);
                if (string.IsNullOrEmpty(pickedKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify that player.</size>", clientId);
                    return;
                }

                lock (draftLock)
                {
                    if (!draftAvailablePlayerIds.RemoveAll(id => string.Equals(id, pickedKey, StringComparison.OrdinalIgnoreCase)).Equals(0))
                    {
                        draftAssignedTeams[pickedKey] = captainTeam;
                    }
                    else
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> that player was already picked.</size>", clientId);
                        return;
                    }

                    var turnOwnerKey = usingSoloDummyProxy ? currentCaptainTurnId : actorKey;
                    currentCaptainTurnId = string.Equals(turnOwnerKey, redCaptainId, StringComparison.OrdinalIgnoreCase) ? blueCaptainId : redCaptainId;

                    if (!controlledRankedTestModeActive && draftAvailablePlayerIds.Count == 1)
                    {
                        var lastKey = draftAvailablePlayerIds[0];
                        draftAvailablePlayerIds.Clear();

                        var redCount = draftAssignedTeams.Count(kv => kv.Value == TeamResult.Red);
                        var blueCount = draftAssignedTeams.Count(kv => kv.Value == TeamResult.Blue);
                        var fallbackTeam = redCount <= blueCount ? TeamResult.Red : TeamResult.Blue;
                        draftAssignedTeams[lastKey] = fallbackTeam;
                    }

                    EnsureAllDraftParticipantsAssignedLocked();
                }

                ApplyDraftTeamsToParticipants();
                ForceApplyDraftTeam(pickedKey, captainTeam);
                var actingCaptainName = usingSoloDummyProxy ? GetCaptainDisplayNameByKey(string.Equals(captainTeam, TeamResult.Red) ? redCaptainId : blueCaptainId) : GetCaptainDisplayNameByKey(actorKey);
                SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> {actingCaptainName ?? "Captain"} picked <b>{pickedParticipant.displayName}</b> for {FormatTeamLabel(captainTeam)}.</size>");
                CompleteDraftIfReadyOrAnnounceTurn();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDraftPick failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> /pick failed.</size>", clientId);
            }
        }

        private static void HandleLateJoinAcceptance(object player, ulong clientId, string rawTarget)
        {
            try
            {
                if (!rankedActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there is no active ranked.</size>", clientId);
                    return;
                }

                if (!draftActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> /accept is only available while the captain draft is active.</size>", clientId);
                    return;
                }

                var actorKey = ResolvePlayerObjectKey(player, clientId) ?? $"clientId:{clientId}";
                if (string.IsNullOrEmpty(actorKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify your player.</size>", clientId);
                    return;
                }

                TeamResult captainTeam;
                var usingSoloDummyProxy = false;
                if (!TryGetCaptainTeam(actorKey, out captainTeam))
                {
                    if (!CanUseSoloDummyCaptainProxy(player, clientId) || !TryGetCurrentCaptainTeam(out captainTeam))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> only captains can use /accept.</size>", clientId);
                        return;
                    }
                    usingSoloDummyProxy = true;
                }

                if (string.IsNullOrWhiteSpace(rawTarget))
                {
                    SendSystemChatToClient("<size=14>Usage: /accept <player></size>", clientId);
                    return;
                }

                List<RankedParticipant> pendingPlayers;
                lock (draftLock)
                {
                    pendingPlayers = pendingLateJoiners.Values.Select(CloneParticipant).ToList();
                }

                if (!pendingPlayers.Any())
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there are no pending late joiners.</size>", clientId);
                    return;
                }

                if (!TryResolveParticipantFromCommand(rawTarget, pendingPlayers, out var acceptedParticipant))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> pending player not found.</size>", clientId);
                    return;
                }

                var acceptedKey = ResolveParticipantIdToKey(acceptedParticipant);
                if (string.IsNullOrEmpty(acceptedKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify that player.</size>", clientId);
                    return;
                }

                lock (draftLock)
                {
                    if (!pendingLateJoiners.Remove(acceptedKey))
                    {
                        SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> that player is no longer pending.</size>", clientId);
                        return;
                    }

                    draftAssignedTeams[acceptedKey] = captainTeam;
                    announcedLateJoinerIds.Remove(acceptedKey);
                }

                MergeOrReplaceParticipant(acceptedParticipant, captainTeam);
                if (!TryApplyOfficialTeamJoin(acceptedKey, acceptedParticipant.clientId, captainTeam))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> late join acceptance failed to move that player into the team.</size>", clientId);
                    lock (draftLock)
                    {
                        pendingLateJoiners[acceptedKey] = acceptedParticipant;
                        announcedLateJoinerIds.Remove(acceptedKey);
                        draftAssignedTeams.Remove(acceptedKey);
                    }
                    PublishDraftOverlayState();
                    return;
                }
                PublishDraftOverlayState();
                var acceptingCaptainName = usingSoloDummyProxy ? GetCaptainDisplayNameByKey(string.Equals(captainTeam, TeamResult.Red) ? redCaptainId : blueCaptainId) : GetCaptainDisplayNameByKey(actorKey);
                SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> {acceptingCaptainName ?? "Captain"} accepted late joiner <b>{acceptedParticipant.displayName}</b> for {FormatTeamLabel(captainTeam)}.</size>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleLateJoinAcceptance failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> /accept failed.</size>", clientId);
            }
        }

        private static void MergeOrReplaceParticipant(RankedParticipant participant, TeamResult team)
        {
            if (participant == null) return;
            participant.team = team;
            var participantKey = ResolveParticipantIdToKey(participant);
            if (string.IsNullOrEmpty(participantKey)) return;

            if (participant.isDummy)
            {
                lock (dummyLock)
                {
                    if (activeDraftDummies.TryGetValue(participantKey, out var dummy)) dummy.team = team;
                }

                if (team != TeamResult.Unknown)
                {
                    BotManager.AssignBotTeam(participantKey, team);
                }
            }

            var replaced = false;
            for (var i = 0; i < rankedParticipants.Count; i++)
            {
                var existing = rankedParticipants[i];
                if (existing == null) continue;
                var existingKey = ResolveParticipantIdToKey(existing);
                if (!string.Equals(existingKey, participantKey, StringComparison.OrdinalIgnoreCase)) continue;
                rankedParticipants[i] = participant;
                replaced = true;
                break;
            }

            if (!replaced)
            {
                rankedParticipants.Add(participant);
            }
        }

        private static void CompleteDraftIfReadyOrAnnounceTurn()
        {
            var shouldFinalize = false;
            var loggedNoAvailablePlayers = false;
            lock (draftLock)
            {
                if (!draftActive) return;

                EnsureAllDraftParticipantsAssignedLocked();

                var noAvailablePlayers = draftAvailablePlayerIds.Count == 0;
                var completionConditionMet = IsDraftCompletionConditionMetLocked();
                var staleTurnWithNoAvailablePlayers = noAvailablePlayers
                    && pendingLateJoiners.Count == 0
                    && !string.IsNullOrWhiteSpace(currentCaptainTurnId);

                if (completionConditionMet || staleTurnWithNoAvailablePlayers)
                {
                    shouldFinalize = true;
                    loggedNoAvailablePlayers = noAvailablePlayers;
                }
            }

            if (shouldFinalize)
            {
                if (loggedNoAvailablePlayers)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] Draft completion condition met: no available players");
                }

                Debug.Log($"[{Constants.MOD_NAME}] Draft completed — starting match");
                CompleteDraftAndStartMatch();
                return;
            }

            AnnounceDraftTurn();
        }

        private static void CompleteDraftAndStartMatch()
        {
            lock (draftLock)
            {
                if (!draftActive) return;
                EnsureAllDraftParticipantsAssignedLocked();
                currentCaptainTurnId = null;
                draftActive = false;
                draftTeamLockActive = false;
            }
            Debug.Log("DRAFT COMPLETED");

            ForceApplyAllDraftTeams();
            ApplyDraftTeamsToParticipants();
            EnsureDraftBotPositions();
            PublishDraftOverlayState();
            SendDraftTeamSummary();
            SendSystemChatToAll("<size=14><color=#00ff00>Ranked match started.</color></size>");
            TryStartMatch();
        }

        private static void EnsureDraftBotPositions()
        {
            try
            {
                List<KeyValuePair<string, TeamResult>> botAssignments;
                lock (draftLock)
                {
                    botAssignments = draftAssignedTeams
                        .Where(kv => BotManager.IsBotKey(kv.Key) && (kv.Value == TeamResult.Red || kv.Value == TeamResult.Blue))
                        .ToList();
                }

                if (botAssignments.Count == 0) return;

                var usedPositionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assignment in botAssignments)
                {
                    BotManager.AssignBotTeam(assignment.Key, assignment.Value);

                    var assigned = BotManager.EnsureBotSafePosition(assignment.Key, assignment.Value, usedPositionKeys);
                    if (!assigned)
                    {
                        BotManager.EnsureBotSafePosition(assignment.Key, assignment.Value, usedPositionKeys);
                    }
                }
            }
            catch { }
        }

        private static void EnsureAllDraftParticipantsAssignedLocked()
        {
            if (draftAssignedTeams == null) return;
            if (rankedParticipants == null) return;

            var redCount = draftAssignedTeams.Count(kv => kv.Value == TeamResult.Red);
            var blueCount = draftAssignedTeams.Count(kv => kv.Value == TeamResult.Blue);

            foreach (var participant in rankedParticipants)
            {
                if (participant == null) continue;
                var key = ResolveParticipantIdToKey(participant);
                if (string.IsNullOrEmpty(key)) continue;

                if (draftAssignedTeams.TryGetValue(key, out var team) && (team == TeamResult.Red || team == TeamResult.Blue))
                {
                    continue;
                }

                var assignedTeam = redCount <= blueCount ? TeamResult.Red : TeamResult.Blue;
                draftAssignedTeams[key] = assignedTeam;
                participant.team = assignedTeam;
                if (assignedTeam == TeamResult.Red) redCount++;
                else if (assignedTeam == TeamResult.Blue) blueCount++;
            }
        }

        private static bool IsDraftCompletionConditionMetLocked()
        {
            if (!draftActive) return false;
            if (draftAvailablePlayerIds.Count != 0) return false;
            if (pendingLateJoiners.Count != 0) return false;
            if (rankedParticipants == null || rankedParticipants.Count == 0) return false;

            foreach (var participant in rankedParticipants)
            {
                if (participant == null) continue;

                var participantKey = ResolveParticipantIdToKey(participant);
                if (string.IsNullOrWhiteSpace(participantKey)) continue;

                if (!draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam)) return false;
                if (assignedTeam != TeamResult.Red && assignedTeam != TeamResult.Blue) return false;
            }

            return true;
        }

        private static void AnnounceDraftTurn()
        {
            string captainName;
            string turnId;
            int remaining;
            string availableSignature;
            lock (draftLock)
            {
                if (!draftActive) return;
                turnId = currentCaptainTurnId ?? string.Empty;
                captainName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Captain";
                remaining = draftAvailablePlayerIds.Count;
                availableSignature = BuildDraftAvailablePlayersSignatureLocked();
            }

            var now = Time.unscaledTime;
            var sameAnnouncement = string.Equals(lastAnnouncedTurnId, turnId, StringComparison.OrdinalIgnoreCase)
                && lastAnnouncedAvailableCount == remaining
                && string.Equals(lastAnnouncedAvailablePlayersSignature, availableSignature, StringComparison.OrdinalIgnoreCase);
            if (sameAnnouncement && now - lastDraftTurnAnnouncementTime < DraftAnnouncementMinInterval) return;

            lastAnnouncedTurnId = turnId;
            lastAnnouncedAvailableCount = remaining;
            lastAnnouncedAvailablePlayersSignature = availableSignature;
            lastDraftTurnAnnouncementTime = now;

            PublishDraftOverlayState();
        }

        private static void SendDraftStatusToClient(ulong clientId)
        {
            try
            {
                bool isDraftActive;
                string turnName;
                int availableCount;
                int pendingCount;
                lock (draftLock)
                {
                    isDraftActive = draftActive;
                    turnName = GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "none";
                    availableCount = draftAvailablePlayerIds.Count;
                    pendingCount = pendingLateJoiners.Count;
                }

                if (!rankedActive)
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there is no active ranked.</size>", clientId);
                    return;
                }

                if (!isDraftActive)
                {
                    SendSystemChatToClient(BuildDraftOverlayFallbackMessage(true, false), clientId);
                    return;
                }

                SendSystemChatToClient(BuildDraftOverlayFallbackMessage(false, true), clientId);
            }
            catch { }
        }

        private static void DetectLateJoiners()
        {
            try
            {
                if (!ShouldPollDraftPlayerChanges()) return;
                if (!draftActive) return;

                var players = GetAllPlayers();
                if (players == null || players.Count == 0) return;
                var newlyAnnouncedLateJoiners = new List<RankedParticipant>();

                foreach (var player in players)
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                    var playerKey = ResolveParticipantIdToKey(snapshot);
                    if (string.IsNullOrEmpty(playerKey)) continue;
                    if (IsKnownRankedParticipant(snapshot)) continue;

                    if (!snapshot.isDummy && TryReplaceBotWithLateJoiner(snapshot, out var replacedBotName))
                    {
                        SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> replaced bot <b>{replacedBotName}</b> with real player <b>{snapshot.displayName}</b>.</size>");
                        PublishDraftOverlayState();
                        continue;
                    }

                    lock (draftLock)
                    {
                        pendingLateJoiners[playerKey] = snapshot;
                        if (announcedLateJoinerIds.Add(playerKey))
                        {
                            TryMovePlayerToNeutralState(snapshot);
                            newlyAnnouncedLateJoiners.Add(CloneParticipant(snapshot));
                        }
                    }
                }

                if (newlyAnnouncedLateJoiners.Count > 0)
                {
                    var names = string.Join(", ", newlyAnnouncedLateJoiners.Select(p => p.displayName).Where(n => !string.IsNullOrEmpty(n)));
                    PublishDraftOverlayState();
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> late joiner{(newlyAnnouncedLateJoiners.Count == 1 ? string.Empty : "s")} waiting for captain approval: <b>{names}</b>. Captains can use <b>/accept player</b> or click the scoreboard.</size>");
                }
            }
            catch { }
        }

        private static bool TryReplaceBotWithLateJoiner(RankedParticipant lateJoiner, out string replacedBotName)
        {
            replacedBotName = null;

            if (lateJoiner == null || IsDummyParticipant(lateJoiner)) return false;
            var lateJoinerKey = ResolveParticipantIdToKey(lateJoiner);
            if (string.IsNullOrEmpty(lateJoinerKey)) return false;

            string botKey = null;
            RankedParticipant botParticipant = null;
            TeamResult botAssignedTeam = TeamResult.Unknown;
            var botWasAvailable = false;
            var botWasPending = false;

            lock (draftLock)
            {
                botParticipant = rankedParticipants
                    .Where(p => p != null && IsDummyParticipant(p))
                    .Where(p =>
                    {
                        var key = ResolveParticipantIdToKey(p);
                        if (string.IsNullOrEmpty(key)) return false;
                        if (string.Equals(key, redCaptainId, StringComparison.OrdinalIgnoreCase)) return false;
                        if (string.Equals(key, blueCaptainId, StringComparison.OrdinalIgnoreCase)) return false;
                        return true;
                    })
                    .OrderByDescending(p =>
                    {
                        var key = ResolveParticipantIdToKey(p);
                        if (string.IsNullOrEmpty(key)) return false;
                        return draftAvailablePlayerIds.Contains(key, StringComparer.OrdinalIgnoreCase);
                    })
                    .FirstOrDefault();

                if (botParticipant == null) return false;

                botKey = ResolveParticipantIdToKey(botParticipant);
                if (string.IsNullOrEmpty(botKey)) return false;

                replacedBotName = botParticipant.displayName ?? botKey;

                if (draftAssignedTeams.TryGetValue(botKey, out var existingAssignedTeam))
                {
                    botAssignedTeam = existingAssignedTeam;
                    draftAssignedTeams.Remove(botKey);
                }

                botWasAvailable = draftAvailablePlayerIds.RemoveAll(id => string.Equals(id, botKey, StringComparison.OrdinalIgnoreCase)) > 0;
                botWasPending = pendingLateJoiners.Remove(botKey);
                announcedLateJoinerIds.Remove(botKey);

                rankedParticipants.RemoveAll(p =>
                {
                    var key = ResolveParticipantIdToKey(p);
                    return string.Equals(key, botKey, StringComparison.OrdinalIgnoreCase);
                });

                if (botWasAvailable && !draftAvailablePlayerIds.Contains(lateJoinerKey, StringComparer.OrdinalIgnoreCase))
                {
                    draftAvailablePlayerIds.Add(lateJoinerKey);
                }

                if (botAssignedTeam != TeamResult.Unknown)
                {
                    draftAssignedTeams[lateJoinerKey] = botAssignedTeam;
                }
                else if (botWasPending || !botWasAvailable)
                {
                    pendingLateJoiners[lateJoinerKey] = lateJoiner;
                    announcedLateJoinerIds.Remove(lateJoinerKey);
                }
            }

            lock (dummyLock)
            {
                activeDraftDummies.Remove(botKey);
                queuedDraftDummies.Remove(botKey);
            }

            BotManager.RemoveBot(botKey);

            MergeOrReplaceParticipant(lateJoiner, botAssignedTeam);
            if (botAssignedTeam != TeamResult.Unknown)
            {
                ForceApplyDraftTeam(lateJoinerKey, botAssignedTeam);
            }

            return true;
        }

        private static void RemoveDisconnectedDraftCandidates()
        {
            try
            {
                if (!ShouldPollDraftPlayerChanges()) return;
                if (!draftActive) return;

                var connectedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var connectedClientIds = new HashSet<ulong>();
                foreach (var player in GetAllPlayers())
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                    var connectedKey = ResolveParticipantIdToKey(snapshot);
                    if (!string.IsNullOrEmpty(connectedKey)) connectedIds.Add(connectedKey);
                    if (snapshot.clientId != 0) connectedClientIds.Add(snapshot.clientId);
                }

                if (connectedIds.Count == 0 && connectedClientIds.Count == 0) return;

                List<string> removedNames = null;
                var draftPoolChanged = false;
                var pendingPoolChanged = false;
                lock (draftLock)
                {
                    if (draftActive)
                    {
                        removedNames = new List<string>();
                        for (var i = draftAvailablePlayerIds.Count - 1; i >= 0; i--)
                        {
                            var candidateId = draftAvailablePlayerIds[i];
                            if (IsDummyKey(candidateId)) continue;
                            if (connectedIds.Contains(candidateId)) continue;
                            if (TryGetParticipantByKey(candidateId, out var connectedCandidate) && connectedCandidate.clientId != 0 && connectedClientIds.Contains(connectedCandidate.clientId)) continue;
                            removedNames.Add(GetParticipantDisplayNameByKey(candidateId) ?? candidateId);
                            draftAvailablePlayerIds.RemoveAt(i);
                            draftPoolChanged = true;
                        }
                    }

                    var stalePending = pendingLateJoiners
                        .Where(entry =>
                        {
                            if (IsDummyKey(entry.Key)) return false;
                            if (connectedIds.Contains(entry.Key)) return false;
                            var pendingParticipant = entry.Value;
                            return pendingParticipant == null || pendingParticipant.clientId == 0 || !connectedClientIds.Contains(pendingParticipant.clientId);
                        })
                        .Select(entry => entry.Key)
                        .ToList();
                    foreach (var pendingId in stalePending)
                    {
                        pendingLateJoiners.Remove(pendingId);
                        announcedLateJoinerIds.Remove(pendingId);
                    }
                    pendingPoolChanged = stalePending.Count > 0;
                }

                if (removedNames != null)
                {
                    if (removedNames.Count > 0)
                    {
                        SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> removed disconnected player{(removedNames.Count == 1 ? string.Empty : "s")} from the draft pool: <b>{string.Join(", ", removedNames)}</b>.</size>");
                    }
                }

                if (draftPoolChanged)
                {
                    CompleteDraftIfReadyOrAnnounceTurn();
                }
                else if (pendingPoolChanged)
                {
                    PublishDraftOverlayState();
                }
            }
            catch { }
        }

        private static bool TryBuildConnectedPlayerSnapshot(object player, out RankedParticipant participant)
        {
            participant = null;
            try
            {
                if (player == null) return false;
                if (!TryGetClientId(player, out var clientId) || clientId == 0) return false;

                var isBotPlayer = BotManager.TryGetBotIdByClientId(clientId, out var botKey);

                var playerId = isBotPlayer
                    ? botKey
                    : (ResolvePlayerObjectKey(player, clientId) ?? TryGetPlayerId(player, clientId));
                if (string.IsNullOrEmpty(playerId)) return false;

                var displayName = isBotPlayer
                    ? (BotManager.GetBotDisplayName(botKey) ?? TryGetPlayerName(player) ?? $"Bot {clientId}")
                    : (TryGetPlayerName(player) ?? $"Player {clientId}");
                var team = TeamResult.Unknown;
                TryGetPlayerTeam(player, out team);
                if (team == TeamResult.Unknown) TryGetPlayerTeamFromManager(clientId, out team);

                participant = new RankedParticipant
                {
                    clientId = clientId,
                    playerId = playerId,
                    displayName = displayName,
                    team = team,
                    isDummy = isBotPlayer
                };
                return true;
            }
            catch { }
            return false;
        }

        private static bool IsKnownRankedPlayer(string playerKey)
        {
            lock (draftLock)
            {
                if (draftAssignedTeams.ContainsKey(playerKey)) return true;
                if (pendingLateJoiners.ContainsKey(playerKey)) return true;
                if (draftAvailablePlayerIds.Contains(playerKey, StringComparer.OrdinalIgnoreCase)) return true;
            }

            lock (dummyLock)
            {
                if (queuedDraftDummies.ContainsKey(playerKey) || activeDraftDummies.ContainsKey(playerKey)) return true;
            }

            foreach (var participant in rankedParticipants)
            {
                if (participant == null) continue;
                var participantKey = ResolveParticipantIdToKey(participant);
                if (string.Equals(participantKey, playerKey, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static bool IsKnownRankedParticipant(RankedParticipant participant)
        {
            if (participant == null) return false;

            var participantKey = ResolveParticipantIdToKey(participant);
            if (!string.IsNullOrEmpty(participantKey) && IsKnownRankedPlayer(participantKey)) return true;

            if (participant.clientId == 0) return false;

            lock (draftLock)
            {
                if (pendingLateJoiners.Values.Any(p => p != null && p.clientId == participant.clientId)) return true;
            }

            foreach (var existingParticipant in rankedParticipants)
            {
                if (existingParticipant != null && existingParticipant.clientId == participant.clientId) return true;
            }

            return false;
        }

        private static bool TryResolveParticipantFromCommand(string rawTarget, List<RankedParticipant> candidates, out RankedParticipant participant)
        {
            participant = null;
            if (string.IsNullOrWhiteSpace(rawTarget) || candidates == null || candidates.Count == 0) return false;

            var normalizedTarget = NormalizeCommandToken(rawTarget);
            RankedParticipant containsMatch = null;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                var candidateName = NormalizeCommandToken(candidate.displayName);
                var candidateId = NormalizeCommandToken(candidate.playerId);
                var candidateClient = NormalizeCommandToken(candidate.clientId.ToString());
                if (normalizedTarget == candidateName || normalizedTarget == candidateId || normalizedTarget == candidateClient)
                {
                    participant = candidate;
                    return true;
                }

                if (!string.IsNullOrEmpty(candidateName) && candidateName.Contains(normalizedTarget))
                {
                    if (containsMatch != null) return false;
                    containsMatch = candidate;
                }
            }

            participant = containsMatch;
            return participant != null;
        }

        private static bool TryResolveDraftUiTarget(Dictionary<string, object> dict, out RankedParticipant participant)
        {
            participant = null;
            if (dict == null) return false;

            var clickedPlayer = TryGetPlayerFromDict(dict);
            if (TryBuildConnectedPlayerSnapshot(clickedPlayer, out participant)) return true;

            var clickedSteamId = TryGetSteamIdFromDict(dict);
            if (!string.IsNullOrEmpty(clickedSteamId) && TryGetParticipantByKey(clickedSteamId, out participant)) return true;

            var clickedClientId = TryGetClientIdFromDict(dict);
            if (clickedClientId != 0 && TryGetParticipantByClientId(clickedClientId, out participant)) return true;

            foreach (var value in dict.Values)
            {
                if (TryBuildConnectedPlayerSnapshot(value, out participant)) return true;
            }

            return false;
        }

        private static bool TryGetParticipantByKey(string playerKey, out RankedParticipant participant)
        {
            participant = null;
            if (string.IsNullOrEmpty(playerKey)) return false;

            lock (draftLock)
            {
                if (pendingLateJoiners.TryGetValue(playerKey, out var pending))
                {
                    participant = CloneParticipant(pending);
                    return participant != null;
                }
            }

            lock (dummyLock)
            {
                if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy))
                {
                    participant = CreateParticipantFromDummy(activeDummy);
                    return participant != null;
                }

                if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy))
                {
                    participant = CreateParticipantFromDummy(queuedDummy);
                    return participant != null;
                }
            }

            foreach (var candidate in rankedParticipants)
            {
                if (candidate == null) continue;
                var candidateKey = ResolveParticipantIdToKey(candidate);
                if (!string.Equals(candidateKey, playerKey, StringComparison.OrdinalIgnoreCase)) continue;
                participant = CloneParticipant(candidate);
                return participant != null;
            }

            foreach (var player in GetAllPlayers())
            {
                if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                var snapshotKey = ResolveParticipantIdToKey(snapshot);
                if (!string.Equals(snapshotKey, playerKey, StringComparison.OrdinalIgnoreCase)) continue;
                participant = snapshot;
                return true;
            }

            return false;
        }

        private static bool TryGetParticipantByClientId(ulong clientId, out RankedParticipant participant)
        {
            participant = null;
            if (clientId == 0) return false;

            if (BotManager.TryGetBotIdByClientId(clientId, out var botKey) && TryGetParticipantByKey(botKey, out participant))
            {
                return participant != null;
            }

            foreach (var candidate in rankedParticipants)
            {
                if (candidate == null || candidate.clientId != clientId) continue;
                participant = CloneParticipant(candidate);
                return participant != null;
            }

            lock (draftLock)
            {
                var pending = pendingLateJoiners.Values.FirstOrDefault(p => p != null && p.clientId == clientId);
                if (pending != null)
                {
                    participant = CloneParticipant(pending);
                    return true;
                }
            }

            if (TryGetPlayerByClientId(clientId, out var player) && TryBuildConnectedPlayerSnapshot(player, out participant)) return true;
            return false;
        }

        private static bool IsDraftAvailablePlayer(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return false;
            lock (draftLock)
            {
                return draftAvailablePlayerIds.Any(id => string.Equals(id, playerKey, StringComparison.OrdinalIgnoreCase));
            }
        }

        private static bool IsPendingLateJoiner(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return false;
            lock (draftLock)
            {
                return pendingLateJoiners.ContainsKey(playerKey);
            }
        }

        private static bool HasPendingLateJoiners()
        {
            lock (draftLock)
            {
                return pendingLateJoiners.Count > 0;
            }
        }

        private static string NormalizeCommandToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var stripped = StripRichTextTags(value).Trim();
            return new string(stripped.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool TryGetCaptainTeam(string playerKey, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (string.IsNullOrEmpty(playerKey)) return false;

            lock (draftLock)
            {
                if (string.Equals(playerKey, redCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Red;
                    return true;
                }

                if (string.Equals(playerKey, blueCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Blue;
                    return true;
                }
            }

            return false;
        }

        private static string GetCaptainDisplayNameByKey(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return null;
            return GetParticipantDisplayNameByKey(playerKey);
        }

        private static string GetParticipantDisplayNameByKey(string playerKey)
        {
            if (string.IsNullOrEmpty(playerKey)) return null;

            lock (draftLock)
            {
                if (pendingLateJoiners.TryGetValue(playerKey, out var pendingParticipant)) return pendingParticipant.displayName;
            }
            lock (dummyLock)
            {
                if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy)) return queuedDummy.displayName;
                if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy)) return activeDummy.displayName;
            }

            foreach (var participant in rankedParticipants)
            {
                if (participant == null) continue;
                var participantKey = ResolveParticipantIdToKey(participant);
                if (string.Equals(participantKey, playerKey, StringComparison.OrdinalIgnoreCase)) return participant.displayName;
            }

            return null;
        }

        private static void ForceApplyAllDraftTeams()
        {
            List<KeyValuePair<string, TeamResult>> assignments;
            lock (draftLock)
            {
                assignments = draftAssignedTeams.ToList();
            }

            foreach (var assignment in assignments)
            {
                ForceApplyDraftTeam(assignment.Key, assignment.Value);
            }
        }

        private static void ForceApplyDraftTeam(string playerKey, TeamResult team)
        {
            if (string.IsNullOrEmpty(playerKey) || team == TeamResult.Unknown) return;
            if (IsDummyKey(playerKey))
            {
                lock (dummyLock)
                {
                    if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy)) activeDummy.team = team;
                    if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy)) queuedDummy.team = team;
                }

                BotManager.AssignBotTeam(playerKey, team);
                return;
            }
            try
            {
                if (TryApplyOfficialTeamJoin(playerKey, 0UL, team, openPositionSelection: true))
                {
                    return;
                }

                RegisterInternalTeamAssignment(playerKey, team == TeamResult.Red ? "Red" : "Blue");
                using (BeginForcedTeamAssignment())
                {
                    if (TrySetPlayerTeamBySteamId(playerKey, team == TeamResult.Red ? "Red" : "Blue"))
                    {
                        lock (teamStateLock)
                        {
                            lastKnownPlayerTeam[playerKey] = team == TeamResult.Red ? "Red" : "Blue";
                        }
                    }
                }
            }
            catch { }
        }

        private static void TryMovePlayerToNeutralState(RankedParticipant participant)
        {
            if (participant == null) return;
            var participantKey = ResolveParticipantIdToKey(participant);
            if (string.IsNullOrEmpty(participantKey)) return;
            if (IsDummyParticipant(participant)) return;

            var neutralCandidates = new[] { "Spectator", "None", "Unknown", "Unassigned" };
            foreach (var neutralCandidate in neutralCandidates)
            {
                try
                {
                    RegisterInternalTeamAssignment(participantKey, neutralCandidate);
                    using (BeginForcedTeamAssignment())
                    {
                        if (TrySetPlayerTeamBySteamId(participantKey, neutralCandidate)) return;
                    }
                }
                catch { }
            }
        }

        private static IDisposable BeginForcedTeamAssignment()
        {
            Interlocked.Increment(ref forcedTeamAssignmentDepth);
            return new ForcedTeamAssignmentScope();
        }

        private sealed class ForcedTeamAssignmentScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Interlocked.Decrement(ref forcedTeamAssignmentDepth);
            }
        }

        private static string FormatTeamLabel(TeamResult team)
        {
            return team == TeamResult.Red ? "Red" : team == TeamResult.Blue ? "Blue" : "Unknown";
        }

        private static void SendDraftTeamSummary()
        {
            try
            {
                List<string> redPlayers;
                List<string> bluePlayers;
                lock (draftLock)
                {
                    redPlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var team) && team == TeamResult.Red;
                        })
                        .Select(p => p.displayName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    bluePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var team) && team == TeamResult.Blue;
                        })
                        .Select(p => p.displayName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }

                SendSystemChatToAll($"<size=13><color=#ff6666>Red</color>: {string.Join(", ", redPlayers)}</size>");
                SendSystemChatToAll($"<size=13><color=#66ccff>Blue</color>: {string.Join(", ", bluePlayers)}</size>");
            }
            catch { }
        }

        public static DraftOverlayState GetDraftOverlayState()
        {
            try
            {
                lock (draftLock)
                {
                    var availableParticipants = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAvailablePlayerIds.Contains(participantKey, StringComparer.OrdinalIgnoreCase);
                        })
                        .GroupBy(ResolveParticipantIdToKey, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToArray();

                    var redParticipants = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam) && assignedTeam == TeamResult.Red;
                        })
                        .GroupBy(ResolveParticipantIdToKey, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToArray();

                    var blueParticipants = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam) && assignedTeam == TeamResult.Blue;
                        })
                        .GroupBy(ResolveParticipantIdToKey, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToArray();

                    var pendingParticipants = pendingLateJoiners.Values
                        .Where(p => p != null)
                        .GroupBy(ResolveParticipantIdToKey, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToArray();

                    var availableEntries = BuildDraftOverlayEntries(availableParticipants, TeamResult.Unknown, null, sortByMmrDescending: true, captainFirst: false);
                    var redEntries = BuildDraftOverlayEntries(redParticipants, TeamResult.Red, redCaptainId, sortByMmrDescending: true, captainFirst: true);
                    var blueEntries = BuildDraftOverlayEntries(blueParticipants, TeamResult.Blue, blueCaptainId, sortByMmrDescending: true, captainFirst: true);
                    var pendingEntries = BuildDraftOverlayEntries(pendingParticipants, TeamResult.Unknown, null, sortByMmrDescending: true, captainFirst: false);

                    var completionConditionMet = draftActive && IsDraftCompletionConditionMetLocked();
                    var isCompleted = completionConditionMet || (rankedActive && !draftActive && (redEntries.Length > 0 || blueEntries.Length > 0));
                    var isVisible = draftActive;
                    if (!isVisible)
                    {
                        return new DraftOverlayState { IsVisible = false };
                    }

                    return new DraftOverlayState
                    {
                        IsVisible = true,
                        IsCompleted = isCompleted,
                        Title = "RANKED MATCH SETUP",
                        RedCaptainName = GetCaptainDisplayNameByKey(redCaptainId) ?? "Pending",
                        BlueCaptainName = GetCaptainDisplayNameByKey(blueCaptainId) ?? "Pending",
                        CurrentTurnName = isCompleted ? string.Empty : (draftActive ? (GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Pending") : string.Empty),
                        AvailablePlayers = availableEntries.Select(entry => entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        AvailablePlayerEntries = availableEntries,
                        RedPlayers = redEntries.Select(entry => entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        RedPlayerEntries = redEntries,
                        BluePlayers = blueEntries.Select(entry => entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        BluePlayerEntries = blueEntries,
                        PendingLateJoinerCount = pendingLateJoiners.Count,
                        PendingLateJoiners = pendingEntries.Select(entry => entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        PendingLateJoinerEntries = pendingEntries,
                        DummyModeActive = rankedParticipants.Any(IsDummyParticipant),
                        FooterText = isCompleted
                            ? "Draft complete. Starting match..."
                            : "Click a player to send /pick or /accept. Scoreboard click still works as a fallback."
                    };
                }
            }
            catch { }

            return new DraftOverlayState { IsVisible = false };
        }

        private static DraftOverlayPlayerEntryMessage[] BuildDraftOverlayEntries(IEnumerable<RankedParticipant> participants, TeamResult fallbackTeam, string captainKey, bool sortByMmrDescending, bool captainFirst)
        {
            var entries = (participants ?? Enumerable.Empty<RankedParticipant>())
                .Where(participant => participant != null && !string.IsNullOrWhiteSpace(participant.displayName))
                .Select(participant => BuildDraftOverlayEntry(participant, fallbackTeam, captainKey))
                .Where(entry => entry != null)
                .ToList();

            IOrderedEnumerable<DraftOverlayPlayerEntryMessage> orderedEntries;
            if (captainFirst)
            {
                orderedEntries = entries
                    .OrderByDescending(entry => entry.IsCaptain)
                    .ThenByDescending(entry => entry.HasMmr)
                    .ThenByDescending(entry => entry.HasMmr ? entry.Mmr : int.MinValue)
                    .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
            }
            else if (sortByMmrDescending)
            {
                orderedEntries = entries
                    .OrderByDescending(entry => entry.HasMmr)
                    .ThenByDescending(entry => entry.HasMmr ? entry.Mmr : int.MinValue)
                    .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                orderedEntries = entries.OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase);
            }

            return orderedEntries.ToArray();
        }

        private static DraftOverlayPlayerEntryMessage BuildDraftOverlayEntry(RankedParticipant participant, TeamResult fallbackTeam, string captainKey)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.displayName))
            {
                return null;
            }

            var participantKey = ResolveParticipantIdToKey(participant);
            var effectiveTeam = participant.team != TeamResult.Unknown ? participant.team : fallbackTeam;
            var isCaptain = !string.IsNullOrWhiteSpace(captainKey)
                && !string.IsNullOrWhiteSpace(participantKey)
                && string.Equals(participantKey, captainKey, StringComparison.OrdinalIgnoreCase);
            var hasMmr = TryGetMmrValue(participantKey, out var mmrValue);

            return new DraftOverlayPlayerEntryMessage
            {
                CommandTarget = participant.displayName,
                DisplayName = participant.displayName,
                HasMmr = hasMmr,
                Mmr = hasMmr ? mmrValue : 0,
                IsCaptain = isCaptain,
                Team = effectiveTeam
            };
        }

        private static void PublishDraftOverlayState()
        {
            try
            {
                var state = GetDraftOverlayState();
                var playerCount = (state.AvailablePlayers?.Length ?? 0)
                    + (state.RedPlayers?.Length ?? 0)
                    + (state.BluePlayers?.Length ?? 0)
                    + (state.PendingLateJoiners?.Length ?? 0);
                Debug.Log($"[{Constants.MOD_NAME}] Sending draft state with {playerCount} players");
                RankedOverlayNetwork.PublishDraftState(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] PublishDraftOverlayState failed: {ex.Message}");
            }
        }
    }
}
