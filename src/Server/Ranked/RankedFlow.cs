using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private static readonly Dictionary<string, string> draftPreferredPositionKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private sealed class DraftAvailableCandidate
        {
            public string PoolKey;
            public string AuthoritativeKey;
            public RankedParticipant Participant;
        }

        private static void RefreshRankedParticipantsFromLiveState()
        {
            try
            {
                if (!TryGetEligiblePlayers(out var liveParticipants, out _)) return;
                if (liveParticipants == null || liveParticipants.Count == 0) return;

                var merged = new Dictionary<string, RankedParticipant>(StringComparer.OrdinalIgnoreCase);
                MergeRankedParticipantsByIdentity(merged, rankedParticipants, false);
                MergeRankedParticipantsByIdentity(merged, liveParticipants, true);

                if (merged.Count > 0)
                {
                    rankedParticipants = OrderParticipantsForDeterminism(merged.Values.Where(participant => participant != null));
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
            }
            catch { }
        }

        private static void MergeRankedParticipantsByIdentity(Dictionary<string, RankedParticipant> merged, IEnumerable<RankedParticipant> sourceParticipants, bool liveSource)
        {
            if (merged == null || sourceParticipants == null)
            {
                return;
            }

            foreach (var participant in sourceParticipants)
            {
                if (participant == null)
                {
                    continue;
                }

                var participantKey = ResolveRankedParticipantMergeKey(participant);
                if (string.IsNullOrWhiteSpace(participantKey))
                {
                    continue;
                }

                if (liveSource
                    && merged.TryGetValue(participantKey, out var existingParticipant)
                    && existingParticipant != null
                    && existingParticipant.clientId != 0
                    && participant.clientId != 0
                    && existingParticipant.clientId != participant.clientId)
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [RANKED-IDENTITY] Rebinding participant {participantKey} from clientId={existingParticipant.clientId} to clientId={participant.clientId}.");
                }

                merged[participantKey] = participant;
            }
        }

        private static string ResolveRankedParticipantMergeKey(RankedParticipant participant)
        {
            if (participant == null)
            {
                return null;
            }

            var resolvedKey = ResolveParticipantIdToKey(participant);
            if (!string.IsNullOrWhiteSpace(resolvedKey))
            {
                return resolvedKey;
            }

            if (!string.IsNullOrWhiteSpace(participant.playerId))
            {
                return ResolveStoredIdToSteam(participant.playerId);
            }

            return participant.clientId != 0 ? $"clientId:{participant.clientId}" : null;
        }

        private static bool TryStartCaptainDraft(List<RankedParticipant> participants, bool forcedByAdmin)
        {
            try
            {
                if (participants == null) participants = new List<RankedParticipant>();

                var combinedParticipants = OrderParticipantsForDeterminism(participants.Select(CloneParticipant).Where(p => p != null));
                if (controlledTestModeEnabled)
                {
                    EnsureControlledTestDraftParticipants(combinedParticipants);
                }

                if (combinedParticipants.Count < 2) return false;
                rankedParticipants = OrderParticipantsForDeterminism(combinedParticipants);

                var pool = combinedParticipants
                    .Where(p => p != null && (p.clientId != 0 || p.isDummy))
                    .Where(p => p != null && !string.IsNullOrEmpty(ResolveParticipantIdToKey(p)))
                    .GroupBy(p => ResolveParticipantIdToKey(p), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (pool.Count < 2) return false;

                if (!TrySelectDraftCaptains(pool, out var redCaptain, out var blueCaptain)) return false;

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
                    draftAvailablePlayerIds = OrderParticipantsForDeterminism(pool
                            .Where(participant => participant != null)
                            .Where(participant =>
                            {
                                var participantKey = ResolveParticipantIdToKey(participant);
                                return !string.IsNullOrEmpty(participantKey)
                                    && !string.Equals(participantKey, redCaptainKey, StringComparison.OrdinalIgnoreCase)
                                    && !string.Equals(participantKey, blueCaptainKey, StringComparison.OrdinalIgnoreCase);
                            }))
                        .Select(ResolveParticipantIdToKey)
                        .Where(pid => !string.IsNullOrEmpty(pid))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    draftAssignedTeams.Clear();
                    draftPreferredPositionKeys.Clear();
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
                RetryDraftAssignedPositionRestores();

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

        private static void EnsureControlledTestDraftParticipants(List<RankedParticipant> combinedParticipants)
        {
            try
            {
                if (combinedParticipants == null) return;

                RankedParticipant initiator = null;
                if (!string.IsNullOrWhiteSpace(controlledTestModeInitiatorKey))
                {
                    initiator = combinedParticipants.FirstOrDefault(participant =>
                        string.Equals(ResolveParticipantIdToKey(participant), controlledTestModeInitiatorKey, StringComparison.OrdinalIgnoreCase));
                }

                if (initiator == null && controlledTestModeInitiatorClientId != 0)
                {
                    initiator = combinedParticipants.FirstOrDefault(participant => participant != null && participant.clientId == controlledTestModeInitiatorClientId);
                }

                if (initiator == null && controlledTestModeInitiatorClientId != 0 && TryGetParticipantByClientId(controlledTestModeInitiatorClientId, out var initiatorSnapshot))
                {
                    initiator = CloneParticipant(initiatorSnapshot);
                    if (initiator != null)
                    {
                        combinedParticipants.Add(initiator);
                    }
                }

                combinedParticipants.RemoveAll(participant => participant == null);

                var humanCount = combinedParticipants.Count(participant => participant != null && !IsDummyParticipant(participant));
                if (humanCount == 1)
                {
                    var extraCaptain = CreateSinglePlayerDummyCaptain();
                    if (extraCaptain != null)
                    {
                        combinedParticipants.Add(extraCaptain);
                    }
                }

                EnsureDraftPoolHasMinimumPlayers(combinedParticipants, 6);
            }
            catch { }
        }

        private static bool TrySelectDraftCaptains(List<RankedParticipant> pool, out RankedParticipant redCaptain, out RankedParticipant blueCaptain)
        {
            redCaptain = null;
            blueCaptain = null;

            var orderedPool = OrderParticipantsForDeterminism(pool);
            var humanPool = orderedPool
                .Where(participant => participant != null && !IsDummyParticipant(participant))
                .ToList();
            if (humanPool.Count == 0) return false;

            var activeCaptainKeys = new HashSet<string>(humanPool
                .Select(ResolveParticipantIdToKey)
                .Where(key => !string.IsNullOrWhiteSpace(key)), StringComparer.OrdinalIgnoreCase);
            captainRotationQueue.RemoveAll(key => !activeCaptainKeys.Contains(key));
            if (captainRotationQueue.Count >= activeCaptainKeys.Count)
            {
                captainRotationQueue.Clear();
            }

            if (controlledTestModeEnabled)
            {
                redCaptain = ResolveForcedTestCaptain(humanPool);
            }

            if (redCaptain == null)
            {
                redCaptain = humanPool.FirstOrDefault(participant =>
                {
                    var participantKey = ResolveParticipantIdToKey(participant);
                    return !string.IsNullOrWhiteSpace(participantKey) && !captainRotationQueue.Contains(participantKey);
                }) ?? humanPool.FirstOrDefault();
            }

            if (redCaptain == null) return false;

            var redCaptainKey = ResolveParticipantIdToKey(redCaptain);
            blueCaptain = humanPool.FirstOrDefault(participant =>
            {
                var participantKey = ResolveParticipantIdToKey(participant);
                return !string.IsNullOrWhiteSpace(participantKey)
                    && !string.Equals(participantKey, redCaptainKey, StringComparison.OrdinalIgnoreCase)
                    && !captainRotationQueue.Contains(participantKey);
            });

            if (blueCaptain == null)
            {
                blueCaptain = humanPool.FirstOrDefault(participant =>
                {
                    var participantKey = ResolveParticipantIdToKey(participant);
                    return !string.IsNullOrWhiteSpace(participantKey)
                        && !string.Equals(participantKey, redCaptainKey, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (blueCaptain == null && controlledTestModeEnabled)
            {
                blueCaptain = orderedPool.FirstOrDefault(participant =>
                {
                    var participantKey = ResolveParticipantIdToKey(participant);
                    return !string.IsNullOrWhiteSpace(participantKey)
                        && !string.Equals(participantKey, redCaptainKey, StringComparison.OrdinalIgnoreCase);
                });
            }

            if (blueCaptain == null) return false;

            RememberCaptainRotation(redCaptain);
            RememberCaptainRotation(blueCaptain);
            return true;
        }

        private static RankedParticipant ResolveForcedTestCaptain(IEnumerable<RankedParticipant> candidates)
        {
            var orderedCandidates = OrderParticipantsForDeterminism(candidates);
            if (!string.IsNullOrWhiteSpace(controlledTestModeInitiatorKey))
            {
                var byKey = orderedCandidates.FirstOrDefault(participant =>
                    string.Equals(ResolveParticipantIdToKey(participant), controlledTestModeInitiatorKey, StringComparison.OrdinalIgnoreCase));
                if (byKey != null) return byKey;
            }

            if (controlledTestModeInitiatorClientId != 0)
            {
                return orderedCandidates.FirstOrDefault(participant => participant != null && participant.clientId == controlledTestModeInitiatorClientId);
            }

            return null;
        }

        private static void RememberCaptainRotation(RankedParticipant captain)
        {
            if (captain == null || IsDummyParticipant(captain)) return;

            var captainKey = ResolveParticipantIdToKey(captain);
            if (string.IsNullOrWhiteSpace(captainKey)) return;
            if (captainRotationQueue.Contains(captainKey, StringComparer.OrdinalIgnoreCase)) return;

            captainRotationQueue.Add(captainKey);
        }

        private sealed class CaptainAuthorityChange
        {
            public TeamResult Team;
            public string PreviousCaptainKey;
            public string NewCaptainKey;
        }

        private static bool EnsureValidCaptainAssignments(bool publishOverlayState, bool refreshApprovalPanels)
        {
            List<CaptainAuthorityChange> changes;
            lock (draftLock)
            {
                changes = EnsureValidCaptainAssignmentsLocked();
            }

            if (changes == null || changes.Count == 0)
            {
                return false;
            }

            foreach (var change in changes)
            {
                var captainName = GetCaptainDisplayNameByKey(change.NewCaptainKey) ?? "none";
                Debug.Log($"[{Constants.MOD_NAME}] [CAPTAIN] Reassigned to {captainName} ({change.NewCaptainKey ?? "none"}) for {FormatTeamLabel(change.Team)}.");
            }

            if (publishOverlayState)
            {
                PublishDraftOverlayState();
            }

            if (refreshApprovalPanels)
            {
                RefreshCaptainApprovalPanels();
            }

            return true;
        }

        private static List<CaptainAuthorityChange> EnsureValidCaptainAssignmentsLocked()
        {
            var changes = new List<CaptainAuthorityChange>();

            if (!rankedActive)
            {
                return changes;
            }

            var previousRedCaptain = redCaptainId;
            var previousBlueCaptain = blueCaptainId;

            redCaptainId = ResolveValidCaptainKeyLocked(TeamResult.Red, previousRedCaptain, changes);
            blueCaptainId = ResolveValidCaptainKeyLocked(TeamResult.Blue, previousBlueCaptain, changes);

            if (!string.IsNullOrWhiteSpace(currentCaptainTurnId))
            {
                if (string.Equals(currentCaptainTurnId, previousRedCaptain, StringComparison.OrdinalIgnoreCase))
                {
                    currentCaptainTurnId = !string.IsNullOrWhiteSpace(redCaptainId) ? redCaptainId : blueCaptainId;
                }
                else if (string.Equals(currentCaptainTurnId, previousBlueCaptain, StringComparison.OrdinalIgnoreCase))
                {
                    currentCaptainTurnId = !string.IsNullOrWhiteSpace(blueCaptainId) ? blueCaptainId : redCaptainId;
                }
                else if (!string.Equals(currentCaptainTurnId, redCaptainId, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(currentCaptainTurnId, blueCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    currentCaptainTurnId = !string.IsNullOrWhiteSpace(redCaptainId) ? redCaptainId : blueCaptainId;
                }
            }

            return changes;
        }

        private static string ResolveValidCaptainKeyLocked(TeamResult team, string currentCaptainKey, List<CaptainAuthorityChange> changes)
        {
            if (IsValidTeamCaptainLocked(currentCaptainKey, team))
            {
                return currentCaptainKey;
            }

            var candidateKeys = GetCaptainCandidateKeysLocked(team);
            var newCaptainKey = candidateKeys.Count == 0
                ? null
                : candidateKeys[UnityEngine.Random.Range(0, candidateKeys.Count)];

            if (!string.Equals(currentCaptainKey, newCaptainKey, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new CaptainAuthorityChange
                {
                    Team = team,
                    PreviousCaptainKey = currentCaptainKey,
                    NewCaptainKey = newCaptainKey
                });
            }

            return newCaptainKey;
        }

        private static bool IsValidTeamCaptainLocked(string captainKey, TeamResult team)
        {
            if (string.IsNullOrWhiteSpace(captainKey))
            {
                return false;
            }

            if (!draftAssignedTeams.TryGetValue(captainKey, out var assignedTeam) || assignedTeam != team)
            {
                return false;
            }

            if (draftAvailablePlayerIds.Contains(captainKey, StringComparer.OrdinalIgnoreCase) || pendingLateJoiners.ContainsKey(captainKey))
            {
                return false;
            }

            return IsConnectedCaptainCandidateLocked(captainKey);
        }

        private static List<string> GetCaptainCandidateKeysLocked(TeamResult team)
        {
            var assignedTeamKeys = draftAssignedTeams
                .Where(entry => entry.Value == team)
                .Select(entry => entry.Key)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Where(key => !draftAvailablePlayerIds.Contains(key, StringComparer.OrdinalIgnoreCase))
                .Where(key => !pendingLateJoiners.ContainsKey(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var connectedHumanKeys = assignedTeamKeys
                .Where(key => TryGetParticipantByKey(key, out var participant)
                    && participant != null
                    && !IsDummyParticipant(participant)
                    && participant.clientId != 0
                    && TryResolveConnectedPlayer(key, participant.clientId, out _, out _, out _))
                .ToList();
            if (connectedHumanKeys.Count > 0)
            {
                return connectedHumanKeys;
            }

            return assignedTeamKeys.Where(IsConnectedCaptainCandidateLocked).ToList();
        }

        private static bool IsConnectedCaptainCandidateLocked(string candidateKey)
        {
            if (string.IsNullOrWhiteSpace(candidateKey))
            {
                return false;
            }

            if (IsDummyKey(candidateKey) || BotManager.IsBotKey(candidateKey))
            {
                return TryGetParticipantByKey(candidateKey, out var dummyCandidate) && dummyCandidate != null;
            }

            if (!TryGetParticipantByKey(candidateKey, out var participant) || participant == null || participant.clientId == 0)
            {
                return false;
            }

            return TryResolveConnectedPlayer(candidateKey, participant.clientId, out _, out _, out _);
        }

        private static void EnsureDraftPoolHasMinimumPlayers(List<RankedParticipant> combinedParticipants, int minimumPlayers)
        {
            try
            {
                if (!AreSyntheticPlayersAllowed()) return;
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
                if (!AreSyntheticPlayersAllowed()) return null;
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

                EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: true);

                var actorKey = GetPlayerKey(player, clientId) ?? $"clientId:{clientId}";
                if (string.IsNullOrEmpty(actorKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify your player.</size>", clientId);
                    return;
                }

                if (!TryGetCaptainTeam(actorKey, out var captainTeam))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> only captains can use /pick.</size>", clientId);
                    return;
                }

                if (!string.Equals(currentCaptainTurnId, actorKey, StringComparison.OrdinalIgnoreCase))
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

                Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Incoming pick token = {rawTarget}");
                if (!TryResolveAvailableDraftParticipant(rawTarget, out var resolvedCandidate, out var failureReason, out var availableTargets, out var staleEntries, out var duplicateEntries))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Available authoritative targets = {string.Join(", ", availableTargets ?? Array.Empty<string>())}");
                    if (staleEntries != null && staleEntries.Length > 0)
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] duplicate/stale pool entry = {string.Join(", ", staleEntries)}");
                    }

                    if (duplicateEntries != null && duplicateEntries.Length > 0)
                    {
                        Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] duplicate/stale pool entry = {string.Join(", ", duplicateEntries)}");
                    }

                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Resolution failed: {failureReason} actor={actorKey} target={rawTarget}");
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> player not found in the available draft pool.</size>", clientId);
                    return;
                }

                var pickedParticipant = resolvedCandidate.Participant;
                var pickedKey = resolvedCandidate.AuthoritativeKey;
                Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Available authoritative targets = {string.Join(", ", availableTargets ?? Array.Empty<string>())}");
                Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Resolved participant = {pickedParticipant?.displayName ?? "unknown"} ({pickedKey ?? "none"}) clientId={pickedParticipant?.clientId ?? 0} poolKey={resolvedCandidate.PoolKey ?? "none"}");

                if (string.IsNullOrEmpty(pickedKey))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Resolution failed: token mismatch actor={actorKey} target={rawTarget}");
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify that player.</size>", clientId);
                    return;
                }

                if (!TryApplyDraftPick(actorKey, captainTeam, pickedParticipant, clientId))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Resolution failed: participant not in available pool actor={actorKey} target={rawTarget} resolved={pickedKey}");
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> that player was already picked.</size>", clientId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDraftPick failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> /pick failed.</size>", clientId);
            }
        }

        private static bool TryApplyDraftPick(string actorKey, TeamResult captainTeam, RankedParticipant pickedParticipant, ulong feedbackClientId)
        {
            if (pickedParticipant == null) return false;

            var pickedKey = ResolveParticipantIdToKey(pickedParticipant);
            if (string.IsNullOrEmpty(pickedKey)) return false;

            var removedCount = 0;
            var availableCount = 0;
            var redCount = 0;
            var blueCount = 0;

            lock (draftLock)
            {
                RemoveDuplicateDraftAvailablePlayersLocked();

                if (!draftAvailablePlayerIds.Contains(pickedKey, StringComparer.OrdinalIgnoreCase))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Attempt to pick already removed player: {pickedKey}");
                    return false;
                }

                removedCount = draftAvailablePlayerIds.RemoveAll(id => string.Equals(id, pickedKey, StringComparison.OrdinalIgnoreCase));
                if (removedCount == 0)
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Attempt to pick already removed player: {pickedKey}");
                    return false;
                }

                draftAssignedTeams[pickedKey] = captainTeam;
                pickedParticipant.team = captainTeam;
                for (var i = 0; i < rankedParticipants.Count; i++)
                {
                    var existing = rankedParticipants[i];
                    if (existing == null) continue;
                    var existingKey = ResolveParticipantIdToKey(existing);
                    if (!string.Equals(existingKey, pickedKey, StringComparison.OrdinalIgnoreCase)) continue;
                    existing.team = captainTeam;
                    rankedParticipants[i] = existing;
                    break;
                }

                currentCaptainTurnId = string.Equals(actorKey, redCaptainId, StringComparison.OrdinalIgnoreCase) ? blueCaptainId : redCaptainId;
                availableCount = draftAvailablePlayerIds.Count;
                redCount = draftAssignedTeams.Count(kv => kv.Value == TeamResult.Red);
                blueCount = draftAssignedTeams.Count(kv => kv.Value == TeamResult.Blue);
            }

            ApplyDraftTeamsToParticipants();
            ForceApplyDraftTeam(pickedKey, captainTeam);
            RetryDraftAssignedPositionRestores();
            SetJoinState(pickedKey, RankedJoinState.InTeam);

            Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Found: {pickedParticipant.displayName} ({pickedKey})");
            Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Removed from available: {pickedParticipant.displayName} ({pickedKey}) removedCount={removedCount}");
            Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Added to team: {FormatTeamLabel(captainTeam)} player={pickedParticipant.displayName} ({pickedKey})");
            Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Final counts -> Available={availableCount} Red={redCount} Blue={blueCount}");

            PublishDraftOverlayState();
            var actingCaptainName = GetCaptainDisplayNameByKey(actorKey);
            SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> {actingCaptainName ?? "Captain"} picked <b>{pickedParticipant.displayName}</b> for {FormatTeamLabel(captainTeam)}.</size>");
            CompleteDraftIfReadyOrAnnounceTurn();
            return true;
        }

        private static bool TryResolveAvailableDraftParticipant(string rawTarget, out DraftAvailableCandidate candidate, out string failureReason, out string[] availableTargets, out string[] staleEntries, out string[] duplicateEntries)
        {
            candidate = null;
            failureReason = "token mismatch";
            availableTargets = Array.Empty<string>();
            staleEntries = Array.Empty<string>();
            duplicateEntries = Array.Empty<string>();

            var trimmedTarget = StripRichTextTags(rawTarget)?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTarget))
            {
                return false;
            }

            List<DraftAvailableCandidate> candidates;
            lock (draftLock)
            {
                RemoveDuplicateDraftAvailablePlayersLocked();
                candidates = BuildAvailableDraftCandidatesLocked(out staleEntries, out duplicateEntries);
            }

            availableTargets = candidates
                .Select(entry => entry.AuthoritativeKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var matches = candidates
                .Where(entry => string.Equals(entry.AuthoritativeKey, trimmedTarget, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 1)
            {
                candidate = matches[0];
                return true;
            }

            if (matches.Count > 1)
            {
                failureReason = "duplicate/stale pool entry";
                return false;
            }

            if (candidates.Any(entry => string.Equals(entry.PoolKey, trimmedTarget, StringComparison.OrdinalIgnoreCase)))
            {
                failureReason = "scoreboard translation mismatch";
                return false;
            }

            if ((staleEntries?.Length ?? 0) > 0 || (duplicateEntries?.Length ?? 0) > 0)
            {
                failureReason = "duplicate/stale pool entry";
                return false;
            }

            failureReason = "participant not in available pool";
            return false;
        }

        private static List<DraftAvailableCandidate> BuildAvailableDraftCandidatesLocked(out string[] staleEntries, out string[] duplicateEntries)
        {
            var candidates = new List<DraftAvailableCandidate>();
            var stale = new List<string>();
            var duplicates = new List<string>();
            var seenAuthoritativeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var poolKey in draftAvailablePlayerIds ?? Enumerable.Empty<string>())
            {
                var cleanPoolKey = StripRichTextTags(poolKey)?.Trim();
                if (string.IsNullOrWhiteSpace(cleanPoolKey))
                {
                    stale.Add("<empty>");
                    continue;
                }

                if (!TryGetParticipantByKey(cleanPoolKey, out var participant) || participant == null)
                {
                    stale.Add($"{cleanPoolKey}:unresolved");
                    continue;
                }

                var authoritativeKey = ResolveParticipantIdToKey(participant) ?? participant.playerId ?? $"clientId:{participant.clientId}";
                if (string.IsNullOrWhiteSpace(authoritativeKey))
                {
                    stale.Add($"{cleanPoolKey}:missing-authoritative-key");
                    continue;
                }

                if (!seenAuthoritativeKeys.Add(authoritativeKey))
                {
                    duplicates.Add($"{cleanPoolKey}->{authoritativeKey}");
                    continue;
                }

                if (!string.Equals(cleanPoolKey, authoritativeKey, StringComparison.OrdinalIgnoreCase))
                {
                    stale.Add($"{cleanPoolKey}->{authoritativeKey}");
                }

                candidates.Add(new DraftAvailableCandidate
                {
                    PoolKey = cleanPoolKey,
                    AuthoritativeKey = authoritativeKey,
                    Participant = participant
                });
            }

            staleEntries = stale.ToArray();
            duplicateEntries = duplicates.ToArray();
            return candidates;
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

                EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: true);

                var actorKey = GetPlayerKey(player, clientId) ?? $"clientId:{clientId}";
                if (string.IsNullOrEmpty(actorKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify your player.</size>", clientId);
                    return;
                }

                if (!TryGetCaptainTeam(actorKey, out var captainTeam))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> only captains can use /accept.</size>", clientId);
                    return;
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

                Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Approval requested: actor={actorKey} target={rawTarget} pending={string.Join(", ", pendingPlayers.Select(candidate => ResolveParticipantIdToKey(candidate) ?? candidate.displayName ?? candidate.clientId.ToString()))}");

                if (!pendingPlayers.Any())
                {
                    SendSystemChatToClient("<size=14><color=#ffcc66>Draft</color> there are no pending late joiners.</size>", clientId);
                    return;
                }

                if (!TryResolveParticipantFromCommand(rawTarget, pendingPlayers, out var acceptedParticipant))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Approval target not found or ambiguous: actor={actorKey} target={rawTarget}");
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> pending player not found.</size>", clientId);
                    return;
                }

                var acceptedKey = ResolveParticipantIdToKey(acceptedParticipant);
                if (string.IsNullOrEmpty(acceptedKey))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Draft</color> could not identify that player.</size>", clientId);
                    return;
                }

                Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Found player: {acceptedParticipant.displayName} ({acceptedKey}) clientId={acceptedParticipant.clientId}");

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

                SetJoinState(acceptedKey, RankedJoinState.Approved);

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
                    SetJoinState(acceptedKey, RankedJoinState.PendingApproval);
                    PublishDraftOverlayState();
                    return;
                }

                SetJoinState(acceptedKey, RankedJoinState.InTeam);
                PublishDraftOverlayState();
                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Approved: {acceptedParticipant.displayName} ({acceptedKey}) -> {FormatTeamLabel(captainTeam)} by {actorKey}.");
                var acceptingCaptainName = GetCaptainDisplayNameByKey(actorKey);
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
            lock (draftLock)
            {
                if (!draftActive) return;

                RemoveDuplicateDraftAvailablePlayersLocked();
                var completionConditionMet = IsDraftCompletionConditionMetLocked();

                if (completionConditionMet)
                {
                    shouldFinalize = true;
                }
            }

            if (shouldFinalize)
            {
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
                currentCaptainTurnId = null;
                draftActive = false;
                draftTeamLockActive = false;
            }
            Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Draft completed.");

            ForceApplyAllDraftTeams();
            RetryDraftAssignedPositionRestores();
            EnsureDraftBotPositions();
            ApplyDraftTeamsToParticipants();
            PruneInvalidLiveRankedParticipants();
            Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Teams finalized.");
            MarkRankedMatchLiveStarted();
            PublishDraftOverlayState();
            PublishScoreboardStarState();
            SendDraftTeamSummary();
            SendSystemChatToAll("<size=14><color=#00ff00>Ranked match started.</color></size>");
            Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Starting ranked match directly.");
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
            if (TryProcessAutomaticDraftTurn())
            {
                return;
            }

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

        private static bool TryProcessAutomaticDraftTurn()
        {
            try
            {
                string autoCaptainId;
                string autoPickTargetId;
                lock (draftLock)
                {
                    if (!draftActive) return false;
                    autoCaptainId = currentCaptainTurnId;
                    if (string.IsNullOrWhiteSpace(autoCaptainId)) return false;
                    if (!IsDummyKey(autoCaptainId) && !BotManager.IsBotKey(autoCaptainId)) return false;
                    if (draftAvailablePlayerIds.Count == 0) return false;

                    autoPickTargetId = draftAvailablePlayerIds[0];
                }

                if (!TryGetCaptainTeam(autoCaptainId, out var captainTeam)) return false;
                if (!TryGetParticipantByKey(autoPickTargetId, out var pickedParticipant) || pickedParticipant == null) return false;

                return TryApplyDraftPick(autoCaptainId, captainTeam, pickedParticipant, 0UL);
            }
            catch { }

            return false;
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

                    lock (draftLock)
                    {
                        pendingLateJoiners[playerKey] = snapshot;
                        if (announcedLateJoinerIds.Add(playerKey))
                        {
                            SetJoinState(playerKey, RankedJoinState.PendingApproval);
                            Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Player requested join: {snapshot.displayName} ({playerKey})");
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
                List<string> removedAssignedIds = null;
                List<string> removedAssignedNames = null;
                var draftPoolChanged = false;
                var pendingPoolChanged = false;
                var assignedPoolChanged = false;
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
                            rankedParticipants.RemoveAll(participant => string.Equals(ResolveParticipantIdToKey(participant), candidateId, StringComparison.OrdinalIgnoreCase));
                            ClearJoinState(candidateId);
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
                        ClearJoinState(pendingId);
                    }
                    pendingPoolChanged = stalePending.Count > 0;

                    removedAssignedIds = draftAssignedTeams
                        .Where(entry => entry.Value == TeamResult.Red || entry.Value == TeamResult.Blue)
                        .Where(entry => !IsDummyKey(entry.Key) && !BotManager.IsBotKey(entry.Key))
                        .Where(entry => !connectedIds.Contains(entry.Key))
                        .Where(entry =>
                        {
                            if (TryGetParticipantByKey(entry.Key, out var connectedParticipant) && connectedParticipant != null && connectedParticipant.clientId != 0)
                            {
                                return !connectedClientIds.Contains(connectedParticipant.clientId);
                            }

                            return true;
                        })
                        .Select(entry => entry.Key)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    removedAssignedNames = removedAssignedIds
                        .Select(key => GetParticipantDisplayNameByKey(key) ?? key)
                        .ToList();

                    foreach (var assignedKey in removedAssignedIds)
                    {
                        draftAssignedTeams.Remove(assignedKey);
                        pendingLateJoiners.Remove(assignedKey);
                        announcedLateJoinerIds.Remove(assignedKey);
                        rankedParticipants.RemoveAll(participant => string.Equals(ResolveParticipantIdToKey(participant), assignedKey, StringComparison.OrdinalIgnoreCase));
                        ClearJoinState(assignedKey);
                        assignedPoolChanged = true;
                    }
                }

                var captainChanged = (draftPoolChanged || assignedPoolChanged)
                    && EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: true);

                if (removedNames != null)
                {
                    if (removedNames.Count > 0)
                    {
                        SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> removed disconnected player{(removedNames.Count == 1 ? string.Empty : "s")} from the draft pool: <b>{string.Join(", ", removedNames)}</b>.</size>");
                    }
                }

                if (removedAssignedNames != null && removedAssignedNames.Count > 0)
                {
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Draft</color> removed disconnected team player{(removedAssignedNames.Count == 1 ? string.Empty : "s")} from the ranked draft: <b>{string.Join(", ", removedAssignedNames)}</b>.</size>");
                }

                if (draftPoolChanged || assignedPoolChanged)
                {
                    CompleteDraftIfReadyOrAnnounceTurn();
                }
                else if (pendingPoolChanged || captainChanged)
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

                var playerKey = TryGetPlayerIdNoFallback(player);
                if (ShouldIgnoreTransientTeamHookPlayer(player, clientId, playerKey)) return false;

                var isBotPlayer = BotManager.TryGetBotIdByClientId(clientId, out var botKey);
                if (isBotPlayer) return false;

                var playerId = GetPlayerKey(player, clientId) ?? TryGetPlayerId(player, clientId);
                if (string.IsNullOrEmpty(playerId)) return false;

                var displayName = TryGetPlayerName(player) ?? $"Player {clientId}";
                var team = TeamResult.Unknown;
                TryGetPlayerTeam(player, out team);
                if (team == TeamResult.Unknown) TryGetPlayerTeamFromManager(clientId, out team);

                participant = new RankedParticipant
                {
                    clientId = clientId,
                    playerId = playerId,
                    displayName = displayName,
                    team = team,
                    isDummy = false
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

            var trimmedTarget = StripRichTextTags(rawTarget)?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedTarget)) return false;

            var matches = GetDistinctParticipantMatches(candidates, candidate => ParticipantMatchesStableDraftIdentity(candidate, trimmedTarget));
            if (matches.Count == 1)
            {
                participant = matches[0];
                return true;
            }

            if (matches.Count > 1)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Ambiguous exact identity target '{trimmedTarget}': {string.Join(", ", matches.Select(match => ResolveParticipantIdToKey(match) ?? match.playerId ?? $"clientId:{match.clientId}"))}");
            }

            return false;
        }

        private static bool ParticipantMatchesStableDraftIdentity(RankedParticipant candidate, string rawTarget)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(rawTarget))
            {
                return false;
            }

            var participantKey = ResolveParticipantIdToKey(candidate);
            if (string.IsNullOrWhiteSpace(participantKey))
            {
                return false;
            }

            return string.Equals(participantKey, rawTarget, StringComparison.OrdinalIgnoreCase);
        }

        private static List<RankedParticipant> GetDistinctParticipantMatches(IEnumerable<RankedParticipant> candidates, Func<RankedParticipant, bool> predicate)
        {
            return (candidates ?? Enumerable.Empty<RankedParticipant>())
                .Where(candidate => candidate != null && predicate(candidate))
                .GroupBy(candidate => ResolveParticipantIdToKey(candidate) ?? candidate.playerId ?? $"clientId:{candidate.clientId}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<string> EnumerateParticipantIdentityTokens(RankedParticipant candidate)
        {
            var participantKey = ResolveParticipantIdToKey(candidate);
            if (!string.IsNullOrWhiteSpace(participantKey))
            {
                yield return participantKey;
            }

            if (!string.IsNullOrWhiteSpace(candidate?.playerId))
            {
                yield return candidate.playerId;
            }

            if (candidate != null && candidate.clientId != 0)
            {
                yield return $"clientId:{candidate.clientId}";
                yield return candidate.clientId.ToString();
            }
        }

        private static bool TryParseClientIdFallbackKey(string candidateKey, out ulong clientId)
        {
            clientId = 0;
            var clean = StripRichTextTags(candidateKey)?.Trim();
            if (string.IsNullOrWhiteSpace(clean) || !clean.StartsWith("clientId:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ulong.TryParse(clean.Substring("clientId:".Length), out clientId) && clientId != 0;
        }

        private static string BuildStableDraftCommandTarget(string participantKey)
        {
            if (string.IsNullOrWhiteSpace(participantKey))
            {
                return null;
            }

            return StripRichTextTags(participantKey)?.Trim();
        }

        private static int RemoveDuplicateDraftAvailablePlayersLocked()
        {
            if (draftAvailablePlayerIds == null || draftAvailablePlayerIds.Count <= 1)
            {
                return 0;
            }

            var removed = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = draftAvailablePlayerIds.Count - 1; index >= 0; index--)
            {
                var playerKey = draftAvailablePlayerIds[index];
                if (string.IsNullOrWhiteSpace(playerKey) || !seen.Add(playerKey))
                {
                    draftAvailablePlayerIds.RemoveAt(index);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Removed duplicate available entries: {removed}");
            }

            return removed;
        }

        private static bool TryResolveDraftUiTarget(Dictionary<string, object> dict, out RankedParticipant participant)
        {
            participant = null;
            if (dict == null) return false;

            var clickedPlayer = TryGetPlayerFromDict(dict);
            if (TryBuildConnectedPlayerSnapshot(clickedPlayer, out participant)) return true;

            if (clickedPlayer != null)
            {
                if (TryGetClientId(clickedPlayer, out var clickedPlayerClientId)
                    && clickedPlayerClientId != 0
                    && TryGetParticipantByClientId(clickedPlayerClientId, out participant))
                {
                    return participant != null;
                }

                var clickedPlayerKey = ResolvePlayerObjectKey(clickedPlayer, 0UL) ?? TryGetPlayerIdNoFallback(clickedPlayer);
                if (!string.IsNullOrWhiteSpace(clickedPlayerKey) && TryGetParticipantByKey(clickedPlayerKey, out participant))
                {
                    return participant != null;
                }
            }

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

            if (TryParseClientIdFallbackKey(playerKey, out var clientId)
                && BotManager.TryGetBotIdByClientId(clientId, out var fallbackBotKey)
                && BotManager.TryGetBotParticipant(fallbackBotKey, out var fallbackBotParticipant))
            {
                participant = CloneParticipant(fallbackBotParticipant);
                return participant != null;
            }

            if (BotManager.IsBotKey(playerKey) && BotManager.TryGetBotParticipant(playerKey, out var botParticipant))
            {
                participant = CloneParticipant(botParticipant);
                return participant != null;
            }

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

            if (BotManager.IsBotKey(playerKey))
            {
                return BotManager.GetBotDisplayName(playerKey);
            }

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
            if (IsDummyKey(playerKey) || BotManager.IsBotKey(playerKey))
            {
                if (IsDummyKey(playerKey))
                {
                    lock (dummyLock)
                    {
                        if (activeDraftDummies.TryGetValue(playerKey, out var activeDummy)) activeDummy.team = team;
                        if (queuedDraftDummies.TryGetValue(playerKey, out var queuedDummy)) queuedDummy.team = team;
                    }
                }

                if (BotManager.AssignBotTeam(playerKey, team))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Applied bot team assignment: {playerKey} -> {FormatTeamLabel(team)}");
                }
                else if (BotManager.IsBotKey(playerKey))
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [DRAFT] Bot team assignment failed: {playerKey} -> {FormatTeamLabel(team)}");
                }

                return;
            }
            try
            {
                string preferredPositionKey = null;
                if (TryResolveConnectedPlayer(playerKey, 0UL, out var player, out _, out _))
                {
                    preferredPositionKey = TryGetCurrentPositionKey(player);
                    if (!string.IsNullOrWhiteSpace(preferredPositionKey))
                    {
                        lock (draftLock)
                        {
                            draftPreferredPositionKeys[playerKey] = preferredPositionKey;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(preferredPositionKey))
                {
                    lock (draftLock)
                    {
                        draftPreferredPositionKeys.TryGetValue(playerKey, out preferredPositionKey);
                    }
                }

                if (TryApplyOfficialTeamJoin(playerKey, 0UL, team, openPositionSelection: false, preferredPositionKey: preferredPositionKey))
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

        private static void RetryDraftAssignedPositionRestores()
        {
            List<KeyValuePair<string, TeamResult>> assignments;
            Dictionary<string, string> preferredPositions;

            lock (draftLock)
            {
                assignments = draftAssignedTeams.ToList();
                preferredPositions = new Dictionary<string, string>(draftPreferredPositionKeys, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var assignment in assignments)
            {
                if (string.IsNullOrWhiteSpace(assignment.Key)
                    || assignment.Value == TeamResult.Unknown
                    || IsDummyKey(assignment.Key)
                    || BotManager.IsBotKey(assignment.Key)
                    || !preferredPositions.TryGetValue(assignment.Key, out var preferredPositionKey)
                    || string.IsNullOrWhiteSpace(preferredPositionKey))
                {
                    continue;
                }

                try
                {
                    if (!TryResolveConnectedPlayer(assignment.Key, 0UL, out var player, out _, out _)
                        || player == null)
                    {
                        continue;
                    }

                    var currentPositionKey = TryGetCurrentPositionKey(player);
                    var normalizedPositionKey = NormalizePositionKeyForTeam(preferredPositionKey, assignment.Value);
                    if (string.IsNullOrWhiteSpace(normalizedPositionKey))
                    {
                        continue;
                    }

                    if (string.Equals(currentPositionKey, normalizedPositionKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var runtimeTeamType = FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
                    if (runtimeTeamType == null || !runtimeTeamType.IsEnum)
                    {
                        continue;
                    }

                    var runtimeTeamValue = Enum.Parse(runtimeTeamType, assignment.Value == TeamResult.Red ? "Red" : "Blue", true);
                    if (TryClaimPlayerPositionByKey(player, runtimeTeamValue, normalizedPositionKey))
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [DRAFT] Restored position {normalizedPositionKey} for {assignment.Key} after draft team updates.");
                    }
                }
                catch { }
            }
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
                RefreshRankedParticipantsFromLiveState();
                EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: false);

                lock (draftLock)
                {
                    RemoveDuplicateDraftAvailablePlayersLocked();

                    var availableParticipants = draftAvailablePlayerIds
                        .Select(playerId =>
                        {
                            TryGetParticipantByKey(playerId, out var participant);
                            return participant;
                        })
                        .Where(participant => participant != null)
                        .ToArray();

                    var redParticipants = draftAssignedTeams
                        .Where(entry => entry.Value == TeamResult.Red)
                        .Select(entry =>
                        {
                            TryGetParticipantByKey(entry.Key, out var participant);
                            return participant;
                        })
                        .Where(participant => participant != null)
                        .ToArray();

                    var blueParticipants = draftAssignedTeams
                        .Where(entry => entry.Value == TeamResult.Blue)
                        .Select(entry =>
                        {
                            TryGetParticipantByKey(entry.Key, out var participant);
                            return participant;
                        })
                        .Where(participant => participant != null)
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

                    ulong currentTurnClientId = 0;
                    string currentTurnSteamId = null;
                    if (!isCompleted
                        && draftActive
                        && !string.IsNullOrWhiteSpace(currentCaptainTurnId)
                        && TryGetParticipantByKey(currentCaptainTurnId, out var currentTurnParticipant)
                        && currentTurnParticipant != null)
                    {
                        var currentTurnEntry = BuildDraftOverlayEntry(currentTurnParticipant, TeamResult.Unknown, currentCaptainTurnId);
                        if (currentTurnEntry != null)
                        {
                            currentTurnClientId = currentTurnEntry.ClientId;
                            currentTurnSteamId = currentTurnEntry.SteamId;
                        }
                    }

                    return new DraftOverlayState
                    {
                        IsVisible = true,
                        IsCompleted = isCompleted,
                        Title = "RANKED MATCH SETUP",
                        RedCaptainName = GetCaptainDisplayNameByKey(redCaptainId) ?? "Pending",
                        BlueCaptainName = GetCaptainDisplayNameByKey(blueCaptainId) ?? "Pending",
                        CurrentTurnName = isCompleted ? string.Empty : (draftActive ? (GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Pending") : string.Empty),
                        CurrentTurnClientId = currentTurnClientId,
                        CurrentTurnSteamId = currentTurnSteamId,
                        AvailablePlayers = availableEntries.Select(entry => entry.DisplayName ?? entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        AvailablePlayerEntries = availableEntries,
                        RedPlayers = redEntries.Select(entry => entry.DisplayName ?? entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        RedPlayerEntries = redEntries,
                        BluePlayers = blueEntries.Select(entry => entry.DisplayName ?? entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        BluePlayerEntries = blueEntries,
                        PendingLateJoinerCount = pendingLateJoiners.Count,
                        PendingLateJoiners = pendingEntries.Select(entry => entry.DisplayName ?? entry.CommandTarget).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray(),
                        PendingLateJoinerEntries = pendingEntries,
                        DummyModeActive = controlledTestModeEnabled,
                        FooterText = isCompleted
                            ? "Draft complete. Starting match..."
                            : "Select a player to add them to your team."
                    };
                }
            }
            catch { }

            return new DraftOverlayState { IsVisible = false };
        }

        private static DraftOverlayPlayerEntryMessage[] BuildDraftOverlayEntries(IEnumerable<RankedParticipant> participants, TeamResult fallbackTeam, string captainKey, bool sortByMmrDescending, bool captainFirst)
        {
            var entries = (participants ?? Enumerable.Empty<RankedParticipant>())
                .Where(participant => participant != null)
                .Select(participant => BuildDraftOverlayEntry(participant, fallbackTeam, captainKey))
                .Where(entry => entry != null)
                .GroupBy(entry => entry.CommandTarget ?? entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
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
            if (participant == null)
            {
                return null;
            }

            var sourceParticipant = ResolveDraftOverlaySourceParticipant(participant) ?? CloneParticipant(participant);
            var participantKey = ResolveParticipantIdToKey(sourceParticipant);
            if (string.IsNullOrWhiteSpace(participantKey))
            {
                return null;
            }

            var provisionalResolvedId = ResolveMatchResultParticipantId(sourceParticipant);
            var resolvedId = ResolveAuthoritativeParticipantKey(sourceParticipant, null, provisionalResolvedId);
            var mmrValue = GetAuthoritativeMmrOrDefault(sourceParticipant, resolvedId, out var canonicalMmrKey);
            var effectiveMmrKey = !string.IsNullOrWhiteSpace(canonicalMmrKey) ? canonicalMmrKey : resolvedId;
            var liveSteamId = ResolveParticipantSteamIdForUi(sourceParticipant, effectiveMmrKey);
            var displayName = ResolvePreferredParticipantDisplayName(sourceParticipant, null, resolvedId);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            var hasJsonHit = !string.IsNullOrWhiteSpace(effectiveMmrKey) && HasStoredMmrEntry(effectiveMmrKey);
            if (!hasJsonHit && !BotManager.IsBotKey(participantKey))
            {
                Debug.LogWarning($"[{Constants.MOD_NAME}] [MMR] Missing draft MMR entry for real player key={effectiveMmrKey ?? "none"}");
            }

            var effectiveTeam = sourceParticipant.team != TeamResult.Unknown ? sourceParticipant.team : fallbackTeam;
            var playerNumber = ResolveParticipantPlayerNumber(sourceParticipant);
            var isCaptain = !string.IsNullOrWhiteSpace(captainKey)
                && !string.IsNullOrWhiteSpace(participantKey)
                && string.Equals(participantKey, captainKey, StringComparison.OrdinalIgnoreCase);

            return new DraftOverlayPlayerEntryMessage
            {
                ClientId = sourceParticipant.clientId,
                SteamId = liveSteamId,
                CommandTarget = BuildStableDraftCommandTarget(participantKey),
                DisplayName = displayName,
                PlayerNumber = playerNumber,
                HasMmr = true,
                Mmr = mmrValue,
                IsCaptain = isCaptain,
                Team = effectiveTeam
            };
        }

        private static RankedParticipant ResolveDraftOverlaySourceParticipant(RankedParticipant participant)
        {
            if (participant == null)
            {
                return null;
            }

            if (participant.clientId != 0)
            {
                if (BotManager.TryGetBotIdByClientId(participant.clientId, out var botKey)
                    && BotManager.TryGetBotParticipant(botKey, out var botParticipant))
                {
                    return CloneParticipant(botParticipant);
                }

                if (TryGetPlayerByClientId(participant.clientId, out var livePlayer)
                    && TryBuildConnectedPlayerSnapshot(livePlayer, out var liveParticipant))
                {
                    return liveParticipant;
                }
            }

            return CloneParticipant(participant);
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
                Debug.Log($"[{Constants.MOD_NAME}] [OVERLAY] Draft state created. Visible={state.IsVisible} Completed={state.IsCompleted}");
                Debug.Log($"[{Constants.MOD_NAME}] [OVERLAY] Draft counts: available={(state.AvailablePlayers?.Length ?? 0)} red={(state.RedPlayers?.Length ?? 0)} blue={(state.BluePlayers?.Length ?? 0)} pending={(state.PendingLateJoiners?.Length ?? 0)} total={playerCount}");
                RankedOverlayNetwork.PublishDraftState(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] PublishDraftOverlayState failed: {ex.Message}");
            }
        }

        private static void PruneInvalidLiveRankedParticipants()
        {
            try
            {
                List<string> keysToRemove;

                lock (draftLock)
                {
                    keysToRemove = rankedParticipants
                        .Where(participant => participant != null)
                        .Select(participant => new
                        {
                            Participant = participant,
                            Key = ResolveParticipantIdToKey(participant)
                        })
                        .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                        .Where(entry =>
                            (!draftActive && IsDummyKey(entry.Key))
                            || (!BotManager.IsBotKey(entry.Key) && entry.Participant.clientId == 0))
                        .Select(entry => entry.Key)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (keysToRemove.Count == 0)
                    {
                        return;
                    }

                    rankedParticipants.RemoveAll(participant =>
                        participant != null
                        && keysToRemove.Contains(ResolveParticipantIdToKey(participant), StringComparer.OrdinalIgnoreCase));

                    foreach (var playerKey in keysToRemove)
                    {
                        draftAssignedTeams.Remove(playerKey);
                        pendingLateJoiners.Remove(playerKey);
                        announcedLateJoinerIds.Remove(playerKey);
                        draftAvailablePlayerIds.RemoveAll(candidate => string.Equals(candidate, playerKey, StringComparison.OrdinalIgnoreCase));
                        draftPreferredPositionKeys.Remove(playerKey);
                        ClearJoinState(playerKey);
                    }
                }

                if (keysToRemove.Count > 0)
                {
                    lock (dummyLock)
                    {
                        foreach (var playerKey in keysToRemove)
                        {
                            activeDraftDummies.Remove(playerKey);
                            queuedDraftDummies.Remove(playerKey);
                        }
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [RANKED] Pruned invalid live participants: {string.Join(", ", keysToRemove)}");
                }
            }
            catch { }
        }

}

}
