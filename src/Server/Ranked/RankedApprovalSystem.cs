using System;
using System.Collections;
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
        private const float ApprovalRequestTimeoutSeconds = 15f;
        private const float ApprovalResultNotificationDurationSeconds = 4.5f;
        private const float ApprovalRejectCooldownSeconds = 7f;
        private static readonly Dictionary<string, TeamApprovalRequest> pendingTeamApprovalRequests = new Dictionary<string, TeamApprovalRequest>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ApprovalRequestNotification> approvalNotificationsByPlayerKey = new Dictionary<string, ApprovalRequestNotification>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, float> rejectedRequestCooldownEndsAtByPlayerKey = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
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

        private sealed class ApprovalRequestNotification
        {
            public string PlayerId;
            public ulong ClientId;
            public string RequestId;
            public string PlayerName;
            public TeamResult TargetTeam;
            public TeamResult PreviousTeam;
            public TeamApprovalRequestKind Kind;
            public string CaptainName;
            public ApprovalRequestDisplayStatus Status;
            public float ExpiresAt;
            public float CooldownEndsAt;
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
                if (TryGetCaptainIdentityForClient(clientId, out _, out var captainKey))
                {
                    TeamApprovalRequest captainRequest;
                    int queueLength;
                    lock (approvalRequestLock)
                    {
                        captainRequest = CloneApprovalRequest(GetNextApprovalRequestForCaptainLocked(captainKey, clientId));
                        queueLength = CountApprovalRequestsForCaptainLocked(captainKey, clientId);
                    }

                    if (captainRequest != null)
                    {
                        return BuildCaptainApprovalState(captainRequest, queueLength);
                    }
                }

                if (clientId != 0)
                {
                    TeamApprovalRequest targetedCaptainRequest;
                    int targetedQueueLength;
                    lock (approvalRequestLock)
                    {
                        targetedCaptainRequest = CloneApprovalRequest(GetNextApprovalRequestForCaptainLocked(null, clientId));
                        targetedQueueLength = CountApprovalRequestsForCaptainLocked(null, clientId);
                    }

                    if (targetedCaptainRequest != null)
                    {
                        return BuildCaptainApprovalState(targetedCaptainRequest, targetedQueueLength);
                    }
                }

                if (!TryGetApprovalPlayerKeyForClient(clientId, out var playerKey))
                {
                    return ApprovalRequestStateMessage.Hidden();
                }

                TeamApprovalRequest requesterPendingRequest;
                ApprovalRequestNotification requesterNotification;
                lock (approvalRequestLock)
                {
                    requesterPendingRequest = CloneApprovalRequest(GetPendingApprovalRequestForPlayerLocked(playerKey, clientId));
                    requesterNotification = CloneApprovalRequestNotification(GetApprovalNotificationLocked(playerKey, clientId));
                }

                if (requesterPendingRequest != null)
                {
                    return BuildRequesterPendingState(requesterPendingRequest);
                }

                if (requesterNotification != null)
                {
                    return BuildRequesterNotificationState(requesterNotification);
                }

                return ApprovalRequestStateMessage.Hidden();
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
                List<TeamApprovalRequest> playerDisconnectedRequests = null;
                List<TeamApprovalRequest> captainUnavailableRequests = null;
                List<TeamApprovalRequest> expiredRequests = null;
                List<ApprovalRequestNotification> expiredNotifications = null;

                lock (approvalRequestLock)
                {
                    TrimRejectedRequestCooldownsLocked(Time.unscaledTime);

                    foreach (var notification in approvalNotificationsByPlayerKey.Values.ToList())
                    {
                        if (notification == null)
                        {
                            continue;
                        }

                        if (notification.ExpiresAt > Time.unscaledTime)
                        {
                            continue;
                        }

                        if (expiredNotifications == null) expiredNotifications = new List<ApprovalRequestNotification>();
                        expiredNotifications.Add(CloneApprovalRequestNotification(notification));
                        approvalNotificationsByPlayerKey.Remove(notification.PlayerId);
                    }

                    foreach (var request in pendingTeamApprovalRequests.Values.ToList())
                    {
                        if (request == null) continue;

                        if (GetApprovalSecondsRemaining(request) <= 0f)
                        {
                            if (expiredRequests == null) expiredRequests = new List<TeamApprovalRequest>();
                            expiredRequests.Add(CloneApprovalRequest(request));
                            pendingTeamApprovalRequests.Remove(request.RequestId);
                            continue;
                        }

                        if (ShouldAutoApproveTeamSelection(request.TargetTeam, request.PlayerId))
                        {
                            ResolveApprovedRequest(CloneApprovalRequest(request), null);
                            pendingTeamApprovalRequests.Remove(request.RequestId);
                            continue;
                        }

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

                if (expiredRequests != null)
                {
                    foreach (var request in expiredRequests)
                    {
                        ResolveExpiredRequest(request);
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
                        RegisterApprovalNotification(request, ApprovalRequestDisplayStatus.Cancelled, "System");

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

                if (expiredRequests != null || playerDisconnectedRequests != null || captainUnavailableRequests != null || expiredNotifications != null)
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
                hadRequests = pendingTeamApprovalRequests.Count > 0 || rejectedLateJoinTeams.Count > 0 || approvalNotificationsByPlayerKey.Count > 0;
                pendingTeamApprovalRequests.Clear();
                approvalNotificationsByPlayerKey.Clear();
                rejectedRequestCooldownEndsAtByPlayerKey.Clear();
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

                var targetTeam = ResolveRequestedTeamSelectionValue(requestedTeam);
                if (targetTeam != TeamResult.Red && targetTeam != TeamResult.Blue)
                {
                    if (IsTeamNoneLike(currentTeam))
                    {
                        return false;
                    }

                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Ignoring non-playable in-match team selection request: requested={FormatTeamValue(requestedTeam)} current={FormatTeamValue(currentTeam)}.");
                    return true;
                }

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN-DEBUG] phase={GetTrackedPhaseName() ?? "unknown"} replayPhase={IsReplayPlaybackPhaseActive().ToString().ToLowerInvariant()} clientId={clientId} current={FormatTeamValue(currentTeam)} requested={FormatTeamValue(requestedTeam)} approvalRequired=true");

                if (TryQueueTeamApprovalRequest(player, clientId, targetTeam))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static TeamResult ResolveRequestedTeamSelectionValue(object requestedTeam)
        {
            if (requestedTeam == null)
            {
                return TeamResult.Unknown;
            }

            try
            {
                if (requestedTeam is Enum requestedEnum)
                {
                    var enumName = requestedEnum.ToString();
                    if (!string.IsNullOrWhiteSpace(enumName))
                    {
                        var normalizedName = enumName.ToLowerInvariant();
                        if (normalizedName.Contains("blue")) return TeamResult.Blue;
                        if (normalizedName.Contains("red")) return TeamResult.Red;
                        return TeamResult.Unknown;
                    }

                    var enumNumericValue = Convert.ToInt64(requestedEnum);
                    if (enumNumericValue == 2L) return TeamResult.Blue;
                    if (enumNumericValue == 3L) return TeamResult.Red;
                    return TeamResult.Unknown;
                }

                if (requestedTeam is int menuIndex)
                {
                    if (menuIndex == 0 || menuIndex == 2) return TeamResult.Blue;
                    if (menuIndex == 1 || menuIndex == 3) return TeamResult.Red;
                    return TeamResult.Unknown;
                }

                if (requestedTeam is uint unsignedMenuIndex)
                {
                    if (unsignedMenuIndex == 0U || unsignedMenuIndex == 2U) return TeamResult.Blue;
                    if (unsignedMenuIndex == 1U || unsignedMenuIndex == 3U) return TeamResult.Red;
                    return TeamResult.Unknown;
                }

                if (requestedTeam is long longMenuIndex)
                {
                    if (longMenuIndex == 0L || longMenuIndex == 2L) return TeamResult.Blue;
                    if (longMenuIndex == 1L || longMenuIndex == 3L) return TeamResult.Red;
                    return TeamResult.Unknown;
                }

                if (requestedTeam is ulong unsignedLongMenuIndex)
                {
                    if (unsignedLongMenuIndex == 0UL || unsignedLongMenuIndex == 2UL) return TeamResult.Blue;
                    if (unsignedLongMenuIndex == 1UL || unsignedLongMenuIndex == 3UL) return TeamResult.Red;
                    return TeamResult.Unknown;
                }
            }
            catch { }

            return ConvertTeamValue(requestedTeam);
        }

        private static TeamResult ResolveEffectiveCurrentTeamForSelection(object player, ulong clientId, object currentTeam)
        {
            try
            {
                var resolvedTeam = ConvertTeamValue(currentTeam);
                if (resolvedTeam == TeamResult.Red || resolvedTeam == TeamResult.Blue)
                {
                    return resolvedTeam;
                }

                if (player == null && clientId != 0)
                {
                    TryGetPlayerByClientId(clientId, out player);
                }

                if (TryGetPlayerTeam(player, out var liveTeam) && (liveTeam == TeamResult.Red || liveTeam == TeamResult.Blue))
                {
                    return liveTeam;
                }

                if (TryGetPlayerTeamFromManager(clientId, out var managerTeam) && (managerTeam == TeamResult.Red || managerTeam == TeamResult.Blue))
                {
                    return managerTeam;
                }

                var playerKey = TryGetPlayerId(player, clientId);
                if (!string.IsNullOrWhiteSpace(playerKey))
                {
                    lock (teamStateLock)
                    {
                        if (lastKnownPlayerTeam.TryGetValue(playerKey, out var lastKnownTeamValue))
                        {
                            var lastKnownTeam = ConvertTeamValue(lastKnownTeamValue);
                            if (lastKnownTeam == TeamResult.Red || lastKnownTeam == TeamResult.Blue)
                            {
                                return lastKnownTeam;
                            }
                        }
                    }
                }
            }
            catch { }

            return ConvertTeamValue(currentTeam);
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

                return false;
            }
            catch { }

            return false;
        }

        private static bool TryQueueTeamApprovalRequest(object player, ulong clientId, TeamResult targetTeam, string explicitCaptainKey = null, ulong explicitCaptainClientId = 0UL, string explicitCaptainName = null, bool allowSelfTargetedCaptainAutoApprove = true)
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
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Could not resolve your player for a join request.", ChatTone.Error, 13), clientId);
                    return true;
                }

                var playerKey = ResolveParticipantIdToKey(snapshot);
                if (string.IsNullOrWhiteSpace(playerKey))
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Could not resolve your player key for a join request.", ChatTone.Error, 13), clientId);
                    return true;
                }

                var currentTeam = ResolveEffectiveCurrentTeamForSelection(player, snapshot.clientId, snapshot.team);
                var isSwitchRequest = currentTeam == TeamResult.Red || currentTeam == TeamResult.Blue;
                var isSameTeamRequest = isSwitchRequest && currentTeam == targetTeam;

                if (IsPendingJoinState(playerKey))
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"You already have a pending request for {ChatStyle.Team(targetTeam)}.", ChatTone.Warning, 13), clientId);
                    return true;
                }

                if (TryGetRejectedRequestCooldownRemaining(playerKey, out var cooldownSecondsRemaining))
                {
                    var cooldownMessage = $"<size=13><color=#ffcc66>Ranked</color> Please wait {cooldownSecondsRemaining:0.0}s before sending another team request.</size>";
                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN][COOLDOWN] blocked requester={playerKey} clientId={snapshot.clientId} remaining={cooldownSecondsRemaining:0.0}s target={FormatTeamLabel(targetTeam)}");
                    SendSystemChatToClient(cooldownMessage, clientId);
                    RegisterCooldownNotification(playerKey, snapshot.clientId, requestId: string.Empty, snapshot.displayName ?? $"Player {snapshot.clientId}", targetTeam, currentTeam, isSwitchRequest ? TeamApprovalRequestKind.TeamSwitch : TeamApprovalRequestKind.LateJoin, cooldownSecondsRemaining);
                    RefreshCaptainApprovalPanels();
                    return true;
                }

                ClearApprovalNotification(playerKey);

                TeamApprovalRequest existingRequest;
                lock (approvalRequestLock)
                {
                    existingRequest = pendingTeamApprovalRequests.Values.FirstOrDefault(existing =>
                        existing != null && string.Equals(existing.PlayerId, playerKey, StringComparison.OrdinalIgnoreCase));
                }

                if (existingRequest != null)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"You already have a pending request for {ChatStyle.Team(existingRequest.TargetTeam)}.", ChatTone.Warning, 13), clientId);
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
                    Kind = isSwitchRequest ? TeamApprovalRequestKind.TeamSwitch : TeamApprovalRequestKind.LateJoin
                };

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN-DEBUG] queueRequest phase={GetTrackedPhaseName() ?? "unknown"} replayPhase={IsReplayPlaybackPhaseActive().ToString().ToLowerInvariant()} player={request.PlayerId} target={FormatTeamLabel(targetTeam)} previous={FormatTeamLabel(currentTeam)} switch={isSwitchRequest.ToString().ToLowerInvariant()}");

                if (ShouldAutoApproveTeamSelection(targetTeam, playerKey))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Auto-approving request {request.RequestId} for {request.PlayerName} -> {FormatTeamLabel(targetTeam)} because no other approver is available on that team.");
                    ResolveApprovedRequest(CloneApprovalRequest(request), null);
                    RefreshCaptainApprovalPanels();
                    return true;
                }

                var captainClientId = explicitCaptainClientId;
                var captainKey = explicitCaptainKey;
                var captainName = explicitCaptainName;
                if ((captainClientId == 0 && string.IsNullOrWhiteSpace(captainKey))
                    && !TryGetCaptainClientIdForTeam(targetTeam, out captainClientId, out captainKey, out captainName))
                {
                    SetJoinState(playerKey, isSwitchRequest ? RankedJoinState.InTeam : RankedJoinState.Idle);
                    if (!isSwitchRequest)
                    {
                        TryOpenPlayerTeamSelection(playerKey, snapshot.clientId);
                    }

                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"The {ChatStyle.Team(targetTeam)} captain is unavailable right now. Try again when that captain reconnects.", ChatTone.Warning, 13), clientId);
                    return true;
                }

                if (allowSelfTargetedCaptainAutoApprove && string.Equals(captainKey, playerKey, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Auto-approving self-owned captain request {request.RequestId} for {snapshot.displayName} ({playerKey}) targeting {FormatTeamLabel(targetTeam)}.");
                    ResolveApprovedRequest(CloneApprovalRequest(request), null);
                    RefreshCaptainApprovalPanels();
                    return true;
                }

                if (!controlledTestModeEnabled && (IsDummyKey(captainKey) || BotManager.IsBotKey(captainKey)))
                {
                    Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Auto-approving request {request.RequestId} for {request.PlayerName} -> {FormatTeamLabel(targetTeam)} because the only available captain is a dummy/bot.");
                    ResolveApprovedRequest(CloneApprovalRequest(request), null);
                    RefreshCaptainApprovalPanels();
                    return true;
                }

                request.TargetCaptainKey = captainKey;
                request.TargetCaptainClientId = captainClientId;

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

                var requestLabel = isSameTeamRequest ? "rejoin" : isSwitchRequest ? "switch" : "join";
                Debug.Log($"[{Constants.MOD_NAME}] [JOIN][COOLDOWN] accepted requester={playerKey} clientId={snapshot.clientId} target={FormatTeamLabel(targetTeam)} cooldownExpired=true");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Safe(requestLabel)} request sent to the {ChatStyle.Team(targetTeam)} captain.", ChatTone.Info, 13), clientId);
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(request.PlayerName)} wants to {(isSameTeamRequest ? $"re-enter {ChatStyle.Team(targetTeam)}" : isSwitchRequest ? $"switch into {ChatStyle.Team(targetTeam)}" : $"join {ChatStyle.Team(targetTeam)}")}. Use the approval popup.", ChatTone.Warning, 13), captainClientId);
                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Sent to captain {captainName ?? captainKey ?? "unknown"}: request {request.RequestId} for {request.PlayerName} -> {FormatTeamLabel(targetTeam)}.");
                RefreshCaptainApprovalPanels();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{Constants.MOD_NAME}] Failed to queue team approval request: {ex.Message}");
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Could not create a team approval request.", ChatTone.Error, 13), clientId);
                return true;
            }
        }

        private static bool ShouldAutoApproveTeamSelection(TeamResult targetTeam, string requesterPlayerKey)
        {
            if (targetTeam != TeamResult.Red && targetTeam != TeamResult.Blue)
            {
                return false;
            }

            try
            {
                return GetConnectedHumanTeamMemberKeys(targetTeam, requesterPlayerKey).Count == 0;
            }
            catch { }

            return false;
        }

        private static List<string> GetConnectedHumanTeamMemberKeys(TeamResult targetTeam, string excludedPlayerKey = null)
        {
            var connectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var player in GetAllPlayers())
                {
                    if (!TryBuildConnectedPlayerSnapshot(player, out var snapshot) || snapshot == null)
                    {
                        continue;
                    }

                    var playerKey = ResolveParticipantIdToKey(snapshot);
                    if (string.IsNullOrWhiteSpace(playerKey))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(excludedPlayerKey)
                        && string.Equals(playerKey, excludedPlayerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var snapshotTeam = ResolveEffectiveCurrentTeamForSelection(player, snapshot.clientId, snapshot.team);
                    if (snapshotTeam != targetTeam)
                    {
                        continue;
                    }

                    connectedKeys.Add(playerKey);
                }
            }
            catch { }

            return connectedKeys.ToList();
        }

        private static void HandleApprovalDecision(object player, ulong clientId, string requestId, bool approved)
        {
            try
            {
                if (draftActive)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "There is no active approval flow right now.", ChatTone.Warning, 13), clientId);
                    return;
                }

                if (!rankedActive)
                {
                    TeamApprovalRequest targetedRequest;
                    lock (approvalRequestLock)
                    {
                        targetedRequest = GetNextApprovalRequestForCaptainLocked(null, clientId);
                    }

                    if (targetedRequest == null)
                    {
                        SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "There is no active approval flow right now.", ChatTone.Warning, 13), clientId);
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(requestId))
                {
                    SendSystemChatToClient(ChatStyle.Usage($"{(approved ? "/approve" : "/reject")} <requestId>"), clientId);
                    return;
                }

                EnsureValidCaptainAssignments(publishOverlayState: false, refreshApprovalPanels: false);

                var actorKey = GetPlayerKey(player, clientId);
                var isOfficialCaptain = TryGetCaptainTeam(actorKey, out _);
                if (!isOfficialCaptain && clientId == 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Only team captains can approve or reject requests.", ChatTone.Error, 13), clientId);
                    return;
                }

                TeamApprovalRequest request;
                lock (approvalRequestLock)
                {
                    if (!pendingTeamApprovalRequests.TryGetValue(requestId.Trim(), out request) || request == null)
                    {
                        SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Request not found.", ChatTone.Error, 13), clientId);
                        return;
                    }

                    TryResolveApprovalRequestCaptain(request, out _, out _);

                    if (!IsApprovalRequestOwner(actorKey, clientId, request))
                    {
                        SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "That request belongs to the other captain.", ChatTone.Error, 13), clientId);
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
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Could not process that request.", ChatTone.Error, 13), clientId);
            }
        }

        private static void ResolveApprovedRequest(TeamApprovalRequest request, string captainKey)
        {
            if (request == null) return;

            SetJoinState(request.PlayerId, RankedJoinState.Approved);
            Debug.Log($"[{Constants.MOD_NAME}] [JOIN] request approved for player {request.PlayerName} ({request.PlayerId}) targeting {FormatTeamLabel(request.TargetTeam)}.");

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
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, "Approval failed. Your state was left unchanged.", ChatTone.Error, 13), request.ClientId);
                }

                RegisterApprovalNotification(request, ApprovalRequestDisplayStatus.Cancelled, GetCaptainDisplayNameByKey(captainKey) ?? "Captain");
                SendCaptainChatForRequest(request.TargetTeam, ChatStyle.Message(ChatStyle.RankedModule, $"Could not move {ChatStyle.Player(request.PlayerName)} into {ChatStyle.Team(request.TargetTeam)}. The player was left unchanged.", ChatTone.Error, 13));
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
            RecordApprovedLateJoinForCurrentMatch(request);
            PublishScoreboardStarState();

            var captainName = string.IsNullOrWhiteSpace(captainKey)
                ? "System"
                : GetCaptainDisplayNameByKey(captainKey) ?? "Captain";
            if (request.ClientId != 0)
            {
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"Approved for {ChatStyle.Team(request.TargetTeam)}. Select a position to enter the match.", ChatTone.Success, 13), request.ClientId);
            }

            RegisterApprovalNotification(request, ApprovalRequestDisplayStatus.Approved, captainName);
            Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Approved: {request.PlayerName} ({request.PlayerId}) -> {FormatTeamLabel(request.TargetTeam)} by {captainName}.");
            SendSystemChatToAll(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(captainName)} approved {ChatStyle.Player(request.PlayerName)} for {ChatStyle.Team(request.TargetTeam)}.", ChatTone.Info));
        }

        private static void ResolveRejectedRequest(TeamApprovalRequest request, string captainKey)
        {
            if (request == null) return;

            var captainName = GetCaptainDisplayNameByKey(captainKey) ?? "Captain";
            SetJoinState(request.PlayerId, RankedJoinState.Rejected);
            StartRejectedRequestCooldown(request);
            RegisterApprovalNotification(request, ApprovalRequestDisplayStatus.Rejected, captainName);
            if (request.Kind == TeamApprovalRequestKind.TeamSwitch)
            {
                EnsureRejectedSwitchState(request);
                SetJoinState(request.PlayerId, RankedJoinState.InTeam);
                if (request.ClientId != 0)
                {
                    SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(captainName)} rejected your switch request. You remain on {ChatStyle.Team(request.PreviousTeam)}.", ChatTone.Error, 13), request.ClientId);
                }

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Rejected: {request.PlayerName} ({request.PlayerId}) switch to {FormatTeamLabel(request.TargetTeam)} by {captainName}.");
                SendSystemChatToAll(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(captainName)} rejected {ChatStyle.Player(request.PlayerName)}'s switch request to {ChatStyle.Team(request.TargetTeam)}.", ChatTone.Info));
                return;
            }

            var rejectedByBothTeams = RegisterRejectedLateJoinTeam(request.PlayerId, request.TargetTeam);
            if (request.ClientId != 0)
            {
                SendSystemChatToClient(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(captainName)} rejected your request to join {ChatStyle.Team(request.TargetTeam)}.", ChatTone.Error, 13), request.ClientId);
            }

            TryOpenPlayerTeamSelection(request.PlayerId, request.ClientId);
            SetJoinState(request.PlayerId, RankedJoinState.Idle);

            Debug.Log($"[{Constants.MOD_NAME}] [JOIN] Rejected: {request.PlayerName} ({request.PlayerId}) from {FormatTeamLabel(request.TargetTeam)} by {captainName}.");
            SendSystemChatToAll(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(captainName)} rejected {ChatStyle.Player(request.PlayerName)} from joining {ChatStyle.Team(request.TargetTeam)}.", ChatTone.Info));

            if (rejectedByBothTeams)
            {
                Debug.Log($"[{Constants.MOD_NAME}] Player rejected by both teams — removed: {request.PlayerName} ({request.PlayerId})");
                SendSystemChatToAll(ChatStyle.Message(ChatStyle.RankedModule, $"{ChatStyle.Player(request.PlayerName)} was rejected by both teams and removed.", ChatTone.Error));
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

        private static bool TryApplyOfficialTeamJoin(string playerKey, ulong clientId, TeamResult team, bool openPositionSelection = true, string preferredPositionKey = null)
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
                var desiredPositionKey = NormalizePositionKeyForTeam(preferredPositionKey, team);
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

                var assignmentVerified = TryGetPlayerTeam(player, out var actualTeam) && actualTeam == team;
                if (!assignmentVerified && resolvedClientId != 0)
                {
                    assignmentVerified = TryGetPlayerTeamFromManager(resolvedClientId, out actualTeam) && actualTeam == team;
                }

                if (!assignmentVerified)
                {
                    var playerName = TryGetPlayerName(player);
                    if (!string.IsNullOrWhiteSpace(playerName))
                    {
                        assignmentVerified = TryGetTeamFromScoreboard(resolvedClientId, playerName, out actualTeam) && actualTeam == team;
                    }
                }

                if (!assignmentVerified)
                {
                    Debug.LogWarning($"[{Constants.MOD_NAME}] [JOIN] immediate team verification lagged after approval for {resolvedPlayerKey ?? playerKey ?? $"clientId:{resolvedClientId}"}. Continuing with applied internal assignment to {FormatTeamLabel(team)}.");
                }

                lock (teamStateLock)
                {
                    lastKnownPlayerTeam[resolvedPlayerKey] = runtimeTeamValue;
                }

                Debug.Log($"[{Constants.MOD_NAME}] [JOIN] assigned to team {FormatTeamLabel(team)} for {resolvedPlayerKey ?? playerKey ?? $"clientId:{resolvedClientId}"}.");

                var restoredPosition = false;
                if (!string.IsNullOrWhiteSpace(desiredPositionKey))
                {
                    restoredPosition = TryClaimPlayerPositionByKey(player, runtimeTeamValue, desiredPositionKey);
                    if (restoredPosition)
                    {
                        Debug.Log($"[{Constants.MOD_NAME}] [JOIN] preserved position {desiredPositionKey} for {resolvedPlayerKey ?? playerKey ?? $"clientId:{resolvedClientId}"} during team assignment.");
                    }
                }

                if (!restoredPosition && openPositionSelection)
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

        private static string NormalizePositionKeyForTeam(string positionKey, TeamResult team)
        {
            if (string.IsNullOrWhiteSpace(positionKey) || (team != TeamResult.Red && team != TeamResult.Blue))
            {
                return null;
            }

            var targetPrefix = team == TeamResult.Red ? "RedPositions:" : "BluePositions:";
            var separatorIndex = positionKey.IndexOf(':');
            if (separatorIndex < 0)
            {
                return positionKey;
            }

            var suffix = positionKey.Substring(separatorIndex + 1);
            if (positionKey.StartsWith("RedPositions:", StringComparison.OrdinalIgnoreCase)
                || positionKey.StartsWith("BluePositions:", StringComparison.OrdinalIgnoreCase))
            {
                return targetPrefix + suffix;
            }

            return positionKey;
        }

        private static bool TryClaimPlayerPositionByKey(object player, object teamValue, string preferredPositionKey)
        {
            if (player == null || teamValue == null || string.IsNullOrWhiteSpace(preferredPositionKey))
            {
                return false;
            }

            try
            {
                var ppmType = FindTypeByName("PlayerPositionManager", "Puck.PlayerPositionManager");
                if (ppmType == null)
                {
                    return false;
                }

                var ppm = GetManagerInstance(ppmType);
                if (ppm == null)
                {
                    return false;
                }

                string listName = null;
                var teamName = teamValue.ToString();
                if (string.Equals(teamName, "Red", StringComparison.OrdinalIgnoreCase)) listName = "RedPositions";
                else if (string.Equals(teamName, "Blue", StringComparison.OrdinalIgnoreCase)) listName = "BluePositions";
                if (string.IsNullOrEmpty(listName))
                {
                    return false;
                }

                var listProp = ppmType.GetProperty(listName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var positions = listProp != null ? listProp.GetValue(ppm) as IEnumerable : null;
                if (positions == null)
                {
                    return false;
                }

                var index = 0;
                foreach (var position in positions)
                {
                    var positionKey = $"{listName}:{index++}";
                    if (!string.Equals(positionKey, preferredPositionKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (position == null)
                    {
                        return false;
                    }

                    if (IsPositionClaimed(position))
                    {
                        return IsPositionOwnedByPlayer(position, player);
                    }

                    var claimMethod = position.GetType().GetMethod("Server_Claim", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (claimMethod == null)
                    {
                        return false;
                    }

                    claimMethod.Invoke(position, new[] { player });
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool IsPositionClaimed(object position)
        {
            if (position == null) return false;

            try
            {
                var posType = position.GetType();
                var claimedProp = posType.GetProperty("IsClaimed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (claimedProp == null)
                {
                    return false;
                }

                var claimedValue = claimedProp.GetValue(position);
                return claimedValue is bool claimed && claimed;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPositionOwnedByPlayer(object position, object playerObject)
        {
            if (position == null || playerObject == null) return false;

            try
            {
                var posType = position.GetType();
                object owner = null;

                var ownerProp = posType.GetProperty("Player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (ownerProp != null) owner = ownerProp.GetValue(position);

                if (owner == null)
                {
                    var ownerField = posType.GetField("player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                   ?? posType.GetField("Player", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (ownerField != null) owner = ownerField.GetValue(position);
                }

                if (owner == null) return true;
                if (ReferenceEquals(owner, playerObject)) return true;

                var ownerComponent = owner as Component;
                var playerComponent = playerObject as Component;
                if (ownerComponent != null && playerComponent != null)
                {
                    return ownerComponent == playerComponent;
                }
            }
            catch { }

            return false;
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
                    var resolvedKey = ResolvePlayerObjectKey(playerByClientId, clientId) ?? playerKey;
                    if (ShouldIgnoreTransientTeamHookPlayer(playerByClientId, clientId, resolvedKey))
                    {
                        return false;
                    }

                    player = playerByClientId;
                    resolvedClientId = clientId;
                    resolvedPlayerKey = resolvedKey;
                    return true;
                }

                var normalizedKey = ResolveStoredIdToSteam(playerKey ?? string.Empty);
                foreach (var candidate in GetAllPlayers())
                {
                    if (candidate == null) continue;
                    var candidateKey = ResolvePlayerObjectKey(candidate, 0UL);
                    var candidateStoredKey = TryGetPlayerId(candidate, 0UL);
                    ulong candidateClientId = 0;
                    TryGetClientId(candidate, out candidateClientId);
                    if (ShouldIgnoreTransientTeamHookPlayer(candidate, candidateClientId, candidateKey ?? candidateStoredKey))
                    {
                        continue;
                    }

                    if (!string.Equals(candidateKey, normalizedKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(candidateStoredKey, normalizedKey, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(candidateStoredKey, playerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    player = candidate;
                    resolvedClientId = candidateClientId;
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
                if (request.TargetCaptainClientId != 0 && TryGetPlayerByClientId(request.TargetCaptainClientId, out var targetedCaptainPlayer) && targetedCaptainPlayer != null)
                {
                    clientId = request.TargetCaptainClientId;
                    captainKey = !string.IsNullOrWhiteSpace(request.TargetCaptainKey)
                        ? request.TargetCaptainKey
                        : GetPlayerKey(targetedCaptainPlayer, request.TargetCaptainClientId);
                    request.TargetCaptainKey = captainKey;
                    return true;
                }

                if (TryGetCaptainClientIdForTeam(request.TargetTeam, out var currentCaptainClientId, out var currentCaptainKey, out _))
                {
                    if (!controlledTestModeEnabled && (IsDummyKey(currentCaptainKey) || BotManager.IsBotKey(currentCaptainKey)))
                    {
                        return false;
                    }

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

        private static TeamApprovalRequest GetPendingApprovalRequestForPlayerLocked(string playerKey, ulong clientId)
        {
            return pendingTeamApprovalRequests.Values
                .Where(request => request != null)
                .Where(request =>
                    (!string.IsNullOrWhiteSpace(playerKey)
                        && !string.IsNullOrWhiteSpace(request.PlayerId)
                        && string.Equals(request.PlayerId, playerKey, StringComparison.OrdinalIgnoreCase))
                    || (clientId != 0 && request.ClientId != 0 && request.ClientId == clientId))
                .OrderBy(request => request.Timestamp)
                .ThenBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static int CountApprovalRequestsForCaptainLocked(string captainKey, ulong clientId)
        {
            return pendingTeamApprovalRequests.Values.Count(request =>
                request != null
                && (((!string.IsNullOrWhiteSpace(captainKey)
                        && !string.IsNullOrWhiteSpace(request.TargetCaptainKey)
                        && string.Equals(request.TargetCaptainKey, captainKey, StringComparison.OrdinalIgnoreCase))
                    || (clientId != 0 && request.TargetCaptainClientId != 0 && request.TargetCaptainClientId == clientId))));
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

        private static ApprovalRequestNotification CloneApprovalRequestNotification(ApprovalRequestNotification notification)
        {
            if (notification == null) return null;
            return new ApprovalRequestNotification
            {
                PlayerId = notification.PlayerId,
                ClientId = notification.ClientId,
                RequestId = notification.RequestId,
                PlayerName = notification.PlayerName,
                TargetTeam = notification.TargetTeam,
                PreviousTeam = notification.PreviousTeam,
                Kind = notification.Kind,
                CaptainName = notification.CaptainName,
                Status = notification.Status,
                ExpiresAt = notification.ExpiresAt,
                CooldownEndsAt = notification.CooldownEndsAt
            };
        }

        private static ApprovalRequestNotification GetApprovalNotificationLocked(string playerKey, ulong clientId)
        {
            if (!string.IsNullOrWhiteSpace(playerKey)
                && approvalNotificationsByPlayerKey.TryGetValue(playerKey, out var byPlayerKey)
                && byPlayerKey != null)
            {
                return byPlayerKey;
            }

            return approvalNotificationsByPlayerKey.Values.FirstOrDefault(notification =>
                notification != null
                && clientId != 0
                && notification.ClientId != 0
                && notification.ClientId == clientId);
        }

        private static void RegisterApprovalNotification(TeamApprovalRequest request, ApprovalRequestDisplayStatus status, string captainName)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return;
            }

            lock (approvalRequestLock)
            {
                approvalNotificationsByPlayerKey[request.PlayerId] = new ApprovalRequestNotification
                {
                    PlayerId = request.PlayerId,
                    ClientId = request.ClientId,
                    RequestId = request.RequestId,
                    PlayerName = request.PlayerName,
                    TargetTeam = request.TargetTeam,
                    PreviousTeam = request.PreviousTeam,
                    Kind = request.Kind,
                    CaptainName = captainName,
                    Status = status,
                    ExpiresAt = Time.unscaledTime + ApprovalResultNotificationDurationSeconds,
                    CooldownEndsAt = 0f
                };
            }
        }

        private static void RegisterCooldownNotification(string playerKey, ulong clientId, string requestId, string playerName, TeamResult targetTeam, TeamResult previousTeam, TeamApprovalRequestKind kind, float cooldownSecondsRemaining)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            var now = Time.unscaledTime;
            lock (approvalRequestLock)
            {
                approvalNotificationsByPlayerKey[playerKey] = new ApprovalRequestNotification
                {
                    PlayerId = playerKey,
                    ClientId = clientId,
                    RequestId = requestId,
                    PlayerName = playerName,
                    TargetTeam = targetTeam,
                    PreviousTeam = previousTeam,
                    Kind = kind,
                    CaptainName = string.Empty,
                    Status = ApprovalRequestDisplayStatus.Cooldown,
                    ExpiresAt = now + ApprovalResultNotificationDurationSeconds,
                    CooldownEndsAt = now + Mathf.Max(0f, cooldownSecondsRemaining)
                };
            }
        }

        private static void ClearApprovalNotification(string playerKey)
        {
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return;
            }

            lock (approvalRequestLock)
            {
                approvalNotificationsByPlayerKey.Remove(playerKey);
            }
        }

        private static bool TryGetApprovalPlayerKeyForClient(ulong clientId, out string playerKey)
        {
            playerKey = null;

            try
            {
                if (clientId == 0 || !TryGetPlayerByClientId(clientId, out var player) || player == null)
                {
                    return false;
                }

                playerKey = GetPlayerKey(player, clientId);
                return !string.IsNullOrWhiteSpace(playerKey);
            }
            catch
            {
                playerKey = null;
                return false;
            }
        }

        private static float GetApprovalSecondsRemaining(TeamApprovalRequest request)
        {
            if (request == null)
            {
                return 0f;
            }

            return Mathf.Max(0f, ApprovalRequestTimeoutSeconds - (Time.unscaledTime - request.Timestamp));
        }

        private static bool TryGetRejectedRequestCooldownRemaining(string playerKey, out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (string.IsNullOrWhiteSpace(playerKey))
            {
                return false;
            }

            lock (approvalRequestLock)
            {
                TrimRejectedRequestCooldownsLocked(Time.unscaledTime);
                if (!rejectedRequestCooldownEndsAtByPlayerKey.TryGetValue(playerKey, out var cooldownEndsAt))
                {
                    return false;
                }

                remainingSeconds = Mathf.Max(0f, cooldownEndsAt - Time.unscaledTime);
                return remainingSeconds > 0f;
            }
        }

        private static void StartRejectedRequestCooldown(TeamApprovalRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.PlayerId))
            {
                return;
            }

            var cooldownEndsAt = Time.unscaledTime + ApprovalRejectCooldownSeconds;
            lock (approvalRequestLock)
            {
                rejectedRequestCooldownEndsAtByPlayerKey[request.PlayerId] = cooldownEndsAt;
            }

            Debug.Log($"[{Constants.MOD_NAME}] [JOIN][COOLDOWN] started requester={request.PlayerId} clientId={request.ClientId} requestId={request.RequestId} endsAt={cooldownEndsAt:0.00} duration={ApprovalRejectCooldownSeconds:0.0}s");
        }

        private static void TrimRejectedRequestCooldownsLocked(float now)
        {
            var expiredKeys = rejectedRequestCooldownEndsAtByPlayerKey
                .Where(entry => entry.Value <= now)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var expiredKey in expiredKeys)
            {
                rejectedRequestCooldownEndsAtByPlayerKey.Remove(expiredKey);
            }
        }

        private static ApprovalRequestStateMessage BuildCaptainApprovalState(TeamApprovalRequest request, int queueLength)
        {
            var targetTeamText = FormatTeamLabel(request.TargetTeam);
            var previousTeamText = request.PreviousTeam == TeamResult.Red || request.PreviousTeam == TeamResult.Blue
                ? FormatTeamLabel(request.PreviousTeam)
                : string.Empty;
            var secondsRemaining = GetApprovalSecondsRemaining(request);
            var promptText = request.Kind == TeamApprovalRequestKind.TeamSwitch
                ? $"{request.PlayerName} wants to switch from {previousTeamText} to {targetTeamText}."
                : $"{request.PlayerName} wants to join {targetTeamText}.";
            var footerText = queueLength > 1
                ? $"Oldest request shown first. {queueLength - 1} more request(s) are waiting behind this one."
                : "Oldest request shown first. Approve opens position select; reject leaves the player's state unchanged.";

            return new ApprovalRequestStateMessage
            {
                IsVisible = true,
                RequestId = request.RequestId,
                ViewRole = ApprovalRequestViewRole.CaptainDecision,
                Status = ApprovalRequestDisplayStatus.Pending,
                Title = "TEAM APPROVAL REQUIRED",
                PlayerName = request.PlayerName,
                PromptText = promptText,
                TargetTeamName = targetTeamText,
                PreviousTeamName = previousTeamText,
                IsSwitchRequest = request.Kind == TeamApprovalRequestKind.TeamSwitch,
                FooterText = footerText,
                SecondsRemaining = secondsRemaining,
                QueuePosition = queueLength > 0 ? 1 : 0,
                QueueLength = Mathf.Max(0, queueLength)
            };
        }

        private static ApprovalRequestStateMessage BuildRequesterPendingState(TeamApprovalRequest request)
        {
            var targetTeamText = FormatTeamLabel(request.TargetTeam);
            var previousTeamText = request.PreviousTeam == TeamResult.Red || request.PreviousTeam == TeamResult.Blue
                ? FormatTeamLabel(request.PreviousTeam)
                : string.Empty;

            return new ApprovalRequestStateMessage
            {
                IsVisible = true,
                RequestId = request.RequestId,
                ViewRole = ApprovalRequestViewRole.RequesterStatus,
                Status = ApprovalRequestDisplayStatus.Pending,
                Title = request.Kind == TeamApprovalRequestKind.TeamSwitch ? "SWITCH REQUEST SENT" : "JOIN REQUEST SENT",
                PlayerName = request.PlayerName,
                PromptText = request.Kind == TeamApprovalRequestKind.TeamSwitch
                    ? $"Waiting for the {targetTeamText} captain to review your switch from {previousTeamText}."
                    : $"Waiting for the {targetTeamText} captain to review your join request.",
                TargetTeamName = targetTeamText,
                PreviousTeamName = previousTeamText,
                IsSwitchRequest = request.Kind == TeamApprovalRequestKind.TeamSwitch,
                FooterText = "You can keep playing while this request is pending.",
                SecondsRemaining = GetApprovalSecondsRemaining(request),
                QueuePosition = 0,
                QueueLength = 0
            };
        }

        private static ApprovalRequestStateMessage BuildRequesterNotificationState(ApprovalRequestNotification notification)
        {
            var targetTeamText = FormatTeamLabel(notification.TargetTeam);
            var previousTeamText = notification.PreviousTeam == TeamResult.Red || notification.PreviousTeam == TeamResult.Blue
                ? FormatTeamLabel(notification.PreviousTeam)
                : string.Empty;
            var captainName = string.IsNullOrWhiteSpace(notification.CaptainName) ? "Captain" : notification.CaptainName;
            string title;
            string promptText;

            switch (notification.Status)
            {
                case ApprovalRequestDisplayStatus.Approved:
                    title = notification.Kind == TeamApprovalRequestKind.TeamSwitch ? "SWITCH APPROVED" : "JOIN APPROVED";
                    promptText = notification.Kind == TeamApprovalRequestKind.TeamSwitch
                        ? $"{captainName} approved your switch to {targetTeamText}."
                        : $"{captainName} approved your request to join {targetTeamText}.";
                    break;
                case ApprovalRequestDisplayStatus.Rejected:
                    title = notification.Kind == TeamApprovalRequestKind.TeamSwitch ? "SWITCH DENIED" : "JOIN DENIED";
                    promptText = notification.Kind == TeamApprovalRequestKind.TeamSwitch
                        ? $"{captainName} rejected your switch request. You remain on {previousTeamText}."
                        : $"{captainName} rejected your request to join {targetTeamText}.";
                    break;
                case ApprovalRequestDisplayStatus.Cooldown:
                    title = "REQUEST COOLDOWN";
                    promptText = $"Please wait {Mathf.Max(0f, notification.CooldownEndsAt - Time.unscaledTime):0.0}s before sending another team request.";
                    break;
                case ApprovalRequestDisplayStatus.Expired:
                    title = "REQUEST EXPIRED";
                    promptText = $"Your request for {targetTeamText} expired before the captain responded.";
                    break;
                default:
                    title = "REQUEST CANCELLED";
                    promptText = $"Your request for {targetTeamText} was cancelled before it could be completed.";
                    break;
            }

            return new ApprovalRequestStateMessage
            {
                IsVisible = true,
                RequestId = notification.RequestId,
                ViewRole = ApprovalRequestViewRole.RequesterStatus,
                Status = notification.Status,
                Title = title,
                PlayerName = notification.PlayerName,
                PromptText = promptText,
                TargetTeamName = targetTeamText,
                PreviousTeamName = previousTeamText,
                IsSwitchRequest = notification.Kind == TeamApprovalRequestKind.TeamSwitch,
                FooterText = "This popup closes automatically.",
                SecondsRemaining = notification.Status == ApprovalRequestDisplayStatus.Cooldown
                    ? Mathf.Max(0f, notification.CooldownEndsAt - Time.unscaledTime)
                    : Mathf.Max(0f, notification.ExpiresAt - Time.unscaledTime),
                QueuePosition = 0,
                QueueLength = 0
            };
        }

        private static void ResolveExpiredRequest(TeamApprovalRequest request)
        {
            if (request == null)
            {
                return;
            }

            RegisterApprovalNotification(request, ApprovalRequestDisplayStatus.Expired, GetCaptainDisplayNameByKey(request.TargetCaptainKey) ?? "Captain");

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
                var requesterMessage = request.Kind == TeamApprovalRequestKind.TeamSwitch
                    ? $"<size=13><color=#ffcc66>Ranked</color> your team change to {FormatTeamLabel(request.TargetTeam)} expired before anyone answered.</size>"
                    : $"<size=13><color=#ffcc66>Ranked</color> your request to join {FormatTeamLabel(request.TargetTeam)} expired before anyone answered.</size>";
                SendSystemChatToClient(requesterMessage, request.ClientId);
            }

            var captainMessage = request.Kind == TeamApprovalRequestKind.TeamSwitch
                ? $"<size=13><color=#ffcc66>Ranked</color> <b>{request.PlayerName}</b>'s team change request expired.</size>"
                : $"<size=13><color=#ffcc66>Ranked</color> <b>{request.PlayerName}</b>'s join request expired.</size>";
            SendCaptainChatForRequest(request.TargetTeam, captainMessage);
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
                foreach (var player in GetAllPlayers())
                {
                    if (!TryGetClientId(player, out var clientId) || clientId == 0)
                    {
                        continue;
                    }

                    RankedOverlayNetwork.PublishApprovalRequestStateToClient(clientId, GetApprovalRequestStateForClient(clientId));
                }
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