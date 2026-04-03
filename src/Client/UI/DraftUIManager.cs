using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace schrader
{
    public static class DraftUIManager
    {
        private enum OverlayUiState
        {
            None,
            Welcome,
            TeamSelect,
            Draft,
            PostMatch
        }

        private static readonly FieldInfo uiHudField = typeof(UIHUDController)
            .GetField("uiHud", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo uiHudContainerField = typeof(UIHUD)
            .GetField("container", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo uiManagerShowMouseMethod = typeof(UIManager)
            .GetMethod("ShowMouse", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo uiManagerHideMouseMethod = typeof(UIManager)
            .GetMethod("HideMouse", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo uiManagerUpdateMouseVisibilityMethod = typeof(UIManager)
            .GetMethod("UpdateMouseVisibility", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo uiChatSendMessageMethod = typeof(UIChat)
            .GetMethod("Client_SendClientChatMessage", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(bool) }, null);

        private static readonly MethodInfo playerInputResetInputsMethod = typeof(PlayerInput)
            .GetMethod("ResetInputs", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(bool) }, null);

        private static readonly MethodInfo playerSetStateRpcMethod = typeof(Player)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(method => string.Equals(method.Name, "Client_SetPlayerStateRpc", StringComparison.Ordinal)
                && method.GetParameters().Length == 2);

        private static readonly MethodInfo scoreboardStartedMethod = typeof(UIManagerInputs)
            .GetMethod("OnScoreboardActionStarted", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo scoreboardCanceledMethod = typeof(UIManagerInputs)
            .GetMethod("OnScoreboardActionCanceled", BindingFlags.Instance | BindingFlags.NonPublic);

        private static DraftUIRenderer.View view;
        private static bool isSetup;
        private static bool handlersRegistered;
        private static bool disconnectListenerRegistered;
        private static bool suppressVoteUntilHidden;
        private static bool localVoteAccepted;
        private static bool localVoteRejected;
        private static float voteStateReceivedAt;
        private static string lastVoteRenderSignature;
        private static string lastApprovalRenderSignature;
        private static string lastDraftRenderSignature;
        private static string lastPostMatchRenderSignature;
        private static string lastOverlayTickSignature;
        private static OverlayUiState currentUiState = OverlayUiState.None;
        private static CustomMessagingManager currentMessagingManager;
        private static VoteOverlayStateMessage currentVoteState = VoteOverlayStateMessage.Hidden();
        private static ApprovalRequestStateMessage currentApprovalRequestState = ApprovalRequestStateMessage.Hidden();
        private static DraftOverlayStateMessage currentDraftState = DraftOverlayStateMessage.Hidden();
        private static DraftOverlayExtendedMessage currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
        private static MatchResultMessage currentMatchResultState = MatchResultMessage.Hidden();
        private static MatchResultMessage cachedMatchResultState = MatchResultMessage.Hidden();
        private static bool postMatchDismissed;
        private static bool welcomePendingAcknowledgement = true;
        private static float currentStateEnteredAt = -1f;
        private static float welcomeRequestedAt = -1f;
        private static float lastVisibleMatchResultReceivedAt = -1f;
        private static bool hasLoggedWelcomeFallback;
        private static bool hasLoggedPostMatchFallback;
        private static float currentUiOpacity;
        private static float targetUiOpacity;
        private static bool pendingHideAfterFade;
        private const float UiFadeSpeed = 7.5f;
        private const float WelcomeVisibilityFailsafeDelay = 1.5f;
        private const float PostMatchVisibilityFailsafeDelay = 2f;
        private const float LateJoinHandoffRetryInterval = 0.35f;
        private const float ManualSwitchTeamHandoffSuppressDuration = 2f;
        private static float lastLateJoinHandoffAttemptAt = -1f;
        private static float suppressAssignedTeamHandoffUntil = -1f;
        private static bool lastObservedTeamSelectVisible;
        private static bool lastObservedPositionSelectVisible;
        private static string lastObservedLocalSelectionState = string.Empty;

        public static bool ValidateRuntimeMethods()
        {
            var hudHook = typeof(UIHUDController).GetMethod(
                "Event_OnPlayerBodySpawned",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Dictionary<string, object>) },
                null);

            var updateHook = typeof(SynchronizedObjectManager).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            var uiManagerStartHook = typeof(UIManager).GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            var playerPostSpawnHook = typeof(Player).GetMethod(
                "OnNetworkPostSpawn",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            var registerHandler = typeof(CustomMessagingManager).GetMethod(
                "RegisterNamedMessageHandler",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(CustomMessagingManager.HandleNamedMessageDelegate) },
                null);

            var unregisterHandler = typeof(CustomMessagingManager).GetMethod(
                "UnregisterNamedMessageHandler",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);

            var isValid = true;

            isValid &= LogMissingMember(hudHook, "UIHUDController.Event_OnPlayerBodySpawned(Dictionary<string, object>)");
            isValid &= LogMissingMember(updateHook, "SynchronizedObjectManager.Update()");
            isValid &= LogMissingMember(uiManagerStartHook, "UIManager.Start()");
            isValid &= LogMissingMember(playerPostSpawnHook, "Player.OnNetworkPostSpawn()");
            isValid &= LogMissingMember(uiHudField, "UIHUDController.uiHud");
            isValid &= LogMissingMember(uiHudContainerField, "UIHUD.container");
            isValid &= LogMissingMember(uiManagerShowMouseMethod, "UIManager.ShowMouse()");
            isValid &= LogMissingMember(uiManagerHideMouseMethod, "UIManager.HideMouse()");
            isValid &= LogMissingMember(uiManagerUpdateMouseVisibilityMethod, "UIManager.UpdateMouseVisibility()");
            isValid &= LogMissingMember(uiChatSendMessageMethod, "UIChat.Client_SendClientChatMessage(string, bool)");
            isValid &= LogMissingMember(playerInputResetInputsMethod, "PlayerInput.ResetInputs(bool)");
            isValid &= LogMissingMember(scoreboardStartedMethod, "UIManagerInputs.OnScoreboardActionStarted(InputAction.CallbackContext)");
            isValid &= LogMissingMember(scoreboardCanceledMethod, "UIManagerInputs.OnScoreboardActionCanceled(InputAction.CallbackContext)");
            isValid &= LogMissingMember(registerHandler, "CustomMessagingManager.RegisterNamedMessageHandler(string, handler)");
            isValid &= LogMissingMember(unregisterHandler, "CustomMessagingManager.UnregisterNamedMessageHandler(string)");

            if (isValid)
            {
                DraftUIPlugin.Log("Runtime method validation passed");
            }

            return isValid;
        }

        private static bool LogMissingMember(MemberInfo memberInfo, string label)
        {
            if (memberInfo != null)
            {
                return true;
            }

            DraftUIPlugin.LogError($"Missing runtime member: {label}");
            return false;
        }

        private static void Setup(UIHUDController uiHudController)
        {
            if (uiHudController == null || uiHudField == null || uiHudContainerField == null)
            {
                return;
            }

            var uiHud = uiHudField.GetValue(uiHudController) as UIHUD;
            var uiHudContainer = uiHud != null ? uiHudContainerField.GetValue(uiHud) as VisualElement : null;

            Setup(uiHud, uiHudContainer, "UIHUDController.Event_OnPlayerBodySpawned");
        }

        private static bool Setup(UIHUD uiHud, VisualElement uiHudContainer, string sourceLabel)
        {
            if (uiHud == null || uiHudContainer == null || uiHudContainer.parent == null)
            {
                return false;
            }

            if (isSetup && view?.Container != null && view.Container.parent != null)
            {
                return true;
            }

            if (view?.Container != null)
            {
                view.Container.RemoveFromHierarchy();
            }

            DraftUIRenderer.CreateUI(
                uiHud,
                uiHudContainer,
                out view,
                OnVoteAcceptedClicked,
                OnVoteRejectedClicked,
                OnApprovalAcceptedClicked,
                OnApprovalRejectedClicked,
                OnPickPlayerClicked,
                OnAcceptLateJoinerClicked,
                OnWelcomeDiscordClicked,
                OnWelcomeContinueClicked,
                OnPostMatchContinueClicked,
                OnPostMatchCloseClicked);

            isSetup = view?.Container != null && view.Container.parent != null;
            if (!isSetup)
            {
                return false;
            }

            RegisterDisconnectListener();

            DraftUIPlugin.Log($"UI attached via {sourceLabel}");
            RefreshUi(forceRefresh: true);
            return true;
        }

        private static void EnsureViewSetup()
        {
            if (isSetup && view?.Container != null && view.Container.parent != null)
            {
                return;
            }

            TrySetupFromUiManager();
        }

        private static bool TrySetupFromUiManager()
        {
            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager || uiHudContainerField == null)
                {
                    return false;
                }

                var uiHud = uiManager.Hud;
                if (!uiHud)
                {
                    return false;
                }

                var uiHudContainer = uiHudContainerField.GetValue(uiHud) as VisualElement;
                return Setup(uiHud, uiHudContainer, "UIManager.Start");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to set up overlay from UIManager: {ex}");
                return false;
            }
        }

        private static void HandleLocalPlayerInitialized(Player player)
        {
            if (player == null || !player.IsLocalPlayer)
            {
                return;
            }

            DraftUIPlugin.Log("Local player initialized before team selection; arming welcome flow.");
            EnsureViewSetup();
            RefreshUi(forceRefresh: true);
            EnforceGameplayUiPriority();
        }

        private static void RegisterDisconnectListener()
        {
            if (disconnectListenerRegistered)
            {
                return;
            }

            var eventManager = EventManager.Instance;
            if (eventManager == null)
            {
                return;
            }

            eventManager.AddEventListener("Event_OnClientDisconnected", new Action<Dictionary<string, object>>(OnClientDisconnected));
            disconnectListenerRegistered = true;
        }

        private static void OnClientDisconnected(Dictionary<string, object> evt)
        {
            currentVoteState = VoteOverlayStateMessage.Hidden();
            currentApprovalRequestState = ApprovalRequestStateMessage.Hidden();
            currentDraftState = DraftOverlayStateMessage.Hidden();
            currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
            currentMatchResultState = MatchResultMessage.Hidden();
            cachedMatchResultState = MatchResultMessage.Hidden();
            suppressVoteUntilHidden = false;
            localVoteAccepted = false;
            localVoteRejected = false;
            lastVoteRenderSignature = null;
            lastApprovalRenderSignature = null;
            lastDraftRenderSignature = null;
            lastPostMatchRenderSignature = null;
            postMatchDismissed = false;
            welcomePendingAcknowledgement = true;
            currentStateEnteredAt = -1f;
            welcomeRequestedAt = -1f;
            lastVisibleMatchResultReceivedAt = -1f;
            hasLoggedWelcomeFallback = false;
            hasLoggedPostMatchFallback = false;
            UIInputState.Reset();
            ApplyUiState(OverlayUiState.None, forceRefresh: true);
        }

        private static void EnsureMessagingHandlers()
        {
            try
            {
                var networkManager = NetworkManager.Singleton;
                var messagingManager = networkManager != null && networkManager.IsClient
                    ? networkManager.CustomMessagingManager
                    : null;

                if (ReferenceEquals(messagingManager, currentMessagingManager) && handlersRegistered)
                {
                    return;
                }

                UnregisterHandlers();

                if (messagingManager == null)
                {
                    return;
                }

                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.VoteState, OnVoteStateReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.ApprovalRequestState, OnApprovalRequestStateReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.DraftState, OnDraftStateReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.DraftStateExtended, OnDraftExtendedStateReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.MatchResult, OnMatchResultReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.DiscordInviteOpen, OnDiscordInviteOpenReceived);
                currentMessagingManager = messagingManager;
                handlersRegistered = true;
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to register overlay handlers: {ex}");
            }
        }

        private static void UnregisterHandlers()
        {
            if (currentMessagingManager != null && handlersRegistered)
            {
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.VoteState); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.ApprovalRequestState); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.DraftState); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.DraftStateExtended); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.MatchResult); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.DiscordInviteOpen); } catch { }
            }

            currentMessagingManager = null;
            handlersRegistered = false;
        }

        private static void OnVoteStateReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<VoteOverlayStateMessage>(ref reader) ?? VoteOverlayStateMessage.Hidden();
                var preservePostMatch = incomingState.IsVisible && IsPostMatchStateLocked();
                currentVoteState = preservePostMatch ? VoteOverlayStateMessage.Hidden() : incomingState;
                voteStateReceivedAt = Time.unscaledTime;

                if (!currentVoteState.IsVisible)
                {
                    suppressVoteUntilHidden = false;
                    localVoteAccepted = false;
                    localVoteRejected = false;
                }
                else
                {
                    SyncLocalVoteSelectionFromState(currentVoteState);
                    currentMatchResultState = MatchResultMessage.Hidden();
                    postMatchDismissed = false;
                }

                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("Ignoring vote overlay while post-match results are visible.");
                }

                RefreshUi(forceRefresh: true);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive vote overlay state: {ex}");
            }
        }

        private static void OnDraftStateReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<DraftOverlayStateMessage>(ref reader) ?? DraftOverlayStateMessage.Hidden();
                var preservePostMatch = incomingState.IsVisible && IsPostMatchStateLocked();
                DraftUIPlugin.Log($"[CLIENT] DraftState received. Visible={incomingState.IsVisible} Available={(incomingState.AvailablePlayers?.Length ?? 0)} Red={(incomingState.RedPlayers?.Length ?? 0)} Blue={(incomingState.BluePlayers?.Length ?? 0)} Pending={(incomingState.PendingLateJoiners?.Length ?? 0)} CurrentUi={currentUiState}");
                currentDraftState = preservePostMatch ? DraftOverlayStateMessage.Hidden() : incomingState;
                if (currentDraftState == null || !currentDraftState.IsVisible)
                {
                    DraftUIPlugin.Log($"[CLIENT] Draft overlay hidden because payload empty/inactive.");
                    if (!preservePostMatch)
                    {
                        currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
                    }
                }
                else
                {
                    DraftUIPlugin.Log($"[CLIENT] Draft overlay state activated from DraftState. PreviousUi={currentUiState}");
                    welcomePendingAcknowledgement = false;
                    welcomeRequestedAt = -1f;
                    hasLoggedWelcomeFallback = false;
                }

                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("[CLIENT] POST_MATCH blocked/overridden by state DraftState.");
                }

                DraftUIPlugin.Log($"[CLIENT] Draft overlay render requested from DraftState. CurrentUi={currentUiState}");
                RefreshUi(forceRefresh: true);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive draft overlay state: {ex}");
            }
        }

        private static void OnDraftExtendedStateReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<DraftOverlayExtendedMessage>(ref reader) ?? DraftOverlayExtendedMessage.Hidden();
                var preservePostMatch = incomingState.IsVisible && IsPostMatchStateLocked();
                DraftUIPlugin.Log($"[CLIENT] DraftStateExtended received. Visible={incomingState.IsVisible} Available={(incomingState.AvailablePlayerEntries?.Length ?? 0)} Red={(incomingState.RedPlayerEntries?.Length ?? 0)} Blue={(incomingState.BluePlayerEntries?.Length ?? 0)} Pending={(incomingState.PendingLateJoinerEntries?.Length ?? 0)} CurrentUi={currentUiState}");
                currentDraftExtendedState = preservePostMatch ? DraftOverlayExtendedMessage.Hidden() : incomingState;
                if (currentDraftExtendedState != null && currentDraftExtendedState.IsVisible)
                {
                    welcomePendingAcknowledgement = false;
                    welcomeRequestedAt = -1f;
                    hasLoggedWelcomeFallback = false;

                    if (currentDraftState == null || !currentDraftState.IsVisible)
                    {
                        currentDraftState = CreateFallbackDraftStateFromExtended();
                        DraftUIPlugin.Log($"[CLIENT] Draft overlay activated from extended payload fallback. PreviousUi={currentUiState}");
                    }
                }
                else
                {
                    DraftUIPlugin.Log($"[CLIENT] Draft overlay hidden because extended payload empty/inactive.");
                }

                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("[CLIENT] POST_MATCH blocked/overridden by state DraftStateExtended.");
                }

                DraftUIPlugin.Log($"[CLIENT] Draft overlay render requested from DraftStateExtended. CurrentUi={currentUiState}");
                RefreshUi(forceRefresh: true);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive draft extended state: {ex}");
            }
        }

        private static void OnApprovalRequestStateReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<ApprovalRequestStateMessage>(ref reader) ?? ApprovalRequestStateMessage.Hidden();
                var preservePostMatch = incomingState.IsVisible && IsPostMatchStateLocked();
                currentApprovalRequestState = preservePostMatch ? ApprovalRequestStateMessage.Hidden() : incomingState;
                if (currentApprovalRequestState != null && currentApprovalRequestState.IsVisible)
                {
                    currentMatchResultState = MatchResultMessage.Hidden();
                    postMatchDismissed = false;
                }

                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("Ignoring approval overlay while post-match results are visible.");
                }

                RefreshUi(forceRefresh: true);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive approval request state: {ex}");
            }
        }

        private static void OnMatchResultReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var incomingState = RankedOverlayNetcode.ReadJson<MatchResultMessage>(ref reader) ?? MatchResultMessage.Hidden();
                currentMatchResultState = incomingState;

                if (incomingState.IsVisible)
                {
                    cachedMatchResultState = incomingState;
                    lastVisibleMatchResultReceivedAt = Time.unscaledTime;
                    hasLoggedPostMatchFallback = false;
                    postMatchDismissed = false;
                    welcomePendingAcknowledgement = false;
                    welcomeRequestedAt = -1f;
                    hasLoggedWelcomeFallback = false;
                }
                else
                {
                    cachedMatchResultState = MatchResultMessage.Hidden();
                    lastVisibleMatchResultReceivedAt = -1f;
                    hasLoggedPostMatchFallback = false;
                    postMatchDismissed = false;
                }

                currentVoteState = VoteOverlayStateMessage.Hidden();
                currentApprovalRequestState = ApprovalRequestStateMessage.Hidden();
                currentDraftState = DraftOverlayStateMessage.Hidden();
                currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
                suppressVoteUntilHidden = false;
                localVoteAccepted = false;
                localVoteRejected = false;

                DraftUIPlugin.Log($"[CLIENT] Post-match payload received. Visible={currentMatchResultState.IsVisible} Winner={currentMatchResultState.WinningTeam} Players={(currentMatchResultState.Players?.Length ?? 0)}");
                if (currentMatchResultState.IsVisible)
                {
                    EnsureViewSetup();
                    ApplyUiState(OverlayUiState.PostMatch, forceRefresh: true);
                }

                RefreshUi(forceRefresh: true);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive match result state: {ex}");
            }
        }

        private static void OnDiscordInviteOpenReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var message = RankedOverlayNetcode.ReadJson<OpenDiscordInviteMessage>(ref reader) ?? new OpenDiscordInviteMessage();
                OpenDiscordInvite(message.Url, "Server command");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive Discord invite request: {ex}");
            }
        }

        private static void UpdateOverlay()
        {
            EnsureViewSetup();
            EnsureMessagingHandlers();
            SyncInputState();
            RefreshUi(forceRefresh: false);
            TryRecoverApprovedLateJoinHandoff();
            TrackLocalSelectionUiState();
            UpdateStateFailsafes();
            EnforceGameplayUiPriority();
            FreezeLocalGameplayInputIfBlocked();
            EnsureCursorConsistency();
            TickOverlayTransition();
        }

        private static void RefreshUi(bool forceRefresh)
        {
            if (view == null)
            {
                return;
            }

            var nextState = ResolveTargetUiState();
            LogOverlayTick(nextState);
            ApplyUiState(nextState, forceRefresh);

            if (nextState == OverlayUiState.Draft)
            {
                DraftUIPlugin.Log($"[CLIENT] Draft overlay visible. Render path requested.");
                RenderDraftFlow(forceRefresh);
                return;
            }

            if (nextState == OverlayUiState.PostMatch)
            {
                RenderPostMatch(forceRefresh);
                return;
            }
        }

        private static void ApplyUiState(OverlayUiState nextState, bool forceRefresh)
        {
            var previousState = currentUiState;
            if (previousState != nextState)
            {
                currentUiState = nextState;
                currentStateEnteredAt = Time.unscaledTime;
                DraftUIPlugin.Log($"UI state transition: {previousState} -> {nextState}");
                if (nextState == OverlayUiState.PostMatch)
                {
                    DraftUIPlugin.Log("POST_MATCH state activated");
                }
            }
            else
            {
                currentUiState = nextState;
            }

            UIInputState.isDraftUIOpen = ShouldOwnCursor(nextState);

            if (previousState != nextState || forceRefresh)
            {
                switch (nextState)
                {
                    case OverlayUiState.None:
                        ReleaseCursor();
                        targetUiOpacity = 0f;
                        pendingHideAfterFade = true;
                        if (forceRefresh && previousState == OverlayUiState.None)
                        {
                            currentUiOpacity = 0f;
                            targetUiOpacity = 0f;
                            pendingHideAfterFade = false;
                            DraftUIRenderer.ShowHidden(view);
                        }
                        break;
                    case OverlayUiState.Welcome:
                        WelcomeUIRenderer.Show(view);
                        PrepareVisibleTransition(previousState == OverlayUiState.None);
                        view?.Container?.BringToFront();
                        AcquireCursor(forceResetInputs: true);
                        EnforceGameplayUiPriority();
                        break;
                    case OverlayUiState.TeamSelect:
                        ReleaseCursor();
                        targetUiOpacity = 0f;
                        pendingHideAfterFade = true;
                        RestoreSuppressedGameplayUi();
                        break;
                    case OverlayUiState.Draft:
                        ShowDraftFlow();
                        PrepareVisibleTransition(previousState == OverlayUiState.None || previousState == OverlayUiState.TeamSelect);
                        AcquireCursor(forceResetInputs: true);
                        break;
                    case OverlayUiState.PostMatch:
                        PostMatchUIRenderer.Show(view);
                        PrepareVisibleTransition(previousState == OverlayUiState.None || previousState == OverlayUiState.TeamSelect);
                        view?.Container?.BringToFront();
                        AcquireCursor(forceResetInputs: true);
                        EnforceGameplayUiPriority();
                        DraftUIPlugin.Log("[CLIENT] Entering POST_MATCH.");
                        break;
                }
            }
        }

        private static OverlayUiState ResolveTargetUiState()
        {
            if (ShouldShowPostMatchUi() || (currentUiState == OverlayUiState.PostMatch && HasCachedPostMatchResult()))
            {
                welcomeRequestedAt = -1f;
                hasLoggedWelcomeFallback = false;
                return OverlayUiState.PostMatch;
            }

            if (HasActiveDraftPayload())
            {
                return OverlayUiState.Draft;
            }

            if (ShouldAwaitWelcomeFlow())
            {
                if (welcomeRequestedAt < 0f)
                {
                    welcomeRequestedAt = Time.unscaledTime;
                    hasLoggedWelcomeFallback = false;
                    DraftUIPlugin.Log("Welcome flow armed for the local player.");
                }

                return view?.Welcome?.Panel != null
                    ? OverlayUiState.Welcome
                    : OverlayUiState.None;
            }

            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;

            if (ShouldShowDraftUi())
            {
                return OverlayUiState.Draft;
            }

            if (ShouldShowTeamSelectState())
            {
                return OverlayUiState.TeamSelect;
            }

            return OverlayUiState.None;
        }

        private static bool ShouldOwnCursor(OverlayUiState state)
        {
            return state == OverlayUiState.Welcome
                || state == OverlayUiState.Draft
                || state == OverlayUiState.PostMatch;
        }

        private static bool ShouldShowDraftUi()
        {
            return HasActiveDraftPayload()
                || (currentApprovalRequestState != null && currentApprovalRequestState.IsVisible)
                || (currentVoteState != null && currentVoteState.IsVisible && !suppressVoteUntilHidden);
        }

        private static bool HasActiveDraftPayload()
        {
            return (currentDraftState != null && currentDraftState.IsVisible)
                || (currentDraftExtendedState != null && currentDraftExtendedState.IsVisible);
        }

        private static bool ShouldShowPostMatchUi()
        {
            return currentMatchResultState != null && currentMatchResultState.IsVisible && !postMatchDismissed;
        }

        private static bool HasCachedPostMatchResult()
        {
            return cachedMatchResultState != null && cachedMatchResultState.IsVisible && !postMatchDismissed;
        }

        private static bool IsPostMatchStateLocked()
        {
            return !postMatchDismissed
                && (ShouldShowPostMatchUi()
                    || HasCachedPostMatchResult()
                    || currentUiState == OverlayUiState.PostMatch);
        }

        private static bool ShouldShowTeamSelectState()
        {
            var stateName = GetLocalPlayerStateName();
            return ShouldBlockGameplayState(stateName);
        }

        private static void ShowDraftFlow()
        {
            if (HasActiveDraftPayload())
            {
                DraftUIRenderer.ShowDraft(view);
                return;
            }

            if (currentApprovalRequestState != null && currentApprovalRequestState.IsVisible)
            {
                DraftUIRenderer.ShowApproval(view);
                return;
            }

            DraftUIRenderer.ShowVoting(view);
        }

        private static void RenderDraftFlow(bool forceRefresh)
        {
            if (HasActiveDraftPayload())
            {
                RenderDraft(forceRefresh);
                return;
            }

            if (currentApprovalRequestState != null && currentApprovalRequestState.IsVisible)
            {
                RenderApproval(forceRefresh);
                return;
            }

            RenderVoting(forceRefresh);
        }

        private static void PrepareVisibleTransition(bool fromHidden)
        {
            if (view?.Container == null)
            {
                return;
            }

            pendingHideAfterFade = false;
            targetUiOpacity = 1f;

            if (fromHidden || view.Container.style.display == DisplayStyle.None)
            {
                currentUiOpacity = 0f;
                view.Container.style.opacity = 0f;
            }
        }

        private static void TickOverlayTransition()
        {
            if (view?.Container == null)
            {
                return;
            }

            currentUiOpacity = Mathf.MoveTowards(currentUiOpacity, targetUiOpacity, Time.unscaledDeltaTime * UiFadeSpeed);
            view.Container.style.opacity = currentUiOpacity;

            if (pendingHideAfterFade && currentUiOpacity <= 0.001f)
            {
                pendingHideAfterFade = false;
                DraftUIRenderer.ShowHidden(view);
            }
        }

        private static void RenderVoting(bool forceRefresh)
        {
            var secondsRemainingPrecise = Mathf.Max(0f, currentVoteState.SecondsRemainingPrecise - (Time.unscaledTime - voteStateReceivedAt));
            var signature = string.Join("|",
                currentVoteState.Title ?? string.Empty,
                currentVoteState.PromptText ?? string.Empty,
                currentVoteState.InitiatorName ?? string.Empty,
                currentVoteState.YesVotes,
                currentVoteState.NoVotes,
                currentVoteState.RequiredYesVotes,
                currentVoteState.EligibleCount,
                currentVoteState.VoteDurationSeconds,
                string.Join(",", (currentVoteState.PlayerEntries ?? Array.Empty<VoteOverlayPlayerEntryMessage>()).Select(BuildVoteEntrySignature)),
                currentVoteState.FooterText ?? string.Empty,
                localVoteAccepted,
                localVoteRejected);
            var contentChanged = forceRefresh || !string.Equals(signature, lastVoteRenderSignature, StringComparison.Ordinal);

            if (contentChanged)
            {
                lastVoteRenderSignature = signature;
            }

            DraftUIRenderer.RenderVoting(view, currentVoteState, secondsRemainingPrecise, localVoteAccepted, localVoteRejected, contentChanged);
        }

        private static void RenderApproval(bool forceRefresh)
        {
            var signature = string.Join("|",
                currentApprovalRequestState.RequestId ?? string.Empty,
                currentApprovalRequestState.Title ?? string.Empty,
                currentApprovalRequestState.PlayerName ?? string.Empty,
                currentApprovalRequestState.PromptText ?? string.Empty,
                currentApprovalRequestState.TargetTeamName ?? string.Empty,
                currentApprovalRequestState.PreviousTeamName ?? string.Empty,
                currentApprovalRequestState.IsSwitchRequest,
                currentApprovalRequestState.FooterText ?? string.Empty);

            if (!forceRefresh && string.Equals(signature, lastApprovalRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastApprovalRenderSignature = signature;
            DraftUIRenderer.RenderApproval(view, currentApprovalRequestState);
        }

        private static void RenderDraft(bool forceRefresh)
        {
            var renderState = GetRenderableDraftState();
            ResolveDraftTurnContext(renderState, currentDraftExtendedState, out var isLocalTurn, out var currentTurnTeam);
            var signature = string.Join("|",
                renderState.Title ?? string.Empty,
                renderState.RedCaptainName ?? string.Empty,
                renderState.BlueCaptainName ?? string.Empty,
                renderState.CurrentTurnName ?? string.Empty,
                renderState.CurrentTurnClientId,
                renderState.CurrentTurnSteamId ?? string.Empty,
                renderState.IsCompleted,
                renderState.FooterText ?? string.Empty,
                renderState.PendingLateJoinerCount,
                string.Join(",", renderState.AvailablePlayers ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.AvailablePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", renderState.RedPlayers ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.RedPlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", renderState.BluePlayers ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.BluePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", renderState.PendingLateJoiners ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.PendingLateJoinerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)));

            if (!forceRefresh && string.Equals(signature, lastDraftRenderSignature, StringComparison.Ordinal))
            {
                DraftUIRenderer.UpdateDraftAmbientMotion(view, isLocalTurn, currentTurnTeam);
                return;
            }

            lastDraftRenderSignature = signature;
            DraftUIPlugin.Log($"[CLIENT] Draft overlay render requested. BasicVisible={currentDraftState?.IsVisible ?? false} ExtendedVisible={currentDraftExtendedState?.IsVisible ?? false}");
            DraftUIRenderer.RenderDraft(view, renderState, currentDraftExtendedState, isLocalTurn, currentTurnTeam, OnPickPlayerClicked, OnAcceptLateJoinerClicked);
        }

        private static DraftOverlayStateMessage GetRenderableDraftState()
        {
            if (currentDraftState != null && currentDraftState.IsVisible)
            {
                return currentDraftState;
            }

            if (currentDraftExtendedState != null && currentDraftExtendedState.IsVisible)
            {
                return CreateFallbackDraftStateFromExtended();
            }

            return currentDraftState ?? DraftOverlayStateMessage.Hidden();
        }

        private static DraftOverlayStateMessage CreateFallbackDraftStateFromExtended()
        {
            return new DraftOverlayStateMessage
            {
                IsVisible = currentDraftExtendedState != null && currentDraftExtendedState.IsVisible,
                IsCompleted = false,
                Title = string.IsNullOrWhiteSpace(currentDraftState?.Title) ? "RANKED MATCH SETUP" : currentDraftState.Title,
                RedCaptainName = currentDraftState?.RedCaptainName,
                BlueCaptainName = currentDraftState?.BlueCaptainName,
                CurrentTurnName = currentDraftState?.CurrentTurnName,
                CurrentTurnClientId = currentDraftState?.CurrentTurnClientId ?? 0,
                CurrentTurnSteamId = currentDraftState?.CurrentTurnSteamId,
                AvailablePlayers = currentDraftState?.AvailablePlayers ?? Array.Empty<string>(),
                RedPlayers = currentDraftState?.RedPlayers ?? Array.Empty<string>(),
                BluePlayers = currentDraftState?.BluePlayers ?? Array.Empty<string>(),
                PendingLateJoinerCount = currentDraftState?.PendingLateJoinerCount ?? 0,
                PendingLateJoiners = currentDraftState?.PendingLateJoiners ?? Array.Empty<string>(),
                DummyModeActive = currentDraftState?.DummyModeActive ?? false,
                FooterText = string.IsNullOrWhiteSpace(currentDraftState?.FooterText)
                    ? "Draft payload active. Waiting for full state..."
                    : currentDraftState.FooterText
            };
        }

        private static void LogOverlayTick(OverlayUiState nextState)
        {
            var signature = string.Join("|", new[]
            {
                $"current={currentUiState}",
                $"next={nextState}",
                $"basicDraft={(currentDraftState?.IsVisible ?? false)}",
                $"extendedDraft={(currentDraftExtendedState?.IsVisible ?? false)}",
                $"vote={(currentVoteState?.IsVisible ?? false)}",
                $"approval={(currentApprovalRequestState?.IsVisible ?? false)}",
                $"postMatch={(currentMatchResultState?.IsVisible ?? false)}",
                $"cachedPostMatch={(cachedMatchResultState?.IsVisible ?? false)}",
                $"welcome={ShouldAwaitWelcomeFlow()}",
                $"teamSelect={ShouldShowTeamSelectState()}"
            });

            if (string.Equals(signature, lastOverlayTickSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastOverlayTickSignature = signature;
            DraftUIPlugin.Log($"[CLIENT] Overlay tick: {signature}");
        }

        private static void RenderPostMatch(bool forceRefresh)
        {
            var playerSignature = string.Join("|", (currentMatchResultState.Players ?? Array.Empty<MatchResultPlayerMessage>())
                .Select(player => string.Join("~",
                    player?.Id ?? string.Empty,
                    player?.Username ?? string.Empty,
                    player?.Team ?? TeamResult.Unknown,
                    player?.Goals ?? 0,
                    player?.Assists ?? 0,
                    player?.Saves ?? 0,
                    player?.Shots ?? 0,
                    player?.MmrBefore ?? 0,
                    player?.MmrAfter ?? 0,
                    player?.MmrDelta ?? 0,
                    player?.IsMVP ?? false)));
            var signature = string.Join("|",
                currentMatchResultState.IsVisible,
                currentMatchResultState.WinningTeam,
                playerSignature);

            if (!forceRefresh && string.Equals(signature, lastPostMatchRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastPostMatchRenderSignature = signature;
            DraftUIPlugin.Log($"[CLIENT] POST_MATCH render requested. Winner={currentMatchResultState.WinningTeam} Players={(currentMatchResultState.Players?.Length ?? 0)}");
            PostMatchUIRenderer.Render(view, currentMatchResultState);
        }

        private static string BuildDraftEntrySignature(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.Join("~",
                entry.ClientId,
                entry.SteamId ?? string.Empty,
                entry.CommandTarget ?? string.Empty,
                entry.DisplayName ?? string.Empty,
                entry.PlayerNumber,
                entry.HasMmr,
                entry.Mmr,
                entry.IsCaptain,
                entry.Team);
        }

        private static void ResolveDraftTurnContext(DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState, out bool isLocalTurn, out TeamResult currentTurnTeam)
        {
            isLocalTurn = false;
            currentTurnTeam = ResolveDraftTurnTeam(state, extendedState);

            if (state == null || state.IsCompleted)
            {
                return;
            }

            var localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            string localSteamId = null;
            if (TryGetLocalPlayer(out var localPlayer))
            {
                localSteamId = localPlayer.SteamId.Value.ToString();
            }

            isLocalTurn = (localClientId != 0 && state.CurrentTurnClientId != 0 && localClientId == state.CurrentTurnClientId)
                || (!string.IsNullOrWhiteSpace(localSteamId)
                    && !string.IsNullOrWhiteSpace(state.CurrentTurnSteamId)
                    && string.Equals(localSteamId, state.CurrentTurnSteamId, StringComparison.Ordinal));
        }

        private static TeamResult ResolveDraftTurnTeam(DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState)
        {
            if (state == null || state.IsCompleted)
            {
                return TeamResult.Unknown;
            }

            foreach (var entry in (extendedState?.RedPlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()))
            {
                if (MatchesDraftTurnIdentity(entry, state))
                {
                    return TeamResult.Red;
                }
            }

            foreach (var entry in (extendedState?.BluePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()))
            {
                if (MatchesDraftTurnIdentity(entry, state))
                {
                    return TeamResult.Blue;
                }
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentTurnName))
            {
                if (string.Equals(state.CurrentTurnName, state.RedCaptainName, StringComparison.OrdinalIgnoreCase))
                {
                    return TeamResult.Red;
                }

                if (string.Equals(state.CurrentTurnName, state.BlueCaptainName, StringComparison.OrdinalIgnoreCase))
                {
                    return TeamResult.Blue;
                }
            }

            return TeamResult.Unknown;
        }

        private static bool MatchesDraftTurnIdentity(DraftOverlayPlayerEntryMessage entry, DraftOverlayStateMessage state)
        {
            if (entry == null || state == null || !entry.IsCaptain)
            {
                return false;
            }

            if (state.CurrentTurnClientId != 0 && entry.ClientId != 0 && entry.ClientId == state.CurrentTurnClientId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentTurnSteamId)
                && !string.IsNullOrWhiteSpace(entry.SteamId)
                && string.Equals(entry.SteamId, state.CurrentTurnSteamId, StringComparison.Ordinal))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(state.CurrentTurnName)
                && string.Equals(entry.DisplayName, state.CurrentTurnName, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildVoteEntrySignature(VoteOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.Join("~",
                entry.ClientId,
                entry.PlayerId ?? string.Empty,
                entry.SteamId ?? string.Empty,
                entry.DisplayName ?? string.Empty,
                entry.PlayerNumber,
                entry.HasVoted,
                entry.VoteAccepted,
                entry.IsInitiator);
        }

        private static void SyncLocalVoteSelectionFromState(VoteOverlayStateMessage state)
        {
            var localVoteEntry = TryFindLocalVoteEntry(state);
            if (localVoteEntry == null || !localVoteEntry.HasVoted)
            {
                return;
            }

            localVoteAccepted = localVoteEntry.VoteAccepted;
            localVoteRejected = !localVoteEntry.VoteAccepted;
        }

        private static VoteOverlayPlayerEntryMessage TryFindLocalVoteEntry(VoteOverlayStateMessage state)
        {
            var playerEntries = state?.PlayerEntries ?? Array.Empty<VoteOverlayPlayerEntryMessage>();
            if (playerEntries.Length == 0)
            {
                return null;
            }

            var localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            string localSteamId = null;
            if (TryGetLocalPlayer(out var localPlayer))
            {
                localSteamId = localPlayer.SteamId.Value.ToString();
            }

            return playerEntries.FirstOrDefault(entry =>
                entry != null
                && ((localClientId != 0 && entry.ClientId == localClientId)
                    || (!string.IsNullOrWhiteSpace(localSteamId)
                        && string.Equals(entry.SteamId, localSteamId, StringComparison.Ordinal))));
        }

        private static void OnVoteAcceptedClicked()
        {
            localVoteAccepted = true;
            localVoteRejected = false;
            SendChatCommand("/y");
            DraftUIPlugin.Log("Voting Accepted");
            RefreshUi(forceRefresh: true);
        }

        private static void OnVoteRejectedClicked()
        {
            suppressVoteUntilHidden = false;
            localVoteAccepted = false;
            localVoteRejected = true;
            SendChatCommand("/n");
            DraftUIPlugin.Log("Voting Rejected");
            RefreshUi(forceRefresh: true);
        }

        private static void OnApprovalAcceptedClicked()
        {
            if (!string.IsNullOrWhiteSpace(currentApprovalRequestState?.RequestId))
            {
                SendChatCommand($"/approve {currentApprovalRequestState.RequestId}");
            }
        }

        private static void OnApprovalRejectedClicked()
        {
            if (!string.IsNullOrWhiteSpace(currentApprovalRequestState?.RequestId))
            {
                SendChatCommand($"/reject {currentApprovalRequestState.RequestId}");
            }
        }

        private static void OnPickPlayerClicked(string commandTarget)
        {
            if (!string.IsNullOrWhiteSpace(commandTarget))
            {
                DraftUIPlugin.Log($"Draft pick click target={commandTarget}");
                SendChatCommand($"/pick {commandTarget}");
            }
        }

        private static void OnAcceptLateJoinerClicked(string commandTarget)
        {
            if (!string.IsNullOrWhiteSpace(commandTarget))
            {
                DraftUIPlugin.Log($"Draft accept click target={commandTarget}");
                SendChatCommand($"/accept {commandTarget}");
            }
        }

        private static void OnWelcomeDiscordClicked()
        {
            OpenDiscordInvite(Constants.DISCORD_INVITE_URL, "Welcome Discord button");
        }

        private static void OnWelcomeContinueClicked()
        {
            DismissWelcomeScreen("Welcome UI Continued");
        }

        private static void OpenDiscordInvite(string url, string sourceLabel)
        {
            try
            {
                var inviteUrl = string.IsNullOrWhiteSpace(url) ? Constants.DISCORD_INVITE_URL : url;
                Application.OpenURL(inviteUrl);
                DraftUIPlugin.Log($"Discord invite opened via {sourceLabel}: {inviteUrl}");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to open Discord invite from {sourceLabel}: {ex}");
            }
        }

        private static void OnPostMatchContinueClicked()
        {
            DismissPostMatch("Post-match UI Continued");
        }

        private static void OnPostMatchCloseClicked()
        {
            DismissPostMatch("Post-match UI Closed");
        }

        private static void SendChatCommand(string message)
        {
            try
            {
                var chat = UIChat.Instance;
                if (!chat || uiChatSendMessageMethod == null)
                {
                    return;
                }

                uiChatSendMessageMethod.Invoke(chat, new object[] { message, false });
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to send UI command '{message}': {ex}");
            }
        }

        private static int GetDraftOverlayPlayerCount()
        {
            return (currentDraftExtendedState.AvailablePlayerEntries?.Length ?? currentDraftState.AvailablePlayers?.Length ?? 0)
                + (currentDraftExtendedState.RedPlayerEntries?.Length ?? currentDraftState.RedPlayers?.Length ?? 0)
                + (currentDraftExtendedState.BluePlayerEntries?.Length ?? currentDraftState.BluePlayers?.Length ?? 0)
                + (currentDraftExtendedState.PendingLateJoinerEntries?.Length ?? currentDraftState.PendingLateJoiners?.Length ?? 0);
        }

        private static int GetPostMatchPlayerCount()
        {
            return currentMatchResultState?.Players?.Length ?? 0;
        }

        public static bool IsBlockingGameplayUI()
        {
            if (!ShouldOwnCursor(currentUiState))
            {
                return false;
            }

            if (HasVisibleBlockingOverlay())
            {
                return true;
            }

            if (currentUiState == OverlayUiState.Welcome)
            {
                return false;
            }

            if (currentUiState == OverlayUiState.PostMatch && HasCachedPostMatchResult())
            {
                return true;
            }

            if (currentUiState == OverlayUiState.Draft)
            {
                ForceRecoverToTeamSelect($"Blocking UI state {currentUiState} has no visible overlay");
            }

            return false;
        }

        private static bool ShouldSuppressGameplaySelectionUi()
        {
            return ShouldAwaitWelcomeFlow()
                || currentUiState == OverlayUiState.Draft
                || IsPostMatchStateLocked();
        }

        private static bool ShouldBlockGameplayState(object state)
        {
            var stateName = state?.ToString() ?? string.Empty;
            return string.Equals(stateName, "TeamSelect", StringComparison.Ordinal)
                || string.Equals(stateName, "PositionSelectBlue", StringComparison.Ordinal)
                || string.Equals(stateName, "PositionSelectRed", StringComparison.Ordinal);
        }

        private static bool IsPostMatchBlockingGameplayUi()
        {
            return IsPostMatchStateLocked();
        }

        private static bool ShouldAwaitWelcomeFlow()
        {
            return welcomePendingAcknowledgement && HasLocalPlayerContext();
        }

        private static bool ShouldShowWelcomeScreen()
        {
            return ShouldAwaitWelcomeFlow() && view?.Welcome?.Panel != null;
        }

        private static bool HasLocalPlayerContext()
        {
            try
            {
                var manager = PlayerManager.Instance;
                if (!manager)
                {
                    return false;
                }

                var localPlayer = manager.GetLocalPlayer();
                return localPlayer;
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldBlockGameplayStateRpc(object state)
        {
            return (IsPostMatchBlockingGameplayUi() || HasActiveDraftPayload() || currentUiState == OverlayUiState.Draft)
                && ShouldBlockGameplayState(state);
        }

        private static void EnforceGameplayUiPriority()
        {
            if (!ShouldSuppressGameplaySelectionUi() && !IsBlockingGameplayUI())
            {
                return;
            }

            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    return;
                }

                if (uiManager.PauseMenu != null && uiManager.PauseMenu.IsVisible)
                {
                    uiManager.PauseMenu.Hide();
                }

                if (uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible)
                {
                    uiManager.TeamSelect.Hide();
                }

                if (uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible)
                {
                    uiManager.PositionSelect.Hide();
                }

                if (uiManager.Scoreboard != null && uiManager.Scoreboard.IsVisible)
                {
                    uiManager.Scoreboard.Hide();
                    UIInputState.isScoreboardOpen = false;
                }

                UIInputState.Sync(uiManager);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to enforce gameplay UI priority: {ex}");
            }
        }

        private static void SendPostMatchDismiss()
        {
            try
            {
                var networkManager = NetworkManager.Singleton;
                var messagingManager = networkManager != null && networkManager.IsClient
                    ? networkManager.CustomMessagingManager
                    : null;
                if (messagingManager == null)
                {
                    return;
                }

                var message = new MatchResultDismissMessage();
                var capacity = RankedOverlayNetcode.EstimateCapacity(message);
                var writer = new FastBufferWriter(capacity, Allocator.Temp);
                try
                {
                    RankedOverlayNetcode.WriteJson(ref writer, message);
                    messagingManager.SendNamedMessage(RankedOverlayChannels.MatchResultDismiss, NetworkManager.ServerClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to dismiss post-match on server: {ex}");
            }
        }

        private static void DismissPostMatch(string actionLabel)
        {
            SendPostMatchDismiss();
            currentMatchResultState = MatchResultMessage.Hidden();
            cachedMatchResultState = MatchResultMessage.Hidden();
            lastVisibleMatchResultReceivedAt = -1f;
            hasLoggedPostMatchFallback = false;
            postMatchDismissed = true;
            DraftUIPlugin.Log(actionLabel);
            ApplyUiState(OverlayUiState.None, forceRefresh: true);
            RestoreSuppressedGameplayUi();
            RefreshUi(forceRefresh: true);
        }

        private static void DismissWelcomeScreen(string actionLabel)
        {
            welcomePendingAcknowledgement = false;
            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            DraftUIPlugin.Log(actionLabel);
            ApplyUiState(OverlayUiState.TeamSelect, forceRefresh: true);
            RefreshUi(forceRefresh: true);
        }

        private static void RestoreSuppressedGameplayUi()
        {
            try
            {
                var uiManager = UIManager.Instance;
                var manager = PlayerManager.Instance;
                if (!uiManager || !manager)
                {
                    return;
                }

                var localPlayer = manager.GetLocalPlayer();
                if (!localPlayer)
                {
                    return;
                }

                var stateName = localPlayer.State.Value.ToString() ?? string.Empty;
                if (string.Equals(stateName, "TeamSelect", StringComparison.Ordinal))
                {
                    if (uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible)
                    {
                        uiManager.PositionSelect.Hide();
                    }

                    uiManager.TeamSelect?.Show();
                }
                else if (string.Equals(stateName, "PositionSelectBlue", StringComparison.Ordinal)
                    || string.Equals(stateName, "PositionSelectRed", StringComparison.Ordinal))
                {
                    if (uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible)
                    {
                        uiManager.TeamSelect.Hide();
                    }

                    uiManager.PositionSelect?.Show();
                }

                UIInputState.Sync(uiManager);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to restore suppressed gameplay UI: {ex}");
            }
        }

        private static void UpdateStateFailsafes()
        {
            UpdateWelcomeFailsafe();
            UpdatePostMatchFailsafe();
        }

        private static void UpdateWelcomeFailsafe()
        {
            if (!ShouldAwaitWelcomeFlow() || welcomeRequestedAt < 0f)
            {
                return;
            }

            if (IsWelcomeOverlayVisible())
            {
                return;
            }

            var elapsed = Time.unscaledTime - Mathf.Max(0f, welcomeRequestedAt);
            if (elapsed < WelcomeVisibilityFailsafeDelay)
            {
                return;
            }

            if (!hasLoggedWelcomeFallback)
            {
                hasLoggedWelcomeFallback = true;
                DraftUIPlugin.LogError($"Welcome UI failed to become visible before TeamSelect after {elapsed:0.00}s. Recovering to TeamSelect.");
            }

            ForceRecoverToTeamSelect("Welcome UI visibility failsafe triggered");
        }

        private static void UpdatePostMatchFailsafe()
        {
            if (!HasCachedPostMatchResult() || lastVisibleMatchResultReceivedAt < 0f)
            {
                return;
            }

            if (ShouldShowPostMatchUi() && IsPostMatchOverlayVisible())
            {
                return;
            }

            var elapsed = Time.unscaledTime - Mathf.Max(0f, lastVisibleMatchResultReceivedAt);
            if (elapsed < PostMatchVisibilityFailsafeDelay)
            {
                return;
            }

            if (!hasLoggedPostMatchFallback)
            {
                hasLoggedPostMatchFallback = true;
                DraftUIPlugin.LogError($"Post-match UI recovery triggered after {elapsed:0.00}s without a visible result overlay.");
            }

            currentMatchResultState = cachedMatchResultState;
            EnsureViewSetup();
            RefreshUi(forceRefresh: true);
        }

        private static void ForceRecoverToTeamSelect(string reason)
        {
            welcomePendingAcknowledgement = false;
            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            DraftUIPlugin.LogError($"UI recovery: {reason}");
            DraftUIRenderer.ShowHidden(view);
            currentUiOpacity = 0f;
            targetUiOpacity = 0f;
            pendingHideAfterFade = false;
            UIInputState.isDraftUIOpen = false;
            ReleaseCursor();
            ApplyUiState(OverlayUiState.TeamSelect, forceRefresh: true);
            RestoreSuppressedGameplayUi();
        }

        private static void FreezeLocalGameplayInputIfBlocked()
        {
            if (!IsPostMatchStateLocked())
            {
                return;
            }

            ResetLocalInputs();
        }

        private static bool HasVisibleBlockingOverlay()
        {
            return IsWelcomeOverlayVisible() || IsDraftOverlayVisible() || IsPostMatchOverlayVisible();
        }

        private static bool IsWelcomeOverlayVisible()
        {
            return currentUiState == OverlayUiState.Welcome
                && view?.Container != null
                && view.Container.style.display == DisplayStyle.Flex
                && view?.Welcome?.Panel != null
                && view.Welcome.Panel.style.display == DisplayStyle.Flex;
        }

        private static bool IsDraftOverlayVisible()
        {
            return currentUiState == OverlayUiState.Draft
                && view?.Container != null
                && view.Container.style.display == DisplayStyle.Flex
                && ((view.DraftPanel != null && view.DraftPanel.style.display == DisplayStyle.Flex)
                    || (view.ApprovalPanel != null && view.ApprovalPanel.style.display == DisplayStyle.Flex)
                    || (view.VotingPanel != null && view.VotingPanel.style.display == DisplayStyle.Flex));
        }

        private static bool IsPostMatchOverlayVisible()
        {
            return currentUiState == OverlayUiState.PostMatch
                && view?.Container != null
                && view.Container.style.display == DisplayStyle.Flex
                && view?.PostMatch?.Panel != null
                && view.PostMatch.Panel.style.display == DisplayStyle.Flex;
        }

        private static string GetLocalPlayerStateName()
        {
            try
            {
                var manager = PlayerManager.Instance;
                if (!manager)
                {
                    return string.Empty;
                }

                var localPlayer = manager.GetLocalPlayer();
                if (!localPlayer)
                {
                    return string.Empty;
                }

                return localPlayer.State.Value.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TryRecoverApprovedLateJoinHandoff()
        {
            if (ShouldSuppressGameplaySelectionUi())
            {
                return;
            }

            if (Time.unscaledTime < suppressAssignedTeamHandoffUntil)
            {
                return;
            }

            try
            {
                if (!TryGetLocalPlayer(out var localPlayer))
                {
                    return;
                }

                var stateName = localPlayer.State.Value.ToString() ?? string.Empty;
                var teamName = localPlayer.Team.Value.ToString() ?? string.Empty;
                if (!string.Equals(stateName, "TeamSelect", StringComparison.Ordinal) || !IsPlayableTeamName(teamName))
                {
                    return;
                }

                if (Time.unscaledTime - lastLateJoinHandoffAttemptAt < LateJoinHandoffRetryInterval)
                {
                    return;
                }

                lastLateJoinHandoffAttemptAt = Time.unscaledTime;
                DraftUIPlugin.Log($"[CLIENT][JOIN] Local player is assigned to team {teamName} while still in TeamSelect. Requesting PositionSelect handoff.");

                var uiManager = UIManager.Instance;
                if (uiManager && uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible)
                {
                    uiManager.TeamSelect.Hide();
                }

                if (playerSetStateRpcMethod == null)
                {
                    DraftUIPlugin.LogError("[CLIENT][JOIN] Could not resolve Player.Client_SetPlayerStateRpc for late-join handoff recovery.");
                    return;
                }

                var stateType = localPlayer.State.Value.GetType();
                var targetStateName = string.Equals(teamName, "Red", StringComparison.OrdinalIgnoreCase)
                    ? "PositionSelectRed"
                    : "PositionSelectBlue";
                var targetState = Enum.Parse(stateType, targetStateName, true);
                playerSetStateRpcMethod.Invoke(localPlayer, new object[] { targetState, 0f });
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to recover approved late-join handoff: {ex}");
            }
        }

        private static void TrackLocalSelectionUiState()
        {
            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager || !TryGetLocalPlayer(out var localPlayer))
                {
                    return;
                }

                var stateName = localPlayer.State.Value.ToString() ?? string.Empty;
                var teamName = localPlayer.Team.Value.ToString() ?? string.Empty;
                var teamSelectVisible = uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible;
                var positionSelectVisible = uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible;

                if ((string.Equals(stateName, "PositionSelectBlue", StringComparison.Ordinal)
                    || string.Equals(stateName, "PositionSelectRed", StringComparison.Ordinal))
                    && uiManager.TeamSelect != null
                    && uiManager.TeamSelect.IsVisible)
                {
                    uiManager.TeamSelect.Hide();
                    teamSelectVisible = false;
                }

                if ((string.Equals(stateName, "PositionSelectBlue", StringComparison.Ordinal)
                    || string.Equals(stateName, "PositionSelectRed", StringComparison.Ordinal))
                    && uiManager.PositionSelect != null
                    && !uiManager.PositionSelect.IsVisible)
                {
                    uiManager.PositionSelect.Show();
                    positionSelectVisible = true;
                }

                if (!lastObservedTeamSelectVisible && teamSelectVisible)
                {
                    lastObservedLocalSelectionState = stateName;
                }

                if (lastObservedTeamSelectVisible && !teamSelectVisible)
                {
                    DraftUIPlugin.Log("[CLIENT][JOIN] team select closed for local player.");
                }

                if (!lastObservedPositionSelectVisible && positionSelectVisible && IsPlayableTeamName(teamName))
                {
                    DraftUIPlugin.Log($"[CLIENT][JOIN] position select opened for team {teamName}.");
                }

                lastObservedTeamSelectVisible = teamSelectVisible;
                lastObservedPositionSelectVisible = positionSelectVisible;
                lastObservedLocalSelectionState = stateName;
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to track local selection UI state: {ex}");
            }
        }

        private static bool TryGetLocalPlayer(out Player localPlayer)
        {
            localPlayer = null;

            try
            {
                var manager = PlayerManager.Instance;
                if (!manager)
                {
                    return false;
                }

                localPlayer = manager.GetLocalPlayer();
                return localPlayer;
            }
            catch
            {
                localPlayer = null;
                return false;
            }
        }

        private static bool IsPlayableTeamName(string teamName)
        {
            return string.Equals(teamName, "Red", StringComparison.OrdinalIgnoreCase)
                || string.Equals(teamName, "Blue", StringComparison.OrdinalIgnoreCase);
        }

        private static void AcquireCursor(bool forceResetInputs)
        {
            if (!UIInputState.ShouldCursorBeVisible())
            {
                return;
            }

            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    return;
                }

                UIInputState.Sync(uiManager);
                if (!UIInputState.isCursorLocked && !forceResetInputs)
                {
                    return;
                }

                uiManagerShowMouseMethod?.Invoke(uiManager, null);
                if (forceResetInputs || UIInputState.isCursorLocked)
                {
                    ResetLocalInputs();
                }
                UIInputState.Sync(uiManager);

                if (!UIInputState.isCursorLocked)
                {
                    DraftUIPlugin.Log("Cursor Unlocked");
                }
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to unlock cursor: {ex}");
            }
        }

        private static void ReleaseCursor()
        {
            if (UIInputState.ShouldCursorBeVisible())
            {
                return;
            }

            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    UIInputState.isCursorLocked = true;
                    return;
                }

                UIInputState.Sync(uiManager);
                if (UIInputState.isCursorLocked)
                {
                    return;
                }

                if (uiManagerUpdateMouseVisibilityMethod != null)
                {
                    uiManagerUpdateMouseVisibilityMethod.Invoke(uiManager, null);
                }
                else
                {
                    uiManagerHideMouseMethod?.Invoke(uiManager, null);
                }

                UIInputState.Sync(uiManager);

                if (UIInputState.isCursorLocked)
                {
                    DraftUIPlugin.Log("Cursor Locked");
                }
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to lock cursor: {ex}");
            }
        }

        private static void ResetLocalInputs()
        {
            try
            {
                var manager = PlayerManager.Instance;
                if (!manager)
                {
                    return;
                }

                var localPlayer = manager.GetLocalPlayer();
                if (!localPlayer || localPlayer.PlayerInput == null || playerInputResetInputsMethod == null)
                {
                    return;
                }

                playerInputResetInputsMethod.Invoke(localPlayer.PlayerInput, new object[] { false });
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to reset local inputs: {ex}");
            }
        }

        private static void SyncInputState()
        {
            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    return;
                }

                UIInputState.Sync(uiManager);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to sync UI input state: {ex}");
            }
        }

        private static void EnsureCursorConsistency()
        {
            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    return;
                }

                UIInputState.Sync(uiManager);
                if (UIInputState.ShouldCursorBeVisible() && UIInputState.isCursorLocked)
                {
                    AcquireCursor(forceResetInputs: true);
                }
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to enforce cursor consistency: {ex}");
            }
        }

        private static void NotifyScoreboardOpened()
        {
            UIInputState.isScoreboardOpen = true;
            SyncInputState();
        }

        private static void NotifyScoreboardClosed()
        {
            UIInputState.isScoreboardOpen = false;
            SyncInputState();

            if (UIInputState.isDraftUIOpen)
            {
                AcquireCursor(forceResetInputs: true);
            }
            else
            {
                ReleaseCursor();
            }
        }

        [HarmonyPatch(typeof(UIHUDController), "Event_OnPlayerBodySpawned")]
        public static class UiHudControllerEventOnPlayerBodySpawnedPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIHUDController __instance, Dictionary<string, object> message)
            {
                if (!isSetup || view?.Container == null || view.Container.parent == null)
                {
                    EnsureViewSetup();
                    if (!isSetup)
                    {
                        Setup(__instance);
                    }
                }

                RefreshUi(forceRefresh: true);
            }
        }

        [HarmonyPatch(typeof(UIManager), "Start")]
        public static class UiManagerStartPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIManager __instance)
            {
                TrySetupFromUiManager();
            }
        }

        [HarmonyPatch(typeof(Player), "OnNetworkPostSpawn")]
        public static class PlayerOnNetworkPostSpawnPatch
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                HandleLocalPlayerInitialized(__instance);
            }
        }

        [HarmonyPatch(typeof(SynchronizedObjectManager), "Update")]
        public static class SynchronizedObjectManagerUpdatePatch
        {
            [HarmonyPostfix]
            public static void Postfix(SynchronizedObjectManager __instance)
            {
                UpdateOverlay();
            }
        }

        [HarmonyPatch(typeof(UIManagerInputs), "OnScoreboardActionStarted")]
        public static class UiManagerInputsScoreboardStartedPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!IsBlockingGameplayUI())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                NotifyScoreboardClosed();
                return false;
            }

            [HarmonyPostfix]
            public static void Postfix()
            {
                if (!IsBlockingGameplayUI())
                {
                    NotifyScoreboardOpened();
                }
            }
        }

        [HarmonyPatch(typeof(UIManagerInputs), "OnScoreboardActionCanceled")]
        public static class UiManagerInputsScoreboardCanceledPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                NotifyScoreboardClosed();
            }
        }

        [HarmonyPatch(typeof(UIManagerInputs), "OnPauseActionPerformed")]
        public static class UiManagerInputsPauseActionPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!ShouldSuppressGameplaySelectionUi() && !IsBlockingGameplayUI())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(UIManagerInputs), "OnPositionSelectActionPerformed")]
        public static class UiManagerInputsPositionSelectActionPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!ShouldSuppressGameplaySelectionUi())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "Event_Client_OnPlayerSelectTeam")]
        public static class PlayerControllerSelectTeamPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!ShouldSuppressGameplaySelectionUi())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "Event_Client_OnPauseMenuClickSwitchTeam")]
        public static class PlayerControllerPauseSwitchTeamPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                try
                {
                    if (TryGetLocalPlayer(out var localPlayer))
                    {
                        var teamName = localPlayer.Team.Value.ToString() ?? string.Empty;
                        if (IsPlayableTeamName(teamName))
                        {
                            suppressAssignedTeamHandoffUntil = Time.unscaledTime + ManualSwitchTeamHandoffSuppressDuration;
                        }
                    }
                }
                catch { }

                if (!ShouldSuppressGameplaySelectionUi())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "Event_Client_OnPlayerRequestPositionSelect")]
        public static class PlayerControllerRequestPositionSelectPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!ShouldSuppressGameplaySelectionUi())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(UIPositionSelect), "OnPositionClicked")]
        public static class UiPositionSelectClickedPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!ShouldSuppressGameplaySelectionUi())
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(Player), "Client_SetPlayerStateRpc")]
        public static class PlayerClientSetPlayerStatePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(object state)
            {
                if (!ShouldBlockGameplayStateRpc(state))
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        [HarmonyPatch(typeof(UIManagerStateController), "Event_OnPlayerStateChanged")]
        public static class UiManagerStateControllerPlayerStateChangedPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Dictionary<string, object> message)
            {
                if (!ShouldSuppressGameplaySelectionUi() || message == null || !message.TryGetValue("player", out var playerObject))
                {
                    return true;
                }

                if (!(playerObject is Player player) || !player.IsLocalPlayer || !ShouldBlockGameplayState(player.State.Value))
                {
                    return true;
                }

                EnforceGameplayUiPriority();
                return false;
            }
        }

        public static void Shutdown()
        {
            isSetup = false;
            suppressVoteUntilHidden = false;
            localVoteAccepted = false;
            localVoteRejected = false;
            lastVoteRenderSignature = null;
            lastApprovalRenderSignature = null;
            lastDraftRenderSignature = null;
            lastPostMatchRenderSignature = null;
            currentUiState = OverlayUiState.None;
            currentVoteState = VoteOverlayStateMessage.Hidden();
            currentApprovalRequestState = ApprovalRequestStateMessage.Hidden();
            currentDraftState = DraftOverlayStateMessage.Hidden();
            currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
            currentMatchResultState = MatchResultMessage.Hidden();
            cachedMatchResultState = MatchResultMessage.Hidden();
            postMatchDismissed = false;
            welcomePendingAcknowledgement = true;
            currentStateEnteredAt = -1f;
            welcomeRequestedAt = -1f;
            lastVisibleMatchResultReceivedAt = -1f;
            hasLoggedWelcomeFallback = false;
            hasLoggedPostMatchFallback = false;
            currentUiOpacity = 0f;
            targetUiOpacity = 0f;
            pendingHideAfterFade = false;
            UIInputState.Reset();

            ReleaseCursor();
            UnregisterHandlers();

            handlersRegistered = false;

            if (view?.Container != null)
            {
                view.Container.RemoveFromHierarchy();
            }

            VoteAvatarCache.Clear();
            view = null;
        }
    }
}