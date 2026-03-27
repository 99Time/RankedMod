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
                    .Where(p => !IsDummyParticipant(p))
                    .ToList();

                if (realCaptainPool.Count < 2)
                {
                    if (!(testModeSinglePlayer && realCaptainPool.Count == 1)) return false;

                    var singlePlayerCaptain = realCaptainPool[0];
                    var dummyCaptain = CreateSinglePlayerDummyCaptain();
                    if (dummyCaptain == null) return false;

                    combinedParticipants.Add(dummyCaptain);
                    rankedParticipants = combinedParticipants.Select(CloneParticipant).Where(p => p != null).ToList();
                    pool.Add(dummyCaptain);
                    realCaptainPool.Add(dummyCaptain);

                    var singlePlayerName = singlePlayerCaptain.displayName ?? "Player";
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Ranked</color> single-player test draft enabled. Auto-added dummy captain for {singlePlayerName}.</size>");
                }

                var rng = new System.Random();
                var shuffled = realCaptainPool.OrderBy(_ => rng.Next()).ToList();
                var redCaptain = shuffled[0];
                var blueCaptain = shuffled[1];
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
                SendSystemChatToAll(BuildDraftOverlayFallbackMessage(false, true));

                if (!draftAvailablePlayerIds.Any())
                {
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

        private static RankedParticipant CreateSinglePlayerDummyCaptain()
        {
            try
            {
                lock (dummyLock)
                {
                    var dummy = new DummyPlayer
                    {
                        dummyId = $"dummy:{nextDummySequence}",
                        displayName = $"DummyCaptain{nextDummySequence}",
                        team = TeamResult.Blue,
                        isPendingLateJoiner = false
                    };

                    nextDummySequence++;
                    activeDraftDummies[dummy.dummyId] = dummy;
                    return CreateParticipantFromDummy(dummy);
                }
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
                }

                ApplyDraftTeamsToParticipants();
                ForceApplyDraftTeam(pickedKey, captainTeam);
                PublishDraftOverlayState();
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
                ForceApplyDraftTeam(acceptedKey, captainTeam);
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
            lock (draftLock)
            {
                if (!draftActive) return;
                if (draftAvailablePlayerIds.Count == 0)
                {
                    CompleteDraftAndStartMatch();
                    return;
                }
            }

            AnnounceDraftTurn();
        }

        private static void CompleteDraftAndStartMatch()
        {
            string completionPanelMessage;
            lock (draftLock)
            {
                if (!draftActive) return;
                draftActive = false;
                completionPanelMessage = BuildDraftOverlayFallbackMessage(true, false);
            }
            Debug.Log("DRAFT COMPLETED");

            ForceApplyAllDraftTeams();
            ApplyDraftTeamsToParticipants();
            PublishDraftOverlayState();
            SendSystemChatToAll(completionPanelMessage);
            SendDraftTeamSummary();
            SendSystemChatToAll("<size=14><color=#00ff00>Ranked match started.</color></size>");
            TryStartMatch();
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
            SendSystemChatToAll(BuildDraftOverlayFallbackMessage(false, true));
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

        private static void RemoveDisconnectedDraftCandidates()
        {
            try
            {
                if (!ShouldPollDraftPlayerChanges()) return;

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

                if (draftPoolChanged || pendingPoolChanged)
                {
                    PublishDraftOverlayState();
                }

                if (draftPoolChanged)
                {
                    CompleteDraftIfReadyOrAnnounceTurn();
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

                var playerId = ResolvePlayerObjectKey(player, clientId) ?? TryGetPlayerId(player, clientId);
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
                return;
            }
            try
            {
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
                    var availablePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAvailablePlayerIds.Contains(participantKey, StringComparer.OrdinalIgnoreCase);
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var redPlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam) && assignedTeam == TeamResult.Red;
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var bluePlayers = rankedParticipants
                        .Where(p => p != null)
                        .Where(p =>
                        {
                            var participantKey = ResolveParticipantIdToKey(p);
                            return !string.IsNullOrEmpty(participantKey) && draftAssignedTeams.TryGetValue(participantKey, out var assignedTeam) && assignedTeam == TeamResult.Blue;
                        })
                        .Select(p => p.displayName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var isCompleted = rankedActive && !draftActive && (redPlayers.Length > 0 || bluePlayers.Length > 0);
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
                        CurrentTurnName = draftActive ? (GetCaptainDisplayNameByKey(currentCaptainTurnId) ?? "Pending") : string.Empty,
                        AvailablePlayers = availablePlayers,
                        RedPlayers = redPlayers,
                        BluePlayers = bluePlayers,
                        PendingLateJoinerCount = pendingLateJoiners.Count,
                        DummyModeActive = rankedParticipants.Any(IsDummyParticipant),
                        FooterText = "Use /draftui to toggle. Use /pick or scoreboard click where supported."
                    };
                }
            }
            catch { }

            return new DraftOverlayState { IsVisible = false };
        }

        private static void PublishDraftOverlayState()
        {
            try
            {
                schrader.DraftStateBridge.PublishState(GetDraftOverlayState());
                Debug.Log("DRAFT STATE PUBLISHED");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] PublishDraftOverlayState failed: {ex.Message}");
            }
        }
    }
}
