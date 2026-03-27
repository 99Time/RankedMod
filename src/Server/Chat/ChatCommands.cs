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
                if (!schrader.DraftStateBridge.CanRenderInCurrentProcess())
                {
                    var reason = schrader.DraftStateBridge.GetUnavailableReason();
                    SendSystemChatToClient($"<size=14><color=#ff6666>Draft UI</color> {reason}</size>", clientId);
                    return;
                }

                var visible = schrader.DraftStateBridge.Toggle();
                var testMode = schrader.DraftStateBridge.IsTestModeEnabled();
                SendSystemChatToClient(visible
                    ? (testMode
                        ? "<size=14><color=#00ff00>Draft UI</color> shown in test mode.</size>"
                        : "<size=14><color=#00ff00>Draft UI</color> shown.</size>")
                    : "<size=14><color=#ffcc66>Draft UI</color> hidden.</size>", clientId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDraftUiCommand failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Draft UI</color> command failed.</size>", clientId);
            }
        }

        private static void HandleDummyCommand(object player, ulong clientId, string rawCount)
        {
            try
            {
                if (!TryIsAdminInternal(player, clientId))
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Dummy</color> permission denied.</size>", clientId);
                    return;
                }

                if (!int.TryParse(rawCount, out var count) || count <= 0)
                {
                    SendSystemChatToClient("<size=14>Usage: /dummy <count></size>", clientId);
                    return;
                }

                var created = CreateDummyParticipants(count, rankedActive);
                if (created.Count == 0)
                {
                    SendSystemChatToClient("<size=14><color=#ff6666>Dummy</color> no dummies were created.</size>", clientId);
                    return;
                }

                if (!rankedActive)
                {
                    SendSystemChatToAll($"<size=14><color=#ffcc66>Dummy</color> queued {created.Count} logical dummies for the next draft: {string.Join(", ", created.Select(p => p.displayName))}</size>");
                    SendSystemChatToAll("<size=13><color=#ffcc66>Dummy</color> these players only exist inside RankedSystem and never spawn as real players.</size>");
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

                SendSystemChatToAll($"<size=14><color=#ffcc66>Dummy</color> created {created.Count} logical late joiners: {string.Join(", ", created.Select(p => p.displayName))}</size>");
                SendSystemChatToAll("<size=13><color=#ffcc66>Dummy</color> captains can accept them like any other late joiner, but they will not enter network gameplay.</size>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] HandleDummyCommand failed: {ex.Message}");
                SendSystemChatToClient("<size=14><color=#ff6666>Dummy</color> command failed.</size>", clientId);
            }
        }

        private static List<RankedParticipant> CreateDummyParticipants(int count, bool asLateJoiners)
        {
            var created = new List<RankedParticipant>();
            lock (dummyLock)
            {
                for (var index = 0; index < count; index++)
                {
                    var dummy = new DummyPlayer
                    {
                        dummyId = $"dummy:{nextDummySequence}",
                        displayName = $"Dummy{nextDummySequence}",
                        team = TeamResult.Unknown,
                        isPendingLateJoiner = asLateJoiners
                    };
                    nextDummySequence++;

                    if (asLateJoiners) activeDraftDummies[dummy.dummyId] = dummy;
                    else queuedDraftDummies[dummy.dummyId] = dummy;

                    created.Add(CreateParticipantFromDummy(dummy));
                }
            }

            return created;
        }

        private static List<RankedParticipant> ConsumeQueuedDummyParticipants()
        {
            lock (dummyLock)
            {
                var consumed = queuedDraftDummies.Values
                    .Select(CreateParticipantFromDummy)
                    .Where(p => p != null)
                    .ToList();

                foreach (var participant in consumed)
                {
                    activeDraftDummies[participant.playerId] = new DummyPlayer
                    {
                        dummyId = participant.playerId,
                        displayName = participant.displayName,
                        team = participant.team,
                        isPendingLateJoiner = false
                    };
                }

                queuedDraftDummies.Clear();
                return consumed;
            }
        }

        private static RankedParticipant CreateParticipantFromDummy(DummyPlayer dummy)
        {
            if (dummy == null || string.IsNullOrEmpty(dummy.dummyId)) return null;
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
        }

        private static bool IsDummyParticipant(RankedParticipant participant)
        {
            return participant != null && (participant.isDummy || IsDummyKey(participant.playerId));
        }

        private static bool IsDummyKey(string playerKey)
        {
            return !string.IsNullOrEmpty(playerKey) && playerKey.StartsWith("dummy:", StringComparison.OrdinalIgnoreCase);
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

    }
}
