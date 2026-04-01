using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace schrader.Server
{
    public static partial class RankedSystem
    {
        private enum RankedJoinState
        {
            Idle,
            PendingApproval,
            Approved,
            Rejected,
            InTeam
        }

        private static readonly object approvalRequestLock = new object();
        private static readonly object joinStateLock = new object();
        private static readonly Dictionary<string, TeamApprovalRequest> pendingTeamApprovalRequests = new Dictionary<string, TeamApprovalRequest>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RankedJoinState> joinStateByPlayerKey = new Dictionary<string, RankedJoinState>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HashSet<TeamResult>> rejectedLateJoinTeams = new Dictionary<string, HashSet<TeamResult>>(StringComparer.OrdinalIgnoreCase);
        private static int nextApprovalRequestSequence = 1;

        private enum TeamApprovalRequestKind
        {
            LateJoin,
            TeamSwitch
        }

        private sealed class TeamApprovalRequest
        {
            public string RequestId;
            public string PlayerId;
            public ulong ClientId;
            public string PlayerName;
            public TeamResult TargetTeam;
            public TeamResult PreviousTeam;
            public string PreviousPositionKey;
            public float Timestamp;
            public TeamApprovalRequestKind Kind;
            public string TargetCaptainKey;
            public ulong TargetCaptainClientId;
        }

        private static RankedJoinState GetJoinState(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return RankedJoinState.Idle;
            }

            lock (joinStateLock)
            {
                return joinStateByPlayerKey.TryGetValue(playerKey, out var state)
                    ? state
                    : RankedJoinState.Idle;
            }
        }

        private static void SetJoinState(string playerKey, RankedJoinState state)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            lock (joinStateLock)
            {
                if (state == RankedJoinState.Idle)
                {
                    joinStateByPlayerKey.Remove(playerKey);
                }
                else
                {
                    joinStateByPlayerKey[playerKey] = state;
                }
            }
        }

        private static void ClearJoinState(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            lock (joinStateLock)
            {
                joinStateByPlayerKey.Remove(playerKey);
            }
        }

        private static bool IsPendingJoinState(string playerKey)
        {
            return GetJoinState(playerKey) == RankedJoinState.PendingApproval;
        }

        public static ApprovalRequestStateMessage GetApprovalRequestStateForClient(ulong clientId)
        {
            try
            {
                if (!TryGetCaptainIdentityForClient(clientId, out _, out var captainKey))
                {
                    return ApprovalRequestStateMessage.Hidden();
                }

                TeamApprovalRequest request;
                lock (approvalRequestLock)
                {
                    request = CloneApprovalRequest(GetNextApprovalRequestForCaptainLocked(captainKey, clientId));
                }

                if (request == null)
                {
                    return ApprovalRequestStateMessage.Hidden();
                }

                var targetTeamText = FormatTeamLabel(request.TargetTeam);
                var previousTeamText = request.PreviousTeam == TeamResult.Red || request.PreviousTeam == TeamResult.Blue
                    ? FormatTeamLabel(request.PreviousTeam)
                    : string.Empty;
                var promptText = request.Kind == TeamApprovalRequestKind.TeamSwitch
                    ? $"{request.PlayerName} wants to switch from {previousTeamText} to {targetTeamText}."
                    : $"{request.PlayerName} wants to join {targetTeamText}.";
                var footerText = request.Kind == TeamApprovalRequestKind.TeamSwitch
                    ? "Approve to move the player into the target team and reopen position selection. Reject leaves them on their current team and position."
                    : "Approve to move the player into the team and open position selection. Reject keeps them out of the match.";

                return new ApprovalRequestStateMessage
                {
                    IsVisible = true,
                    RequestId = request.RequestId,
                    Title = "TEAM APPROVAL REQUIRED",
                    PlayerName = request.PlayerName,
                    PromptText = promptText,
                    TargetTeamName = targetTeamText,
                    PreviousTeamName = previousTeamText,
                    IsSwitchRequest = request.Kind == TeamApprovalRequestKind.TeamSwitch,
                    FooterText = footerText
                };
            }
            catch
            {
                return ApprovalRequestStateMessage.Hidden();
            }
        }

        private static void UpdateApprovalRequestState()
        {
            try
            {
                if (!rankedActive)
                {
                    ClearApprovalRequests();
                    return;
                }

                List<TeamApprovalRequest> playerDisconnectedRequests = null;
                List<TeamApprovalRequest> captainUnavailableRequests = null;

                lock (approvalRequestLock)
                {
                    foreach (var request in pendingTeamApprovalRequests.Values.ToList())
                    {
                        if (request == null) continue;

                        if (!TryResolveConnectedPlayer(request.PlayerId, request.ClientId, out _, out _, out _))
                        {
                            if (playerDisconnectedRequests == null) playerDisconnectedRequests = new List<TeamApprovalRequest>();
                            playerDisconnectedRequests.Add(CloneApprovalRequest(request));
                            pendingTeamApprovalRequests.Remove(request.RequestId);
                            continue;
                        }

                        if (!TryResolveApprovalRequestCaptain(request, out _, out _))
                        {
                            if (captainUnavailableRequests == null) captainUnavailableRequests = new List<TeamApprovalRequest>();
                            captainUnavailableRequests.Add(CloneApprovalRequest(request));
                            pendingTeamApprovalRequests.Remove(request.RequestId);
                        }
                    }
                }

                if (playerDisconnectedRequests != null)
                {
                    foreach (var request in playerDisconnectedRequests)
                    {
                        ClearJoinState(request.PlayerId);
                        SendCaptainChatForRequest(request.TargetTeam, $"<size=13><color=#ffcc66>Ranked</color> pending request removed because <b>{request.PlayerName}</b> disconnected.</size>");
                    }
                }

                if (captainUnavailableRequests != null)
                {
                    foreach (var request in captainUnavailableRequests)
                    {
                        if (request.Kind == TeamApprovalRequestKind.TeamSwitch)
                        {
                            SetJoinState(request.PlayerId, RankedJoinState.InTeam);
                        }
                        else
                        {
                            SetJoinState(request.PlayerId, RankedJoinState.Idle);
                            TryOpenPlayerTeamSelection(request.PlayerId, request.ClientId);
                        }

                        if (request.ClientId != 0)
                        {
                            SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> your request to join {FormatTeamLabel(request.TargetTeam)} was cancelled because that captain is no longer available.</size>", request.ClientId);
                        }
                    }
                }

                if (playerDisconnectedRequests != null || captainUnavailableRequests != null)
                {
                    RefreshCaptainApprovalPanels();
                }
            }
            catch { }
        }

        private static void ClearApprovalRequests()
        {
            var hadRequests = false;
            lock (approvalRequestLock)
            {
                hadRequests = pendingTeamApprovalRequests.Count > 0 || rejectedLateJoinTeams.Count > 0;
                pendingTeamApprovalRequests.Clear();
                rejectedLateJoinTeams.Clear();
            }

            if (hadRequests)
            {
                PublishHiddenApprovalStateToAllClients();
            }
        }

        private static bool TryHandleTeamSelectionRequest(object player, ulong clientId, object currentTeam, object requestedTeam)
        {
            try
            {
                if (!rankedActive || draftActive || draftTeamLockActive) return false;

                var targetTeam = ConvertTeamValue(requestedTeam);
                if (targetTeam != TeamResult.Red && targetTeam != TeamResult.Blue)
                {
                    if (IsTeamNoneLike(currentTeam))
                    {
                        return false;
                    }

                    TryWarnTeamSwitchBlocked(player);
                    return true;
                }

                var currentResolvedTeam = ConvertTeamValue(currentTeam);
                if (currentResolvedTeam == targetTeam)
                {
                    SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> you are already on {FormatTeamLabel(targetTeam)}.</size>", clientId);
                    return true;
                }

                if (currentResolvedTeam == TeamResult.Red || currentResolvedTeam == TeamResult.Blue)
                {
                    TryWarnTeamSwitchBlocked(player);
                    return true;
                }

                if (TryQueueTeamApprovalRequest(player, clientId, targetTeam))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryHandleSwitchTeamMenuRequest(object player, ulong clientId)
        {
            try
            {
                if (!rankedActive || draftActive || draftTeamLockActive)
                {
                    return false;
                }

                if (player == null && clientId != 0)
                {
                    TryGetPlayerByClientId(clientId, out player);
                }

                if (!TryGetPlayerTeam(player, out var currentTeam))
                {
                    TryGetPlayerTeamFromManager(clientId, out currentTeam);
                }

                if (currentTeam != TeamResult.Red && currentTeam != TeamResult.Blue)
                {
                    return false;
                }

                TryWarnTeamSwitchBlocked(player);
                return true;
            }
            catch { }

            return false;
        }

        private static bool TryQueueTeamApprovalRequest(object player, ulong clientId, TeamResult targetTeam)
        {
            if (targetTeam != TeamResult.Red && targetTeam != TeamResult.Blue)
            {
                return false;
            }

            try
            {
                if (player == null && clientId != 0)
                {
                    TryGetPlayerByClientId(clientId, out player);
                }

                if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot) || snapshot == null)
                {
                    SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> could not resolve your player for a join request.</size>", clientId);
                    return true;
                }

                var playerKey = ResolveParticipantIdToKey(snapshot);
                if (string.IsNullOrWhiteSpace(playerKey))
                {
                    SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> could not resolve your player key for a join request.</size>", clientId);
                    return true;
                }

                var currentTeam = snapshot.team;
                var isSwitchRequest = currentTeam == TeamResult.Red || currentTeam == TeamResult.Blue;
                if (isSwitchRequest && currentTeam == targetTeam)
                {
                    SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> you are already on {FormatTeamLabel(targetTeam)}.</size>", clientId);
                    return true;
                }

                if (isSwitchRequest)
                {
                    TryWarnTeamSwitchBlocked(player);
                    SetJoinState(playerKey, RankedJoinState.InTeam);
                    return true;
                }

                if (IsPendingJoinState(playerKey))
                {
                    SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> you already have a pending request for {FormatTeamLabel(targetTeam)}.</size>", clientId);
                    return true;
                }

                TeamApprovalRequest existingRequest;
                lock (approvalRequestLock)
                {
                    existingRequest = pendingTeamApprovalRequests.Values.FirstOrDefault(existing =>
                        existing != null && string.Equals(existing.PlayerId, playerKey, StringComparison.OrdinalIgnoreCase));
                }

                if (existingRequest != null)
                {
                    SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> you already have a pending request for {FormatTeamLabel(existingRequest.TargetTeam)}.</size>", clientId);
                    return true;
                }

                if (!TryGetCaptainClientIdForTeam(targetTeam, out var captainClientId, out var captainKey, out var captainName))
                {
                    SetJoinState(playerKey, isSwitchRequest ? RankedJoinState.InTeam : RankedJoinState.Idle);
                    if (!isSwitchRequest)
                    {
                        TryOpenPlayerTeamSelection(playerKey, snapshot.clientId);
                    }

                    SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> {FormatTeamLabel(targetTeam)} captain is unavailable right now. Try again when that captain reconnects.</size>", clientId);
                    return true;
                }

                if (string.Equals(captainKey, playerKey, StringComparison.OrdinalIgnoreCase))
                {
                    SetJoinState(playerKey, isSwitchRequest ? RankedJoinState.InTeam : RankedJoinState.Idle);
                    if (!isSwitchRequest)
                    {
                        TryOpenPlayerTeamSelection(playerKey, snapshot.clientId);
                    }

                    SendSystemChatToClient($"<size=13><color=#ff6666>Ranked</color> you cannot send an approval request to yourself.</size>", clientId);
                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Blocked self-request for {snapshot.displayName} ({playerKey}) targeting {FormatTeamLabel(targetTeam)}.");
                    return true;
                }

                var requestId = $"req{nextApprovalRequestSequence++:0000}";
                var request = new TeamApprovalRequest
                {
                    RequestId = requestId,
                    PlayerId = playerKey,
                    ClientId = snapshot.clientId,
                    PlayerName = snapshot.displayName ?? $"Player {snapshot.clientId}",
                    TargetTeam = targetTeam,
                    PreviousTeam = currentTeam,
                    PreviousPositionKey = TryGetCurrentPositionKey(player),
                    Timestamp = Time.unscaledTime,
                    Kind = isSwitchRequest ? TeamApprovalRequestKind.TeamSwitch : TeamApprovalRequestKind.LateJoin,
                    TargetCaptainKey = captainKey,
                    TargetCaptainClientId = captainClientId
                };

                lock (approvalRequestLock)
                {
                    pendingTeamApprovalRequests[request.RequestId] = request;
                }

                SetJoinState(playerKey, RankedJoinState.PendingApproval);
                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Player requested join: {request.PlayerName} ({playerKey}) -> {FormatTeamLabel(targetTeam)}.");

                if (!isSwitchRequest)
                {
                    TrySetPlayerWaitingState(request.PlayerId, request.ClientId);
                }

                if (controlledTestModeEnabled && (IsDummyKey(captainKey) || BotManager.IsBotKey(captainKey)))
                {
                    lock (approvalRequestLock)
                    {
                        pendingTeamApprovalRequests.Remove(request.RequestId);
                    }

                    ResolveApprovedRequest(CloneApprovalRequest(request), captainKey);
                    RefreshCaptainApprovalPanels();
                    return true;
                }

                var requestLabel = isSwitchRequest ? "switch" : "join";
                SendSystemChatToClient($"<size=13><color=#66ccff>Ranked</color> {requestLabel} request sent to {FormatTeamLabel(targetTeam)} captain.</size>", clientId);
                SendSystemChatToClient($"<size=13><color=#ffcc66>Ranked</color> {request.PlayerName} wants to {(isSwitchRequest ? $"switch into {FormatTeamLabel(targetTeam)}" : $"join {FormatTeamLabel(targetTeam)}")}. Use the approval popup.</size>", captainClientId);
                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Sent to captain {captainName ?? captainKey ?? "unknown"}: request {request.RequestId} for {request.PlayerName} -> {FormatTeamLabel(targetTeam)}.");
                RefreshCaptainApprovalPanels();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to queue team approval request: {ex.Message}");
                SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> could not create a team approval request.</size>", clientId);
                return true;
            }
        }

        private static void HandleApprovalDecision(object player, ulong clientId, string requestId, bool approved)
        {
            try
            {
                if (!rankedActive || draftActive)
                {
                    SendSystemChatToClient("<size=13><color=#ffcc66>Ranked</color> there is no active approval flow right now.</size>", clientId);
                    return;
                }

                if (string.IsNullOrWhiteSpace(requestId))
                {
                    SendSystemChatToClient($"<size=13>Usage: {(approved ? "/approve" : "/reject")} <requestId></size>", clientId);
                    return;
                }

                EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: false);

                var actorKey = GetPlayerKey(player, clientId);
                if (!TryGetCaptainTeam(actorKey, out _))
                {
                    SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> only team captains can approve or reject requests.</size>", clientId);
                    return;
                }

                TeamApprovalRequest request;
                lock (approvalRequestLock)
                {
                    if (!pendingTeamApprovalRequests.TryGetValue(requestId.Trim(), out request) || request == null)
                    {
                        SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> request not found.</size>", clientId);
                        return;
                    }

                    TryResolveApprovalRequestCaptain(request, out _, out _);

                    if (!IsApprovalRequestOwner(actorKey, clientId, request))
                    {
                        SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> that request belongs to the other captain.</size>", clientId);
                        return;
                    }

                    pendingTeamApprovalRequests.Remove(request.RequestId);
                    request = CloneApprovalRequest(request);
                }

                if (approved)
                {
                    ResolveApprovedRequest(request, actorKey);
                }
                else
                {
                    ResolveRejectedRequest(request, actorKey);
                }

                RefreshCaptainApprovalPanels();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Approval decision failed: {ex.Message}");
                SendSystemChatToClient("<size=13><color=#ff6666>Ranked</color> could not process that request.</size>", clientId);
            }
        }

        private static void ResolveApprovedRequest(TeamApprovalRequest request, string captainKey)
        {
            if (request == null) return;

            SetJoinState(request.PlayerId, RankedJoinState.Approved);

            if (!TryApplyOfficialTeamJoin(request.PlayerId, request.ClientId, request.TargetTeam))
            {
                if (request.Kind == TeamApprovalRequestKind.TeamSwitch)
                {
                    EnsureRejectedSwitchState(request);
                    SetJoinState(request.PlayerId, RankedJoinState.InTeam);
                }
                else
                {
                    TryOpenPlayerTeamSelection(request.PlayerId, request.ClientId);
                    SetJoinState(request.PlayerId, RankedJoinState.Idle);
                }

                if (request.ClientId != 0)
                {
                    SendSystemChatToClient($"<size=13><color=#ff6666>Ranked</color> approval failed. Your state was left unchanged.</size>", request.ClientId);
                }

                SendCaptainChatForRequest(request.TargetTeam, $"<size=13><color=#ff6666>Ranked</color> could not move <b>{request.PlayerName}</b> into {FormatTeamLabel(request.TargetTeam)}. The player was left unchanged.</size>");
                return;
            }

            var participant = new RankedParticipant
            {
                clientId = request.ClientId,
                playerId = request.PlayerId,
                displayName = request.PlayerName,
                team = request.TargetTeam,
                isDummy = false
            };

            lock (draftLock)
            {
                draftAssignedTeams[request.PlayerId] = request.TargetTeam;
                pendingLateJoiners.Remove(request.PlayerId);
                announcedLateJoinerIds.Remove(request.PlayerId);
            }

            MergeOrReplaceParticipant(participant, request.TargetTeam);
            ClearRejectedLateJoinState(request.PlayerId);
            SetJoinState(request.PlayerId, RankedJoinState.InTeam);

            var captainName = GetCaptainDisplayNameByKey(captainKey) ?? "Captain";
            if (request.ClientId != 0)
            {
                SendSystemChatToClient($"<size=13><color=#00ff99>Ranked</color> approved for {FormatTeamLabel(request.TargetTeam)}. Select a position to enter the match.</size>", request.ClientId);
            }

            Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Approved: {request.PlayerName} ({request.PlayerId}) -> {FormatTeamLabel(request.TargetTeam)} by {captainName}.");
            SendSystemChatToAll($"<size=14><color=#ffcc66>Ranked</color> {captainName} approved <b>{request.PlayerName}</b> for {FormatTeamLabel(request.TargetTeam)}.</size>");
        }

        private static void ResolveRejectedRequest(TeamApprovalRequest request, string captainKey)
        {
            if (request == null) return;

            var captainName = GetCaptainDisplayNameByKey(captainKey) ?? "Captain";
            SetJoinState(request.PlayerId, RankedJoinState.Rejected);
            if (request.Kind == TeamApprovalRequestKind.TeamSwitch)
            {
                EnsureRejectedSwitchState(request);
                SetJoinState(request.PlayerId, RankedJoinState.InTeam);
                if (request.ClientId != 0)
                {
                    SendSystemChatToClient($"<size=13><color=#ff6666>Ranked</color> {captainName} rejected your switch request. You remain on {FormatTeamLabel(request.PreviousTeam)}.</size>", request.ClientId);
                }

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Rejected: {request.PlayerName} ({request.PlayerId}) switch to {FormatTeamLabel(request.TargetTeam)} by {captainName}.");
                SendSystemChatToAll($"<size=14><color=#ffcc66>Ranked</color> {captainName} rejected {request.PlayerName}'s switch request to {FormatTeamLabel(request.TargetTeam)}.</size>");
                return;
            }

            var rejectedByBothTeams = RegisterRejectedLateJoinTeam(request.PlayerId, request.TargetTeam);
            if (request.ClientId != 0)
            {
                SendSystemChatToClient($"<size=13><color=#ff6666>Ranked</color> {captainName} rejected your request to join {FormatTeamLabel(request.TargetTeam)}.</size>", request.ClientId);
            }

            TryOpenPlayerTeamSelection(request.PlayerId, request.ClientId);
            SetJoinState(request.PlayerId, RankedJoinState.Idle);

            Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Rejected: {request.PlayerName} ({request.PlayerId}) from {FormatTeamLabel(request.TargetTeam)} by {captainName}.");
            SendSystemChatToAll($"<size=14><color=#ffcc66>Ranked</color> {captainName} rejected <b>{request.PlayerName}</b> from joining {FormatTeamLabel(request.TargetTeam)}.</size>");

            if (rejectedByBothTeams)
            {
                Debug.Log($"[{Constants.MOD_NAME}] Player rejected by both teams — removed: {request.PlayerName} ({request.PlayerId})");
                SendSystemChatToAll($"<size=14><color=#ff6666>Ranked</color> {request.PlayerName} was rejected by both teams and removed.</size>");
                TryKickPlayer(request.PlayerId, request.ClientId);
            }
        }

        private static void EnsureRejectedSwitchState(TeamApprovalRequest request)
        {
            if (request == null) return;
            if (request.PreviousTeam != TeamResult.Red && request.PreviousTeam != TeamResult.Blue) return;

            if (!TryResolveConnectedPlayer(request.PlayerId, request.ClientId, out var player, out _, out _))
            {
                return;
            }

            if (!TryGetPlayerTeam(player, out var currentTeam) || currentTeam != request.PreviousTeam)
            {
                TryApplyOfficialTeamJoin(request.PlayerId, request.ClientId, request.PreviousTeam, openPositionSelection: false);
            }
        }

        private static bool TryApplyOfficialTeamJoin(string playerKey, ulong clientId, TeamResult team, bool openPositionSelection = true)
        {
            if (team != TeamResult.Red && team != TeamResult.Blue) return false;

            try
            {
                if (!TryResolveConnectedPlayer(playerKey, clientId, out var player, out var resolvedClientId, out var resolvedPlayerKey))
                {
                    return false;
                }

                var runtimeTeamType = FindTypeByName("PlayerTeam", "Puck.PlayerTeam");
                if (runtimeTeamType == null || !runtimeTeamType.IsEnum)
                {
                    return false;
                }

                var runtimeTeamValue = Enum.Parse(runtimeTeamType, team == TeamResult.Red ? "Red" : "Blue", true);
                RegisterInternalTeamAssignment(resolvedPlayerKey, runtimeTeamValue);

                var applied = false;
                using (BeginForcedTeamAssignment())
                {
                    applied = TryInvokeRpcExecute(player, "Client_SetPlayerTeamRpc", runtimeTeamValue);
                    if (!applied)
                    {
                        applied = TrySetNamedNetworkVariableValue(player, "Team", runtimeTeamValue);
                    }
                }

                if (!applied)
                {
                    return false;
                }

                if (!TryGetPlayerTeam(player, out var actualTeam) || actualTeam != team)
                {
                    return false;
                }

                lock (teamStateLock)
                {
                    lastKnownPlayerTeam[resolvedPlayerKey] = runtimeTeamValue;
                }

                if (openPositionSelection)
                {
                    TryOpenPlayerPositionSelection(player, team);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Official team join failed for {playerKey ?? $"clientId:{clientId}"}: {ex.Message}");
                return false;
            }
        }

        private static void TrySetPlayerWaitingState(string playerKey, ulong clientId)
        {
            try
            {
                if (!TryResolveConnectedPlayer(playerKey, clientId, out var player, out _, out _))
                {
                    return;
                }

                var playerStateType = FindTypeByName("PlayerState", "Puck.PlayerState");
                if (playerStateType == null || !playerStateType.IsEnum)
                {
                    return;
                }

                var spectateState = Enum.Parse(playerStateType, "Spectate", true);
                TryInvokeRpcExecute(player, "Client_SetPlayerStateRpc", spectateState, 0f);
            }
            catch { }
        }

        private static void TryOpenPlayerTeamSelection(string playerKey, ulong clientId)
        {
            try
            {
                if (!TryResolveConnectedPlayer(playerKey, clientId, out var player, out _, out _))
                {
                    return;
                }

                var playerStateType = FindTypeByName("PlayerState", "Puck.PlayerState");
                if (playerStateType == null || !playerStateType.IsEnum)
                {
                    return;
                }

                var teamSelectState = Enum.Parse(playerStateType, "TeamSelect", true);
                TryInvokeRpcExecute(player, "Client_SetPlayerStateRpc", teamSelectState, 0f);
            }
            catch { }
        }

        private static void TryOpenPlayerPositionSelection(object player, TeamResult team)
        {
            if (player == null) return;
            if (team != TeamResult.Red && team != TeamResult.Blue) return;

            try
            {
                var playerStateType = FindTypeByName("PlayerState", "Puck.PlayerState");
                if (playerStateType == null || !playerStateType.IsEnum)
                {
                    return;
                }

                var stateName = team == TeamResult.Red ? "PositionSelectRed" : "PositionSelectBlue";
                var positionSelectState = Enum.Parse(playerStateType, stateName, true);
                TryInvokeRpcExecute(player, "Client_SetPlayerStateRpc", positionSelectState, 0f);
            }
            catch { }
        }

        private static bool TryResolveConnectedPlayer(string playerKey, ulong clientId, out object player, out ulong resolvedClientId, out string resolvedPlayerKey)
        {
            player = null;
            resolvedClientId = 0;
            resolvedPlayerKey = null;

            try
            {
                if (clientId != 0 && TryGetPlayerByClientId(clientId, out var playerByClientId) && playerByClientId != null)
                {
                    player = playerByClientId;
                    resolvedClientId = clientId;
                    resolvedPlayerKey = ResolvePlayerObjectKey(playerByClientId, clientId) ?? playerKey;
                    return true;
                }

                var normalizedKey = ResolveStoredIdToSteam(playerKey ?? string.Empty);
                foreach (var candidate in GetAllPlayers())
                {
                    if (candidate == null) continue;
                    var candidateKey = ResolvePlayerObjectKey(candidate, 0UL);
                    var candidateStoredKey = TryGetPlayerId(candidate, 0UL);
                    if (!string.Equals(candidateKey, normalizedKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(candidateStoredKey, normalizedKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(candidateStoredKey, playerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    player = candidate;
                    TryGetClientId(candidate, out resolvedClientId);
                    resolvedPlayerKey = candidateKey ?? candidateStoredKey ?? playerKey;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryGetCaptainClientIdForTeam(TeamResult team, out ulong clientId, out string captainKey, out string captainName)
        {
            EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: false);

            clientId = 0;
            captainKey = team == TeamResult.Red ? redCaptainId : team == TeamResult.Blue ? blueCaptainId : null;
            captainName = GetCaptainDisplayNameByKey(captainKey);
            if (string.IsNullOrWhiteSpace(captainKey)) return false;

            if (TryGetParticipantByKey(captainKey, out var participant) && participant != null && participant.clientId != 0)
            {
                if (TryGetPlayerByClientId(participant.clientId, out _))
                {
                    clientId = participant.clientId;
                    return true;
                }
            }

            if (TryResolveConnectedPlayer(captainKey, 0UL, out var player, out var resolvedClientId, out _))
            {
                if (player != null && resolvedClientId != 0)
                {
                    clientId = resolvedClientId;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetCaptainTeamForClient(ulong clientId, out TeamResult team)
        {
            team = TeamResult.Unknown;
            try
            {
                if (clientId == 0) return false;
                if (!TryGetPlayerByClientId(clientId, out var player) || player == null) return false;
                var playerKey = ResolvePlayerObjectKey(player, clientId);
                return TryGetCaptainTeam(playerKey, out team);
            }
            catch { }

            return false;
        }

        private static bool TryGetCaptainIdentityForClient(ulong clientId, out TeamResult team, out string captainKey)
        {
            team = TeamResult.Unknown;
            captainKey = null;

            try
            {
                EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: false);

                if (clientId == 0) return false;
                if (!TryGetPlayerByClientId(clientId, out var player) || player == null) return false;

                captainKey = GetPlayerKey(player, clientId);
                if (string.IsNullOrWhiteSpace(captainKey)) return false;
                return TryGetCaptainTeam(captainKey, out team);
            }
            catch { }

            return false;
        }

        private static bool TryResolveApprovalRequestCaptain(TeamApprovalRequest request, out ulong clientId, out string captainKey)
        {
            clientId = 0;
            captainKey = null;
            if (request == null) return false;

            try
            {
                if (TryGetCaptainClientIdForTeam(request.TargetTeam, out var currentCaptainClientId, out var currentCaptainKey, out _))
                {
                    request.TargetCaptainKey = currentCaptainKey;
                    request.TargetCaptainClientId = currentCaptainClientId;
                    clientId = currentCaptainClientId;
                    captainKey = currentCaptainKey;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsApprovalRequestOwner(string actorKey, ulong clientId, TeamApprovalRequest request)
        {
            if (request == null) return false;

            if (!string.IsNullOrWhiteSpace(actorKey) && !string.IsNullOrWhiteSpace(request.TargetCaptainKey)
                && string.Equals(actorKey, request.TargetCaptainKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return clientId != 0 && request.TargetCaptainClientId != 0 && clientId == request.TargetCaptainClientId;
        }

        private static TeamApprovalRequest GetNextApprovalRequestForCaptainLocked(string captainKey, ulong clientId)
        {
            return pendingTeamApprovalRequests.Values
                .Where(request => request != null)
                .Where(request =>
                    (!string.IsNullOrWhiteSpace(captainKey)
                        && !string.IsNullOrWhiteSpace(request.TargetCaptainKey)
                        && string.Equals(request.TargetCaptainKey, captainKey, StringComparison.OrdinalIgnoreCase))
                    || (clientId != 0 && request.TargetCaptainClientId == clientId))
                .OrderBy(request => request.Timestamp)
                .ThenBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static TeamApprovalRequest CloneApprovalRequest(TeamApprovalRequest request)
        {
            if (request == null) return null;
            return new TeamApprovalRequest
            {
                RequestId = request.RequestId,
                PlayerId = request.PlayerId,
                ClientId = request.ClientId,
                PlayerName = request.PlayerName,
                TargetTeam = request.TargetTeam,
                PreviousTeam = request.PreviousTeam,
                PreviousPositionKey = request.PreviousPositionKey,
                Timestamp = request.Timestamp,
                Kind = request.Kind,
                TargetCaptainKey = request.TargetCaptainKey,
                TargetCaptainClientId = request.TargetCaptainClientId
            };
        }

        private static bool RegisterRejectedLateJoinTeam(string playerId, TeamResult team)
        {
            if (string.IsNullOrWhiteSpace(playerId) || (team != TeamResult.Red && team != TeamResult.Blue))
            {
                return false;
            }

            lock (approvalRequestLock)
            {
                if (!rejectedLateJoinTeams.TryGetValue(playerId, out var rejectedTeams) || rejectedTeams == null)
                {
                    rejectedTeams = new HashSet<TeamResult>();
                    rejectedLateJoinTeams[playerId] = rejectedTeams;
                }

                rejectedTeams.Add(team);
                return rejectedTeams.Contains(TeamResult.Red) && rejectedTeams.Contains(TeamResult.Blue);
            }
        }

        private static void ClearRejectedLateJoinState(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;
            lock (approvalRequestLock)
            {
                rejectedLateJoinTeams.Remove(playerId);
            }
        }

        private static void RefreshCaptainApprovalPanels()
        {
            try
            {
                PublishApprovalRequestPanelForTeam(TeamResult.Red);
                PublishApprovalRequestPanelForTeam(TeamResult.Blue);
            }
            catch { }
        }

        private static void PublishHiddenApprovalStateToAllClients()
        {
            try
            {
                foreach (var player in GetAllPlayers())
                {
                    if (!TryGetClientId(player, out var clientId) || clientId == 0) continue;
                    RankedOverlayNetwork.PublishApprovalRequestStateToClient(clientId, ApprovalRequestStateMessage.Hidden());
                }
            }
            catch { }
        }

        private static void PublishApprovalRequestPanelForTeam(TeamResult team)
        {
            try
            {
                if (!TryGetCaptainClientIdForTeam(team, out var clientId, out _, out _))
                {
                    return;
                }

                RankedOverlayNetwork.PublishApprovalRequestStateToClient(clientId, GetApprovalRequestStateForClient(clientId));
            }
            catch { }
        }

        private static void SendCaptainChatForRequest(TeamResult team, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            try
            {
                if (TryGetCaptainClientIdForTeam(team, out var clientId, out _, out _))
                {
                    SendSystemChatToClient(message, clientId);
                }
            }
            catch { }
        }

        private static bool TryInvokeRpcExecute(object target, string methodName, params object[] arguments)
        {
            if (target == null || string.IsNullOrWhiteSpace(methodName)) return false;

            try
            {
                var method = target.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(candidate => string.Equals(candidate.Name, methodName, StringComparison.Ordinal)
                        && candidate.GetParameters().Length == (arguments?.Length ?? 0));
                if (method == null) return false;

                var rpcExecStageField = FindFieldRecursive(target.GetType(), "__rpc_exec_stage");
                object originalStage = null;
                var forcedExecuteStage = false;
                if (rpcExecStageField != null)
                {
                    try
                    {
                        originalStage = rpcExecStageField.GetValue(target);
                        var executeStageValue = Enum.Parse(rpcExecStageField.FieldType, "Execute", true);
                        rpcExecStageField.SetValue(target, executeStageValue);
                        forcedExecuteStage = true;
                    }
                    catch { }
                }

                try
                {
                    method.Invoke(target, arguments);
                    return true;
                }
                finally
                {
                    if (forcedExecuteStage)
                    {
                        try { rpcExecStageField.SetValue(target, originalStage); } catch { }
                    }
                }
            }
            catch { }

            return false;
        }

        private static FieldInfo FindFieldRecursive(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null) return field;
                type = type.BaseType;
            }

            return null;
        }

        private static bool TrySetNamedNetworkVariableValue(object instance, string memberName, object rawValue)
        {
            if (instance == null || string.IsNullOrWhiteSpace(memberName)) return false;

            try
            {
                var type = instance.GetType();
                object memberValue = null;

                var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    memberValue = property.GetValue(instance);
                }

                if (memberValue == null)
                {
                    var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        memberValue = field.GetValue(instance);
                    }
                }

                if (memberValue == null) return false;

                var valueProperty = memberValue.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (valueProperty == null || !valueProperty.CanWrite) return false;

                var convertedValue = ConvertTeamValueForType(rawValue, valueProperty.PropertyType);
                if (convertedValue == null)
                {
                    convertedValue = ConvertGenericValue(rawValue, valueProperty.PropertyType);
                }

                if (convertedValue == null) return false;
                valueProperty.SetValue(memberValue, convertedValue);
                return true;
            }
            catch { }

            return false;
        }

        private static object ConvertGenericValue(object rawValue, Type targetType)
        {
            try
            {
                if (targetType == null) return null;
                if (rawValue == null)
                {
                    return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
                }

                if (targetType.IsInstanceOfType(rawValue)) return rawValue;

                if (targetType.IsEnum)
                {
                    return Enum.Parse(targetType, rawValue.ToString(), true);
                }

                return Convert.ChangeType(rawValue, targetType);
            }
            catch { }

            return null;
        }

        private static bool TryKickPlayer(string playerKey, ulong clientId)
        {
            try
            {
                if (!TryResolveConnectedPlayer(playerKey, clientId, out var player, out _, out _))
                {
                    return false;
                }

                var serverManagerType = FindTypeByName("ServerManager", "Puck.ServerManager");
                var serverManager = GetManagerInstance(serverManagerType);
                if (serverManager == null) return false;

                var disconnectionCodeType = FindTypeByName("DisconnectionCode", "Puck.DisconnectionCode");
                var kickCode = disconnectionCodeType != null && disconnectionCodeType.IsEnum
                    ? Enum.Parse(disconnectionCodeType, "Kicked", true)
                    : null;
                var kickMethod = serverManager.GetType().GetMethod("Server_KickPlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (kickMethod == null) return false;

                var parameters = kickMethod.GetParameters();
                if (parameters.Length >= 3 && kickCode != null)
                {
                    kickMethod.Invoke(serverManager, new[] { player, kickCode, (object)false });
                    return true;
                }

                kickMethod.Invoke(serverManager, new[] { player });
                return true;
            }
            catch { }

            return false;
        }

        private static string TryGetCurrentPositionKey(object player)
        {
            if (player == null) return null;

            try
            {
                object playerPosition = null;
                var type = player.GetType();
                var property = type.GetProperty("PlayerPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null)
                {
                    playerPosition = property.GetValue(player);
                }

                if (playerPosition == null)
                {
                    var field = type.GetField("PlayerPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        playerPosition = field.GetValue(player);
                    }
                }

                if (playerPosition == null) return null;

                var positionType = playerPosition.GetType();
                foreach (var memberName in new[] { "Name", "name", "PositionKey", "positionKey" })
                {
                    var positionProperty = positionType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (positionProperty != null)
                    {
                        var value = positionProperty.GetValue(playerPosition)?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }

                    var positionField = positionType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (positionField != null)
                    {
                        var value = positionField.GetValue(playerPosition)?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) return value;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}