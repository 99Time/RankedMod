using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private static readonly object postMatchLock = new object();
        private static bool postMatchLockActive;
        private static float postMatchLockStartedAt = -999f;
        private const float PostMatchLockTimeout = 90f;
        private static MatchResultMessage activeMatchResultState = MatchResultMessage.Hidden();
        private static readonly HashSet<ulong> pendingPostMatchDismissClientIds = new HashSet<ulong>();
        private static readonly Dictionary<ulong, string> deferredPostMatchPositionStates = new Dictionary<ulong, string>();
        private static readonly Dictionary<string, string> lockedPostMatchPositionKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static bool BeginPostMatchLock(MatchResultMessage matchResult)
        {
            if (matchResult == null || !matchResult.IsVisible)
            {
                ClearPostMatchLockState();
                return false;
            }

            var pendingClientIds = ResolvePostMatchClientIds();
            var lockedPositions = CaptureLockedPostMatchPositions();

            lock (postMatchLock)
            {
                postMatchLockActive = pendingClientIds.Count > 0;
                postMatchLockStartedAt = postMatchLockActive ? Time.unscaledTime : -999f;
                activeMatchResultState = matchResult;
                pendingPostMatchDismissClientIds.Clear();
                pendingPostMatchDismissClientIds.UnionWith(pendingClientIds);
                deferredPostMatchPositionStates.Clear();
                lockedPostMatchPositionKeys.Clear();
                foreach (var entry in lockedPositions)
                {
                    lockedPostMatchPositionKeys[entry.Key] = entry.Value;
                }
            }

            return postMatchLockActive;
        }

        internal static MatchResultMessage GetMatchResultStateForClient(ulong clientId)
        {
            lock (postMatchLock)
            {
                if (!postMatchLockActive || activeMatchResultState == null || !activeMatchResultState.IsVisible)
                {
                    return MatchResultMessage.Hidden();
                }

                return pendingPostMatchDismissClientIds.Contains(clientId)
                    ? activeMatchResultState
                    : MatchResultMessage.Hidden();
            }
        }

        internal static void HandlePostMatchDismiss(ulong clientId)
        {
            string deferredStateName = null;
            bool shouldFinalize;

            lock (postMatchLock)
            {
                if (!postMatchLockActive)
                {
                    return;
                }

                pendingPostMatchDismissClientIds.Remove(clientId);
                if (deferredPostMatchPositionStates.TryGetValue(clientId, out deferredStateName))
                {
                    deferredPostMatchPositionStates.Remove(clientId);
                }

                shouldFinalize = pendingPostMatchDismissClientIds.Count == 0;
            }

            if (!string.IsNullOrWhiteSpace(deferredStateName))
            {
                ReplayDeferredPostMatchPositionState(clientId, deferredStateName);
            }

            if (shouldFinalize)
            {
                FinalizePostMatchLock("all clients dismissed post-match");
            }
        }

        internal static void UpdatePostMatchLock()
        {
            try
            {
                bool shouldFinalize;
                string finalizeReason;

                lock (postMatchLock)
                {
                    if (!postMatchLockActive)
                    {
                        return;
                    }

                    PruneDisconnectedPostMatchClients_NoLock();
                    if (pendingPostMatchDismissClientIds.Count == 0)
                    {
                        shouldFinalize = true;
                        finalizeReason = "no clients remaining in post-match lock";
                    }
                    else if (postMatchLockStartedAt >= 0f && Time.unscaledTime - postMatchLockStartedAt >= PostMatchLockTimeout)
                    {
                        shouldFinalize = true;
                        finalizeReason = "post-match lock timeout reached";
                    }
                    else
                    {
                        shouldFinalize = false;
                        finalizeReason = null;
                    }
                }

                if (shouldFinalize)
                {
                    FinalizePostMatchLock(finalizeReason);
                }
            }
            catch { }
        }

        internal static bool TryBlockPostMatchPlayerState(object player, object state)
        {
            try
            {
                var stateName = state?.ToString() ?? string.Empty;
                if (!IsBlockedPostMatchState(stateName))
                {
                    return false;
                }

                ulong clientId = 0;
                string playerKey = null;
                string currentPositionKey = null;
                if (player != null)
                {
                    TryGetClientId(player, out clientId);
                    playerKey = TryGetPlayerId(player, clientId) ?? (clientId != 0 ? clientId.ToString() : null);
                    currentPositionKey = TryGetCurrentPositionKey(player);
                }

                lock (postMatchLock)
                {
                    if (!postMatchLockActive || clientId == 0 || !pendingPostMatchDismissClientIds.Contains(clientId))
                    {
                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(playerKey) && !string.IsNullOrWhiteSpace(currentPositionKey))
                    {
                        lockedPostMatchPositionKeys[playerKey] = currentPositionKey;
                    }

                    if (IsDeferredPostMatchState(stateName))
                    {
                        deferredPostMatchPositionStates[clientId] = stateName;
                    }

                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsBlockedPostMatchState(string stateName)
        {
            return string.Equals(stateName, "TeamSelect", StringComparison.Ordinal)
                || IsDeferredPostMatchState(stateName);
        }

        private static bool IsDeferredPostMatchState(string stateName)
        {
            return string.Equals(stateName, "PositionSelectBlue", StringComparison.Ordinal)
                || string.Equals(stateName, "PositionSelectRed", StringComparison.Ordinal);
        }

        private static HashSet<ulong> ResolvePostMatchClientIds()
        {
            lock (rankedLock)
            {
                return rankedParticipants
                    .Where(participant => participant != null && !participant.isDummy && participant.clientId != 0)
                    .Select(participant => participant.clientId)
                    .ToHashSet();
            }
        }

        private static Dictionary<string, string> CaptureLockedPostMatchPositions()
        {
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<RankedParticipant> participants;
            lock (rankedLock)
            {
                participants = rankedParticipants
                    .Where(participant => participant != null && !participant.isDummy)
                    .ToList();
            }

            foreach (var participant in participants)
            {
                object player = null;
                if (participant.clientId != 0 && TryGetPlayerByClientId(participant.clientId, out var playerByClientId))
                {
                    player = playerByClientId;
                }
                else
                {
                    TryResolveConnectedPlayer(participant.playerId, participant.clientId, out player, out _, out _);
                }

                if (player == null)
                {
                    continue;
                }

                var playerKey = ResolveParticipantIdToKey(participant) ?? TryGetPlayerId(player, participant.clientId);
                var positionKey = TryGetCurrentPositionKey(player);
                if (string.IsNullOrWhiteSpace(playerKey) || string.IsNullOrWhiteSpace(positionKey))
                {
                    continue;
                }

                snapshot[playerKey] = positionKey;
            }

            return snapshot;
        }

        private static void ReplayDeferredPostMatchPositionState(ulong clientId, string stateName)
        {
            try
            {
                if (clientId == 0 || string.IsNullOrWhiteSpace(stateName))
                {
                    return;
                }

                if (!TryGetPlayerByClientId(clientId, out var player) || player == null)
                {
                    return;
                }

                var playerStateType = FindTypeByName("PlayerState", "Puck.PlayerState");
                if (playerStateType == null || !playerStateType.IsEnum)
                {
                    return;
                }

                var state = Enum.Parse(playerStateType, stateName, true);
                TryInvokeRpcExecute(player, "Client_SetPlayerStateRpc", state, 0f);
            }
            catch { }
        }

        private static void FinalizePostMatchLock(string reason)
        {
            bool hadVisibleResult;
            lock (postMatchLock)
            {
                hadVisibleResult = activeMatchResultState != null && activeMatchResultState.IsVisible;
                ClearPostMatchLockState_NoLock();
            }

            if (hadVisibleResult)
            {
                Debug.Log($"[{Constants.MOD_NAME}] Finalizing post-match lock: {reason}");
                RankedOverlayNetwork.PublishMatchResult(MatchResultMessage.Hidden());
            }

            ResetRankedState(true, true);
        }

        private static void ClearPostMatchLockState()
        {
            lock (postMatchLock)
            {
                ClearPostMatchLockState_NoLock();
            }
        }

        private static void ClearPostMatchLockState_NoLock()
        {
            postMatchLockActive = false;
            postMatchLockStartedAt = -999f;
            activeMatchResultState = MatchResultMessage.Hidden();
            pendingPostMatchDismissClientIds.Clear();
            deferredPostMatchPositionStates.Clear();
            lockedPostMatchPositionKeys.Clear();
        }

        private static void PruneDisconnectedPostMatchClients_NoLock()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            pendingPostMatchDismissClientIds.RemoveWhere(clientId => !networkManager.ConnectedClientsIds.Contains(clientId));
        }

        [HarmonyPatch(typeof(Player), "Client_SetPlayerStateRpc")]
        private static class PlayerClientSetPlayerStateRpcPostMatchPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(object __instance, object state)
            {
                return !TryBlockPostMatchPlayerState(__instance, state);
            }
        }
    }
}