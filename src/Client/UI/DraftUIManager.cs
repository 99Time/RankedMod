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
        private static float voteStateReceivedAt;
        private static string lastVoteRenderSignature;
        private static string lastApprovalRenderSignature;
        private static string lastDraftRenderSignature;
        private static string lastPostMatchRenderSignature;
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
                }
                else
                {
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
                currentDraftState = preservePostMatch ? DraftOverlayStateMessage.Hidden() : incomingState;
                if (currentDraftState == null || !currentDraftState.IsVisible)
                {
                    currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
                }
                else
                {
                    currentMatchResultState = MatchResultMessage.Hidden();
                    postMatchDismissed = false;
                }

                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("Ignoring draft overlay while post-match results are visible.");
                }

                DraftUIPlugin.Log($"Draft state received. Visible={currentDraftState.IsVisible} Available={(currentDraftState.AvailablePlayers?.Length ?? 0)} Red={(currentDraftState.RedPlayers?.Length ?? 0)} Blue={(currentDraftState.BluePlayers?.Length ?? 0)} Pending={(currentDraftState.PendingLateJoiners?.Length ?? 0)}");
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
                currentDraftExtendedState = preservePostMatch ? DraftOverlayExtendedMessage.Hidden() : incomingState;
                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("Ignoring draft overlay while post-match results are visible.");
                }
                DraftUIPlugin.Log($"Draft extended state received. Visible={currentDraftExtendedState.IsVisible} Available={(currentDraftExtendedState.AvailablePlayerEntries?.Length ?? 0)} Red={(currentDraftExtendedState.RedPlayerEntries?.Length ?? 0)} Blue={(currentDraftExtendedState.BluePlayerEntries?.Length ?? 0)} Pending={(currentDraftExtendedState.PendingLateJoinerEntries?.Length ?? 0)}");
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

                DraftUIPlugin.Log($"MatchResultMessage received. Visible={currentMatchResultState.IsVisible} Winner={currentMatchResultState.WinningTeam} Players={(currentMatchResultState.Players?.Length ?? 0)}");
                if (currentMatchResultState.IsVisible)
                {
                    EnsureViewSetup();
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
            ApplyUiState(nextState, forceRefresh);

            if (nextState == OverlayUiState.Draft)
            {
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
            return (currentDraftState != null && currentDraftState.IsVisible)
                || (currentApprovalRequestState != null && currentApprovalRequestState.IsVisible)
                || (currentVoteState != null && currentVoteState.IsVisible && !suppressVoteUntilHidden);
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
            if (currentDraftState != null && currentDraftState.IsVisible)
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
            if (currentDraftState != null && currentDraftState.IsVisible)
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
            var secondsRemaining = Mathf.Max(0, Mathf.CeilToInt(currentVoteState.SecondsRemaining - (Time.unscaledTime - voteStateReceivedAt)));
            var signature = string.Join("|",
                currentVoteState.Title ?? string.Empty,
                currentVoteState.PromptText ?? string.Empty,
                currentVoteState.InitiatorName ?? string.Empty,
                currentVoteState.YesVotes,
                currentVoteState.NoVotes,
                currentVoteState.RequiredYesVotes,
                currentVoteState.EligibleCount,
                currentVoteState.FooterText ?? string.Empty,
                secondsRemaining,
                localVoteAccepted);

            if (!forceRefresh && string.Equals(signature, lastVoteRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastVoteRenderSignature = signature;
            DraftUIRenderer.RenderVoting(view, currentVoteState, secondsRemaining, localVoteAccepted);
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
            var signature = string.Join("|",
                currentDraftState.Title ?? string.Empty,
                currentDraftState.RedCaptainName ?? string.Empty,
                currentDraftState.BlueCaptainName ?? string.Empty,
                currentDraftState.CurrentTurnName ?? string.Empty,
                currentDraftState.IsCompleted,
                currentDraftState.FooterText ?? string.Empty,
                currentDraftState.PendingLateJoinerCount,
                string.Join(",", currentDraftState.AvailablePlayers ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.AvailablePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", currentDraftState.RedPlayers ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.RedPlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", currentDraftState.BluePlayers ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.BluePlayerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)),
                string.Join(",", currentDraftState.PendingLateJoiners ?? Array.Empty<string>()),
                string.Join(",", (currentDraftExtendedState.PendingLateJoinerEntries ?? Array.Empty<DraftOverlayPlayerEntryMessage>()).Select(BuildDraftEntrySignature)));

            if (!forceRefresh && string.Equals(signature, lastDraftRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastDraftRenderSignature = signature;
            DraftUIRenderer.RenderDraft(view, currentDraftState, currentDraftExtendedState, OnPickPlayerClicked, OnAcceptLateJoinerClicked);
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
            PostMatchUIRenderer.Render(view, currentMatchResultState);
        }

        private static string BuildDraftEntrySignature(DraftOverlayPlayerEntryMessage entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return string.Join("~",
                entry.CommandTarget ?? string.Empty,
                entry.DisplayName ?? string.Empty,
                entry.PlayerNumber,
                entry.HasMmr,
                entry.Mmr,
                entry.IsCaptain,
                entry.Team);
        }

        private static void OnVoteAcceptedClicked()
        {
            localVoteAccepted = true;
            SendChatCommand("/y");
            DraftUIPlugin.Log("Voting Accepted");
            RefreshUi(forceRefresh: true);
        }

        private static void OnVoteRejectedClicked()
        {
            suppressVoteUntilHidden = true;
            localVoteAccepted = false;
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
                SendChatCommand($"/pick {commandTarget}");
            }
        }

        private static void OnAcceptLateJoinerClicked(string commandTarget)
        {
            if (!string.IsNullOrWhiteSpace(commandTarget))
            {
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
            return IsPostMatchBlockingGameplayUi() && ShouldBlockGameplayState(state);
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

            view = null;
        }
    }
}