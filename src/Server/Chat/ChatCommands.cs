using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        public static void SendSystemChatToAll(string message)
        {
            try
            {
                var players = GetAllPlayers();
                foreach (var p in players)
                {
                    if (TryGetClientId(p, out var cid))
                    {
                        UIChat.Instance.Server_SendSystemChatMessage(message, cid);
                    }
                }
            }
            catch { }
        }

        public static void SendSystemChatToClient(string message, ulong clientId)
        {
            try { UIChat.Instance.Server_SendSystemChatMessage(message, clientId); } catch { }
        }

        public static bool TryHandleDraftCommand(object player, ulong clientId, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;

            var trimmed = message.Trim();
            if (trimmed.StartsWith("/dummy ", StringComparison.OrdinalIgnoreCase))
            {
                HandleDummyCommand(player, clientId, trimmed.Substring(7).Trim());
                return true;
            }

            if (trimmed.StartsWith("/dummygk ", StringComparison.OrdinalIgnoreCase))
            {
                HandleDummyGoalkeeperCommand(player, clientId, trimmed.Substring(9).Trim());
                return true;
            }

            if (trimmed.Equals("/record start", StringComparison.OrdinalIgnoreCase))
            {
                HandleRecordCommand(player, clientId, true);
                return true;
            }

            if (trimmed.Equals("/record stop", StringComparison.OrdinalIgnoreCase))
            {
                HandleRecordCommand(player, clientId, false);
                return true;
            }

            if (trimmed.Equals("/replay list", StringComparison.OrdinalIgnoreCase))
            {
                HandleReplayListCommand(clientId);
                return true;
            }

            if (trimmed.Equals("/replay", StringComparison.OrdinalIgnoreCase))
            {
                HandleReplayCommand(player, clientId, "latest");
                return true;
            }

            if (trimmed.StartsWith("/replay ", StringComparison.OrdinalIgnoreCase))
            {
                HandleReplayCommand(player, clientId, trimmed.Substring(8).Trim());
                return true;
            }

            if (trimmed.StartsWith("/pick ", StringComparison.OrdinalIgnoreCase))
            {
                HandleDraftPick(player, clientId, trimmed.Substring(6).Trim());
                return true;
            }

            if (trimmed.StartsWith("/accept ", StringComparison.OrdinalIgnoreCase))
            {
                HandleLateJoinAcceptance(player, clientId, trimmed.Substring(8).Trim());
                return true;
            }

            if (trimmed.StartsWith("/approve ", StringComparison.OrdinalIgnoreCase))
            {
                HandleApprovalDecision(player, clientId, trimmed.Substring(9).Trim(), true);
                return true;
            }

            if (trimmed.StartsWith("/reject ", StringComparison.OrdinalIgnoreCase))
            {
                HandleApprovalDecision(player, clientId, trimmed.Substring(8).Trim(), false);
                return true;
            }

            if (trimmed.Equals("/draft", StringComparison.OrdinalIgnoreCase))
            {
                SendDraftStatusToClient(clientId);
                return true;
            }

            if (trimmed.Equals("/draftui", StringComparison.OrdinalIgnoreCase))
            {
                HandleDraftUiCommand(clientId);
                return true;
            }

            return false;
        }

        private static void HandleDraftUiCommand(ulong clientId)
        {
            try
            {
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.DraftUiModule, $"The ranked overlay opens automatically during vote and draft. Use {ChatStyle.Command("/draft")} only if you need the text fallback.", ChatTone.Info), clientId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDraftUiCommand failed: {ex.Message}");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.DraftUiModule, "Command failed.", ChatTone.Error), clientId);
            }
        }

        private static void HandleDummyCommand(object player, ulong clientId, string rawCount)
        {
            try
            {
                if (!AreSyntheticPlayersAllowed())
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Synthetic players are disabled on this server.", ChatTone.Error), clientId);
                    return;
                }

                if (!TryIsAdminInternal(player, clientId))
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Permission denied.", ChatTone.Error), clientId);
                    return;
                }

                if (!int.TryParse(rawCount, out var count) || count <= 0)
                {
                    SendSystemChatToClient(ChatStyle.Usage("/dummy <count>"), clientId);
                    return;
                }

                var created = CreateDummyParticipants(count, rankedActive);
                if (created.Count == 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "No bots were created.", ChatTone.Error), clientId);
                    return;
                }

                if (!rankedActive)
                {
                    SendSystemChatToAll(ChatStyle.Message(ChatStyle.DummyModule, $"Queued {ChatStyle.Emphasis(created.Count.ToString())} bot spawn request(s) for the next draft: {ChatStyle.Safe(string.Join(", ", created.Select(p => p.displayName)))}.", ChatTone.Warning));
                    return;
                }

                foreach (var participant in created)
                {
                    lock (draftLock)
                    {
                        pendingLateJoiners[participant.playerId] = participant;
                        announcedLateJoinerIds.Remove(participant.playerId);
                    }
                    MergeOrReplaceParticipant(participant, TeamResult.Unknown);
                }

                SendSystemChatToAll(ChatStyle.Message(ChatStyle.DummyModule, $"Created {ChatStyle.Emphasis(created.Count.ToString())} real bot late joiner(s): {ChatStyle.Safe(string.Join(", ", created.Select(p => p.displayName)))}.", ChatTone.Warning));
                SendSystemChatToAll(ChatStyle.Message(ChatStyle.DummyModule, $"Captains can accept them like any other late joiner.", ChatTone.Info, 13));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDummyCommand failed: {ex.Message}");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Command failed.", ChatTone.Error), clientId);
            }
        }

        private static void HandleDummyGoalkeeperCommand(object player, ulong clientId, string rawArguments)
        {
            try
            {
                if (!AreSyntheticPlayersAllowed())
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Synthetic players are disabled on this server.", ChatTone.Error), clientId);
                    return;
                }

                if (!TryIsAdminInternal(player, clientId))
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Permission denied.", ChatTone.Error), clientId);
                    return;
                }

                var tokens = (rawArguments ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2
                    || !TryParseDummyGoalkeeperTeam(tokens[0], out var team)
                    || !TryParseDummyGoalkeeperDifficulty(tokens[1], out var difficulty))
                {
                    SendSystemChatToClient(ChatStyle.Usage("/dummygk <red|blue> <easy|normal|hard>"), clientId);
                    return;
                }

                var replacedExisting = false;
                if (BotManager.TryGetGoalieBotId(team, out var existingGoalieId))
                {
                    replacedExisting = RemoveTrackedBotParticipant(existingGoalieId);
                }

                var requestedName = team == TeamResult.Red ? "RedGoalie" : "BlueGoalie";
                var participant = SpawnTrackedGoalkeeperParticipant(team, difficulty, requestedName);
                if (participant == null)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Could not spawn the goalkeeper.", ChatTone.Error), clientId);
                    return;
                }

                lock (draftLock)
                {
                    pendingLateJoiners.Remove(participant.playerId);
                    announcedLateJoinerIds.Remove(participant.playerId);
                    draftAvailablePlayerIds.RemoveAll(id => string.Equals(id, participant.playerId, StringComparison.OrdinalIgnoreCase));
                    draftAssignedTeams[participant.playerId] = team;
                    draftPreferredPositionKeys.Remove(participant.playerId);
                }

                MergeOrReplaceParticipant(participant, team);
                ForceApplyDraftTeam(participant.playerId, team);
                BotManager.EnsureBotSafePosition(participant.playerId, team, null);

                var difficultyLabel = difficulty.ToString().ToLowerInvariant();
                var replacementLabel = replacedExisting ? "replaced and respawned" : "spawned";
                SendSystemChatToAll(ChatStyle.Message(ChatStyle.DummyModule, $"{ChatStyle.Safe(replacementLabel)} {ChatStyle.Team(team)} goalkeeper {ChatStyle.Player(participant.displayName)} ({ChatStyle.Emphasis(difficultyLabel)}).", ChatTone.Warning));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDummyGoalkeeperCommand failed: {ex.Message}");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.DummyModule, "Command failed.", ChatTone.Error), clientId);
            }
        }

        private static RankedParticipant SpawnTrackedBotParticipant(TeamResult team, bool isPendingLateJoiner, string requestedName = null)
        {
            if (!AreSyntheticPlayersAllowed())
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(requestedName))
            {
                lock (dummyLock)
                {
                    requestedName = $"RankedBot{nextDummySequence}";
                    nextDummySequence++;
                }
            }

            var participant = BotManager.SpawnBot(team, requestedName);
            if (participant == null) return null;

            if (isPendingLateJoiner) participant.team = TeamResult.Unknown;
            participant.isDummy = true;

            lock (dummyLock)
            {
                activeDraftDummies[participant.playerId] = new DummyPlayer
                {
                    dummyId = participant.playerId,
                    displayName = participant.displayName,
                    team = participant.team,
                    isPendingLateJoiner = isPendingLateJoiner
                };
            }

            return participant;
        }

        private static RankedParticipant SpawnTrackedGoalkeeperParticipant(TeamResult team, BotGoalieDifficulty difficulty, string requestedName = null)
        {
            if (!AreSyntheticPlayersAllowed())
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(requestedName))
            {
                requestedName = team == TeamResult.Red ? "RedGoalie" : "BlueGoalie";
            }

            var participant = BotManager.SpawnGoalieBot(team, difficulty, requestedName);
            if (participant == null) return null;

            participant.team = team;
            participant.isDummy = true;

            lock (dummyLock)
            {
                activeDraftDummies[participant.playerId] = new DummyPlayer
                {
                    dummyId = participant.playerId,
                    displayName = participant.displayName,
                    team = participant.team,
                    isPendingLateJoiner = false
                };
            }

            return participant;
        }

        private static List<RankedParticipant> CreateDummyParticipants(int count, bool asLateJoiners)
        {
            var created = new List<RankedParticipant>();

            if (count <= 0) return created;

            if (asLateJoiners)
            {
                for (var index = 0; index < count; index++)
                {
                    string fallbackName;
                    lock (dummyLock)
                    {
                        fallbackName = $"RankedBot{nextDummySequence}";
                        nextDummySequence++;
                    }

                    var participant = SpawnTrackedBotParticipant(TeamResult.Unknown, true, fallbackName);
                    if (participant == null) continue;
                    created.Add(participant);
                }

                return created;
            }

            lock (dummyLock)
            {
                for (var index = 0; index < count; index++)
                {
                    var queuedDummy = new DummyPlayer
                    {
                        dummyId = $"queuebot:{nextDummySequence}",
                        displayName = $"RankedBot{nextDummySequence}",
                        team = TeamResult.Unknown,
                        isPendingLateJoiner = false
                    };

                    nextDummySequence++;

                    queuedDraftDummies[queuedDummy.dummyId] = queuedDummy;
                    created.Add(CreateParticipantFromDummy(queuedDummy));
                }
            }

            return created;
        }

        private static List<RankedParticipant> ConsumeQueuedDummyParticipants()
        {
            List<DummyPlayer> queuedRequests;
            lock (dummyLock)
            {
                queuedRequests = queuedDraftDummies.Values.ToList();
                queuedDraftDummies.Clear();
            }

            var consumed = new List<RankedParticipant>();
            foreach (var queuedRequest in queuedRequests)
            {
                if (queuedRequest == null) continue;
                var participant = SpawnTrackedBotParticipant(TeamResult.Unknown, false, queuedRequest.displayName);
                if (participant == null) continue;
                consumed.Add(participant);
            }

            return consumed;
        }

        private static RankedParticipant CreateParticipantFromDummy(DummyPlayer dummy)
        {
            if (dummy == null || string.IsNullOrEmpty(dummy.dummyId)) return null;

            if (BotManager.TryGetBotParticipant(dummy.dummyId, out var botParticipant))
            {
                botParticipant.displayName = string.IsNullOrEmpty(dummy.displayName) ? botParticipant.displayName : dummy.displayName;
                botParticipant.team = dummy.team;
                botParticipant.isDummy = true;
                return botParticipant;
            }

            return new RankedParticipant
            {
                clientId = 0,
                playerId = dummy.dummyId,
                displayName = dummy.displayName,
                team = dummy.team,
                isDummy = true
            };
        }

        private static void ClearAllDummies()
        {
            lock (dummyLock)
            {
                queuedDraftDummies.Clear();
                activeDraftDummies.Clear();
                nextDummySequence = 1;
            }

            BotManager.RemoveAllBots();
        }

        private static bool RemoveTrackedBotParticipant(string botId)
        {
            if (string.IsNullOrWhiteSpace(botId)) return false;

            lock (draftLock)
            {
                draftAssignedTeams.Remove(botId);
                draftPreferredPositionKeys.Remove(botId);
                pendingLateJoiners.Remove(botId);
                announcedLateJoinerIds.Remove(botId);
                draftAvailablePlayerIds.RemoveAll(id => string.Equals(id, botId, StringComparison.OrdinalIgnoreCase));
                rankedParticipants.RemoveAll(participant => string.Equals(ResolveParticipantIdToKey(participant), botId, StringComparison.OrdinalIgnoreCase));
            }

            lock (dummyLock)
            {
                activeDraftDummies.Remove(botId);
                queuedDraftDummies.Remove(botId);
            }

            return BotManager.RemoveBot(botId);
        }

        private static bool IsDummyParticipant(RankedParticipant participant)
        {
            return participant != null && (participant.isDummy || IsDummyKey(participant.playerId));
        }

        private static bool IsDummyKey(string playerKey)
        {
            return !string.IsNullOrEmpty(playerKey)
                && (playerKey.StartsWith("dummy:", StringComparison.OrdinalIgnoreCase)
                    || playerKey.StartsWith("queuebot:", StringComparison.OrdinalIgnoreCase)
                    || BotManager.IsBotKey(playerKey));
        }

        private static void ResetDraftAnnouncementState()
        {
            lastDraftStatePollTime = -999f;
            lastDraftTurnAnnouncementTime = -999f;
            lastAnnouncedTurnId = null;
            lastAnnouncedAvailablePlayersSignature = null;
            lastAnnouncedAvailableCount = -1;
        }

        private static string BuildDraftAvailablePlayersSignatureLocked()
        {
            try
            {
                if (draftAvailablePlayerIds == null || draftAvailablePlayerIds.Count == 0) return string.Empty;
                return string.Join("|", draftAvailablePlayerIds
                    .Where(id => !string.IsNullOrEmpty(id))
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            }
            catch { }
            return string.Empty;
        }

        private static int GetConnectedRealPlayerCount()
        {
            try
            {
                var count = 0;
                foreach (var player in GetAllPlayers())
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot)) continue;
                    if (snapshot.isDummy) continue;
                    count++;
                }
                return count;
            }
            catch { }
            return 0;
        }

        private static bool CanUseSoloDummyCaptainProxy(object player, ulong clientId)
        {
            if (clientId == 0) return false;
            if (GetConnectedRealPlayerCount() != 1) return false;
            var playerKey = ResolvePlayerObjectKey(player, clientId) ?? $"clientId:{clientId}";
            if (string.IsNullOrEmpty(playerKey)) return false;
            if (TryGetCaptainTeam(playerKey, out var _)) return false;
            lock (draftLock)
            {
                return IsDummyKey(currentCaptainTurnId);
            }
        }

        private static bool TryGetCurrentCaptainTeam(out TeamResult team)
        {
            team = TeamResult.Unknown;
            lock (draftLock)
            {
                if (string.Equals(currentCaptainTurnId, redCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Red;
                    return true;
                }
                if (string.Equals(currentCaptainTurnId, blueCaptainId, StringComparison.OrdinalIgnoreCase))
                {
                    team = TeamResult.Blue;
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseDummyGoalkeeperTeam(string token, out TeamResult team)
        {
            team = TeamResult.Unknown;
            if (string.IsNullOrWhiteSpace(token)) return false;

            switch (token.Trim().ToLowerInvariant())
            {
                case "red":
                    team = TeamResult.Red;
                    return true;
                case "blue":
                    team = TeamResult.Blue;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseDummyGoalkeeperDifficulty(string token, out BotGoalieDifficulty difficulty)
        {
            difficulty = BotGoalieDifficulty.Normal;
            if (string.IsNullOrWhiteSpace(token)) return false;

            switch (token.Trim().ToLowerInvariant())
            {
                case "easy":
                    difficulty = BotGoalieDifficulty.Easy;
                    return true;
                case "normal":
                    difficulty = BotGoalieDifficulty.Normal;
                    return true;
                case "hard":
                    difficulty = BotGoalieDifficulty.Hard;
                    return true;
                default:
                    return false;
            }
        }

    }
}
