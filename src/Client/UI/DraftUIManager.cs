using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace schrader
{
    public static class DraftUIManager
    {
        private enum OverlayUiState
        {
            None,
            DiscordOnboarding,
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
        private static float approvalStateReceivedAt;
        private static bool approvalInteractionActive;
        private static string lastVoteRenderSignature;
        private static string lastApprovalRenderSignature;
        private static string lastDraftRenderSignature;
        private static string lastPostMatchRenderSignature;
        private static string lastDismissedPostMatchSignature;
        private static string lastOverlayTickSignature;
        private static string lastCursorDebugSignature;
        private static OverlayUiState currentUiState = OverlayUiState.None;
        private static CustomMessagingManager currentMessagingManager;
        private static VoteOverlayStateMessage currentVoteState = VoteOverlayStateMessage.Hidden();
        private static ApprovalRequestStateMessage currentApprovalRequestState = ApprovalRequestStateMessage.Hidden();
        private static DraftOverlayStateMessage currentDraftState = DraftOverlayStateMessage.Hidden();
        private static DraftOverlayExtendedMessage currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
        private static MatchResultMessage currentMatchResultState = MatchResultMessage.Hidden();
        private static MatchResultMessage cachedMatchResultState = MatchResultMessage.Hidden();
        private static bool postMatchDismissed;
        private static bool welcomePendingAcknowledgement;
        private static bool publicServerModeActive;
        private static bool trainingServerModeActive;
        private static bool discordOnboardingStateResolved;
        private static bool discordOnboardingIsLinked;
        private static bool discordOnboardingDismissed;
        private static bool discordOnboardingFallbackActive;
        private static string pendingDiscordLinkCommand;
        private static float pendingDiscordLinkQueuedAt = -1f;
        private static bool hasLoggedPendingDiscordLinkWait;
        private static float currentStateEnteredAt = -1f;
        private static float welcomeRequestedAt = -1f;
        private static float discordOnboardingDecisionRequestedAt = -1f;
        private static float lastVisibleMatchResultReceivedAt = -1f;
        private static bool hasLoggedWelcomeFallback;
        private static bool hasLoggedPostMatchFallback;
        private static bool hasLoggedDiscordOnboardingFallback;
        private static float currentUiOpacity;
        private static float targetUiOpacity;
        private static bool pendingHideAfterFade;
        private static readonly bool EnableOverlayHotPathDiagnostics = false;
        private const float UiFadeSpeed = 7.5f;
        private const float DiscordOnboardingDecisionFailsafeDelay = 1.25f;
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
                OnDiscordOnboardingVerifySubmitted,
                OnDiscordOnboardingJoinClicked,
                OnDiscordOnboardingLeaveClicked,
                OnDiscordOnboardingCloseVerificationClicked,
                OnWelcomeDiscordClicked,
                OnWelcomeHostClicked,
                OnWelcomeContinueClicked,
                OnPostMatchHostClicked,
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

            welcomePendingAcknowledgement = true;
            publicServerModeActive = false;
            trainingServerModeActive = false;
            discordOnboardingStateResolved = false;
            discordOnboardingIsLinked = false;
            discordOnboardingDismissed = false;
            discordOnboardingFallbackActive = false;
            TrainingClientRuntime.SetTrainingServerMode(false);
            pendingDiscordLinkCommand = null;
            pendingDiscordLinkQueuedAt = -1f;
            hasLoggedPendingDiscordLinkWait = false;
            welcomeRequestedAt = -1f;
            discordOnboardingDecisionRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            hasLoggedDiscordOnboardingFallback = false;
            DraftUIPlugin.Log("[CLIENT][JOIN] Local player created. Welcome flow armed with safe gameplay gating.");
            EnsureMessagingHandlers();
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
            eventManager.AddEventListener("Event_Client_OnClientStopped", new Action<Dictionary<string, object>>(OnClientDisconnected));
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
            ScoreboardStarClientState.Reset();
            suppressVoteUntilHidden = false;
            localVoteAccepted = false;
            localVoteRejected = false;
            lastVoteRenderSignature = null;
            lastApprovalRenderSignature = null;
            lastDraftRenderSignature = null;
            lastPostMatchRenderSignature = null;
            lastDismissedPostMatchSignature = null;
            postMatchDismissed = false;
            welcomePendingAcknowledgement = false;
            publicServerModeActive = false;
            trainingServerModeActive = false;
            discordOnboardingStateResolved = false;
            discordOnboardingIsLinked = false;
            discordOnboardingDismissed = false;
            discordOnboardingFallbackActive = false;
            TrainingClientRuntime.SetTrainingServerMode(false);
            pendingDiscordLinkCommand = null;
            pendingDiscordLinkQueuedAt = -1f;
            hasLoggedPendingDiscordLinkWait = false;
            currentStateEnteredAt = -1f;
            welcomeRequestedAt = -1f;
            discordOnboardingDecisionRequestedAt = -1f;
            lastVisibleMatchResultReceivedAt = -1f;
            hasLoggedWelcomeFallback = false;
            hasLoggedPostMatchFallback = false;
            hasLoggedDiscordOnboardingFallback = false;
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
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.ScoreboardStars, ScoreboardStarClientState.OnScoreboardStarsReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.ScoreboardBadges, ScoreboardBadgeClientState.OnScoreboardBadgesReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.DiscordOnboardingState, OnDiscordOnboardingStateReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.DiscordInviteOpen, OnDiscordInviteOpenReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.ExternalUrlOpen, OnExternalUrlOpenReceived);
                messagingManager.RegisterNamedMessageHandler(RankedOverlayChannels.TrainingOpenWorldPose, OnTrainingOpenWorldPoseReceived);
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
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.ScoreboardStars); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.ScoreboardBadges); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.DiscordOnboardingState); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.DiscordInviteOpen); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.ExternalUrlOpen); } catch { }
                try { currentMessagingManager.UnregisterNamedMessageHandler(RankedOverlayChannels.TrainingOpenWorldPose); } catch { }
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
                DraftUIPlugin.Log($"[CLIENT] [VOTE][DEBUG] Vote state received. sender={senderClientId} incomingVisible={incomingState.IsVisible} currentUi={currentUiState} postMatchLock={DescribePostMatchLockState()} welcomePending={welcomePendingAcknowledgement} suppressVoteUntilHidden={suppressVoteUntilHidden}");
                currentVoteState = preservePostMatch ? VoteOverlayStateMessage.Hidden() : incomingState;
                voteStateReceivedAt = Time.unscaledTime;

                DraftUIPlugin.Log($"[CLIENT] [VOTE] Vote state received. Visible={incomingState.IsVisible} Eligible={incomingState.EligibleCount} Yes={incomingState.YesVotes} No={incomingState.NoVotes} Required={incomingState.RequiredYesVotes} preservePostMatch={preservePostMatch} welcomePending={welcomePendingAcknowledgement}");

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
                    DraftUIPlugin.Log($"[CLIENT] [VOTE][DEBUG] Vote overlay suppressed by post-match lock. {DescribePostMatchLockState()}");
                }

                RefreshUi(forceRefresh: true);
                DraftUIPlugin.Log($"[CLIENT] [VOTE][DEBUG] Vote processing complete. resultingUi={currentUiState} storedVoteVisible={(currentVoteState?.IsVisible ?? false)} postMatchLock={DescribePostMatchLockState()}");
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
                approvalStateReceivedAt = Time.unscaledTime;
                approvalInteractionActive = false;
                SyncApprovalPopupInteractionCursor();

                if (currentApprovalRequestState != null && currentApprovalRequestState.IsVisible)
                {
                    currentMatchResultState = MatchResultMessage.Hidden();
                    postMatchDismissed = false;
                }

                if (preservePostMatch)
                {
                    DraftUIPlugin.Log("Ignoring approval overlay while post-match results are visible.");
                }

                LogCursorState("approval-state-received");
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
                var incomingSignature = BuildMatchResultSignature(incomingState);
                if (incomingState.IsVisible
                    && postMatchDismissed
                    && !string.IsNullOrWhiteSpace(lastDismissedPostMatchSignature)
                    && string.Equals(incomingSignature, lastDismissedPostMatchSignature, StringComparison.Ordinal))
                {
                    DraftUIPlugin.Log("[CLIENT] Ignoring stale visible post-match rebroadcast after local dismiss.");
                    return;
                }

                currentMatchResultState = incomingState;

                if (incomingState.IsVisible)
                {
                    cachedMatchResultState = incomingState;
                    lastVisibleMatchResultReceivedAt = Time.unscaledTime;
                    hasLoggedPostMatchFallback = false;
                    postMatchDismissed = false;
                    lastDismissedPostMatchSignature = null;
                    welcomePendingAcknowledgement = false;
                    welcomeRequestedAt = -1f;
                    hasLoggedWelcomeFallback = false;
                }
                else
                {
                    cachedMatchResultState = MatchResultMessage.Hidden();
                    lastVisibleMatchResultReceivedAt = -1f;
                    hasLoggedPostMatchFallback = false;
                    postMatchDismissed = true;
                    lastDismissedPostMatchSignature = null;
                    DraftUIPlugin.Log($"[CLIENT] [POST_MATCH][DEBUG] Hidden post-match payload received. Clearing post-match lock. currentUi={currentUiState}");
                }

                currentVoteState = VoteOverlayStateMessage.Hidden();
                currentApprovalRequestState = ApprovalRequestStateMessage.Hidden();
                approvalInteractionActive = false;
                currentDraftState = DraftOverlayStateMessage.Hidden();
                currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
                suppressVoteUntilHidden = false;
                localVoteAccepted = false;
                localVoteRejected = false;

                DraftUIPlugin.Log($"[CLIENT] Post-match payload received. Visible={currentMatchResultState.IsVisible} Winner={currentMatchResultState.WinningTeam} publicPresentation={currentMatchResultState.UsePublicPresentation} Players={(currentMatchResultState.Players?.Length ?? 0)}");
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

        private static void OnDiscordOnboardingStateReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var message = RankedOverlayNetcode.ReadJson<DiscordOnboardingStateMessage>(ref reader) ?? DiscordOnboardingStateMessage.Unresolved();
                var wasVerificationModalOpen = DiscordOnboardingUIRenderer.IsVerificationModalOpen(view);
                var wasOnboardingVisible = DiscordOnboardingUIRenderer.IsOnboardingVisible(view);
                publicServerModeActive = message.IsPublicServer;
                trainingServerModeActive = message.IsTrainingServer;
                TrainingClientRuntime.SetTrainingServerMode(trainingServerModeActive);
                discordOnboardingStateResolved = message.IsResolved;
                discordOnboardingIsLinked = message.IsLinked;
                if (discordOnboardingStateResolved)
                {
                    discordOnboardingFallbackActive = false;
                }

                if (discordOnboardingIsLinked)
                {
                    discordOnboardingDismissed = true;
                }
                else if (discordOnboardingStateResolved)
                {
                    discordOnboardingDismissed = false;
                }

                DraftUIPlugin.Log($"[CLIENT] Discord onboarding state received. resolved={discordOnboardingStateResolved} linked={discordOnboardingIsLinked} publicServer={publicServerModeActive} trainingServer={trainingServerModeActive} currentUi={currentUiState}");
                if (IsOnboardingBypassedServerModeActive())
                {
                    DraftUIPlugin.Log($"[CLIENT] Non-competitive onboarding branch activated. mode={(trainingServerModeActive ? "training" : "public")}. Skipping competitive verification UI and routing to Welcome.");
                    if (ShouldAwaitWelcomeFlow() || currentUiState == OverlayUiState.DiscordOnboarding || wasOnboardingVisible || wasVerificationModalOpen)
                    {
                        ActivatePublicWelcomeFlow($"{(trainingServerModeActive ? "Training" : "Public")} server onboarding auto-closed into Welcome");
                        return;
                    }
                }
                else if (discordOnboardingIsLinked)
                {
                    DraftUIPlugin.Log("[CLIENT][VERIFY] Verification success callback reached from backend linked=true update.");
                    DiscordOnboardingUIRenderer.HideOnboarding(view);
                    DraftUIPlugin.Log($"[CLIENT] Discord onboarding skipped/closed because backend confirmed linked state. onboardingVisibleBefore={wasOnboardingVisible} verificationModalBefore={wasVerificationModalOpen}");

                    if (ShouldAwaitWelcomeFlow() || currentUiState == OverlayUiState.DiscordOnboarding || wasOnboardingVisible || wasVerificationModalOpen)
                    {
                        DraftUIPlugin.Log("[CLIENT] Discord onboarding success close branch triggered. closingMainOnboarding=True closingCodePopup=True");
                        CompleteDiscordVerificationFlow("Discord onboarding auto-closed after successful verification");
                        DraftUIPlugin.Log($"[CLIENT] Discord onboarding success close completed. onboardingVisibleNow={DiscordOnboardingUIRenderer.IsOnboardingVisible(view)} verificationModalNow={DiscordOnboardingUIRenderer.IsVerificationModalOpen(view)} currentUi={currentUiState}");
                        return;
                    }
                }
                else if (discordOnboardingStateResolved)
                {
                    DraftUIPlugin.Log("[CLIENT] Discord onboarding remains eligible because backend confirmed unlinked state.");
                }

                RefreshUi(forceRefresh: true);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive Discord onboarding state: {ex}");
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

        private static void OnExternalUrlOpenReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var message = RankedOverlayNetcode.ReadJson<OpenExternalUrlMessage>(ref reader) ?? new OpenExternalUrlMessage();
                OpenExternalUrl(message.Url, Constants.SPEEDHOSTING_PUCK_URL, "Server command");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive external URL request: {ex}");
            }
        }

        private static void OnTrainingOpenWorldPoseReceived(ulong senderClientId, FastBufferReader reader)
        {
            try
            {
                var message = RankedOverlayNetcode.ReadJson<TrainingOpenWorldPoseMessage>(ref reader) ?? new TrainingOpenWorldPoseMessage();
                var targetPosition = new Vector3(message.PositionX, message.PositionY, message.PositionZ);
                var targetRotation = Quaternion.Euler(message.RotationEulerX, message.RotationEulerY, message.RotationEulerZ);
                TrainingClientRuntime.SetAuthoritativeTrainingPose(message.IsOpenWorldActive, targetPosition, targetRotation, message.Reason);
                DraftUIPlugin.Log($"[CLIENT][TRAINING] Received authoritative training pose. active={(message.IsOpenWorldActive ? "yes" : "no")} pos={targetPosition} rot={targetRotation.eulerAngles} reason={message.Reason ?? string.Empty}");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to receive training pose sync: {ex}");
            }
        }

        private static void UpdateOverlay()
        {
            EnsureViewSetup();
            EnsureMessagingHandlers();
            FlushPendingDiscordLinkCommand();
            SyncInputState();
            HandleApprovalKeyboardShortcuts();
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
            RenderApprovalPopup(forceRefresh);

            if (nextState == OverlayUiState.None || nextState == OverlayUiState.TeamSelect)
            {
                SyncApprovalPopupInteractionCursor();
            }

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
                    case OverlayUiState.DiscordOnboarding:
                        DraftUIPlugin.Log($"[CLIENT][VERIFY] Mandatory verification popup shown. resolved={discordOnboardingStateResolved} linked={discordOnboardingIsLinked}");
                        DiscordOnboardingUIRenderer.Show(view);
                        PrepareVisibleTransition(previousState == OverlayUiState.None);
                        view?.Container?.BringToFront();
                        AcquireCursor(forceResetInputs: true);
                        EnforceGameplayUiPriority();
                        break;
                    case OverlayUiState.Welcome:
                        WelcomeUIRenderer.ApplyServerMode(view, publicServerModeActive, trainingServerModeActive);
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

            if (ShouldShowDraftUi())
            {
                if (currentVoteState != null && currentVoteState.IsVisible)
                {
                    DraftUIPlugin.Log($"[CLIENT] [VOTE] Vote overlay takes priority. welcomePending={welcomePendingAcknowledgement} suppressVoteUntilHidden={suppressVoteUntilHidden}");
                }

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

                if (ShouldAwaitDiscordOnboardingDecision())
                {
                    if (discordOnboardingDecisionRequestedAt < 0f)
                    {
                        discordOnboardingDecisionRequestedAt = Time.unscaledTime;
                        hasLoggedDiscordOnboardingFallback = false;
                        DraftUIPlugin.Log("Awaiting Discord onboarding state for the local player.");
                    }

                    return OverlayUiState.None;
                }

                discordOnboardingDecisionRequestedAt = -1f;
                hasLoggedDiscordOnboardingFallback = false;

                if (ShouldShowDiscordOnboarding())
                {
                    return view?.DiscordOnboarding?.Panel != null
                        ? OverlayUiState.DiscordOnboarding
                        : OverlayUiState.Welcome;
                }

                return view?.Welcome?.Panel != null
                    ? OverlayUiState.Welcome
                    : OverlayUiState.None;
            }

            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            discordOnboardingDecisionRequestedAt = -1f;
            hasLoggedDiscordOnboardingFallback = false;

            if (ShouldShowTeamSelectState())
            {
                return OverlayUiState.TeamSelect;
            }

            return OverlayUiState.None;
        }

        private static bool ShouldOwnCursor(OverlayUiState state)
        {
            return state == OverlayUiState.DiscordOnboarding
                || state == OverlayUiState.Welcome
                || state == OverlayUiState.Draft
                || state == OverlayUiState.PostMatch;
        }

        private static bool ShouldShowDraftUi()
        {
            return HasActiveDraftPayload()
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
            if (postMatchDismissed)
            {
                return false;
            }

            var hasVisiblePostMatchPayload = ShouldShowPostMatchUi() || HasCachedPostMatchResult();
            if (hasVisiblePostMatchPayload)
            {
                return true;
            }

            if (currentUiState == OverlayUiState.PostMatch)
            {
                DraftUIPlugin.Log($"[CLIENT] [POST_MATCH][DEBUG] Ignoring stale PostMatch UI state without visible payload. {DescribePostMatchLockState()}");
            }

            return false;
        }

        private static string DescribePostMatchLockState()
        {
            return $"dismissed={postMatchDismissed} currentVisible={(currentMatchResultState?.IsVisible ?? false)} cachedVisible={(cachedMatchResultState?.IsVisible ?? false)} currentUi={currentUiState}";
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

            DraftUIRenderer.ShowVoting(view);
        }

        private static void RenderDraftFlow(bool forceRefresh)
        {
            if (HasActiveDraftPayload())
            {
                RenderDraft(forceRefresh);
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
            var renderState = BuildRenderableVoteState(secondsRemainingPrecise);
            var signature = string.Join("|",
                renderState.Title ?? string.Empty,
                renderState.PromptText ?? string.Empty,
                renderState.InitiatorName ?? string.Empty,
                renderState.YesVotes,
                renderState.NoVotes,
                renderState.RequiredYesVotes,
                renderState.EligibleCount,
                renderState.VoteDurationSeconds,
                string.Join(",", (renderState.PlayerEntries ?? Array.Empty<VoteOverlayPlayerEntryMessage>()).Select(BuildVoteEntrySignature)),
                renderState.FooterText ?? string.Empty,
                localVoteAccepted,
                localVoteRejected);
            var contentChanged = forceRefresh || !string.Equals(signature, lastVoteRenderSignature, StringComparison.Ordinal);

            if (contentChanged)
            {
                lastVoteRenderSignature = signature;
            }

            DraftUIRenderer.RenderVoting(view, renderState, secondsRemainingPrecise, localVoteAccepted, localVoteRejected, contentChanged);
        }

        private static void RenderApproval(bool forceRefresh)
        {
            RenderApprovalPopup(forceRefresh);
        }

        private static void RenderApprovalPopup(bool forceRefresh)
        {
            if (view == null)
            {
                return;
            }

            if (currentApprovalRequestState == null || !currentApprovalRequestState.IsVisible)
            {
                lastApprovalRenderSignature = string.Empty;
                DraftUIRenderer.SetApprovalPopupVisible(view, false);
                return;
            }

            var secondsRemaining = Mathf.Max(0f, currentApprovalRequestState.SecondsRemaining - (Time.unscaledTime - approvalStateReceivedAt));
            var renderState = new ApprovalRequestStateMessage
            {
                IsVisible = currentApprovalRequestState.IsVisible,
                RequestId = currentApprovalRequestState.RequestId,
                ViewRole = currentApprovalRequestState.ViewRole,
                Status = currentApprovalRequestState.Status,
                Title = currentApprovalRequestState.Title,
                PlayerName = currentApprovalRequestState.PlayerName,
                PromptText = currentApprovalRequestState.PromptText,
                TargetTeamName = currentApprovalRequestState.TargetTeamName,
                PreviousTeamName = currentApprovalRequestState.PreviousTeamName,
                IsSwitchRequest = currentApprovalRequestState.IsSwitchRequest,
                FooterText = currentApprovalRequestState.FooterText,
                SecondsRemaining = secondsRemaining,
                QueuePosition = currentApprovalRequestState.QueuePosition,
                QueueLength = currentApprovalRequestState.QueueLength
            };

            var signature = string.Join("|",
                renderState.RequestId ?? string.Empty,
                renderState.ViewRole,
                renderState.Status,
                renderState.Title ?? string.Empty,
                renderState.PlayerName ?? string.Empty,
                renderState.PromptText ?? string.Empty,
                renderState.TargetTeamName ?? string.Empty,
                renderState.PreviousTeamName ?? string.Empty,
                renderState.IsSwitchRequest,
                renderState.FooterText ?? string.Empty,
                Mathf.CeilToInt(secondsRemaining),
                renderState.QueuePosition,
                renderState.QueueLength);

            if (!forceRefresh && string.Equals(signature, lastApprovalRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastApprovalRenderSignature = signature;
            DraftUIRenderer.SetApprovalPopupVisible(view, true);
            DraftUIRenderer.RenderApproval(view, renderState, false);
        }

        private static bool IsApprovalPopupVisible()
        {
            return currentApprovalRequestState != null && currentApprovalRequestState.IsVisible;
        }

        private static bool CanInteractWithApprovalPopup()
        {
            return IsApprovalPopupVisible()
                && currentApprovalRequestState.ViewRole == ApprovalRequestViewRole.CaptainDecision
                && currentApprovalRequestState.Status == ApprovalRequestDisplayStatus.Pending;
        }

        private static bool IsApprovalPopupCaptainPending()
        {
            return CanInteractWithApprovalPopup();
        }

        private static bool IsSelectionUiVisible()
        {
            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    return false;
                }

                return (uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible)
                    || (uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsNativeMouseOwnedByOtherUi()
        {
            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager || !uiManager.isMouseActive)
                {
                    return false;
                }

                if (IsSelectionUiVisible())
                {
                    return false;
                }

                return !UIInputState.isScoreboardOpen
                    && !ShouldOwnCursor(currentUiState)
                    && !UIInputState.isApprovalPopupInteractionOpen;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanBeginApprovalPopupInteraction()
        {
            return false;
        }

        private static void HandleApprovalKeyboardShortcuts()
        {
            if (!CanUseApprovalKeyboardShortcuts())
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                OnApprovalAcceptedClicked();
                return;
            }

            if (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
            {
                OnApprovalRejectedClicked();
            }
        }

        private static bool CanUseApprovalKeyboardShortcuts()
        {
            return CanInteractWithApprovalPopup()
                && currentUiState == OverlayUiState.None
                && !IsSelectionUiVisible();
        }

        private static bool ShouldApprovalPopupOwnCursor()
        {
            return approvalInteractionActive && CanBeginApprovalPopupInteraction();
        }

        private static void SyncApprovalPopupInteractionCursor()
        {
            if (currentUiState == OverlayUiState.DiscordOnboarding
                || currentUiState == OverlayUiState.Welcome
                || currentUiState == OverlayUiState.Draft
                || currentUiState == OverlayUiState.PostMatch)
            {
                if (UIInputState.isApprovalPopupInteractionOpen)
                {
                    UIInputState.isApprovalPopupInteractionOpen = false;
                    ReleaseCursor();
                }

                LogCursorState("approval-overlay-blocked");
                return;
            }

            var shouldOwnCursor = ShouldApprovalPopupOwnCursor();
            var hadOverride = UIInputState.isApprovalPopupInteractionOpen;
            UIInputState.isApprovalPopupInteractionOpen = shouldOwnCursor;
            if (shouldOwnCursor)
            {
                AcquireCursor(forceResetInputs: !hadOverride);
            }
            else if (hadOverride)
            {
                ReleaseCursor();
            }

            LogCursorState("approval-sync");
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
            DraftOverlayStateMessage baseState;
            if (currentDraftState != null && currentDraftState.IsVisible)
            {
                baseState = currentDraftState;
            }
            else if (currentDraftExtendedState != null && currentDraftExtendedState.IsVisible)
            {
                baseState = CreateFallbackDraftStateFromExtended();
            }
            else
            {
                baseState = currentDraftState ?? DraftOverlayStateMessage.Hidden();
            }

            return BuildRenderableDraftState(baseState, currentDraftExtendedState);
        }

        private static DraftOverlayStateMessage CreateFallbackDraftStateFromExtended()
        {
            return new DraftOverlayStateMessage
            {
                IsVisible = currentDraftExtendedState != null && currentDraftExtendedState.IsVisible,
                IsCompleted = false,
                Title = string.IsNullOrWhiteSpace(currentDraftState?.Title) ? "CAPTAIN DRAFT" : currentDraftState.Title,
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
                    ? "Draft data received. Waiting for the full team view..."
                    : currentDraftState.FooterText
            };
        }

        private static VoteOverlayStateMessage BuildRenderableVoteState(float secondsRemainingPrecise)
        {
            var state = currentVoteState ?? VoteOverlayStateMessage.Hidden();
            if (!state.IsVisible)
            {
                return state;
            }

            var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemainingPrecise));
            var remainingYesVotes = Mathf.Max(0, state.RequiredYesVotes - state.YesVotes);
            var promptText = localVoteAccepted
                ? "Your vote is locked in as YES. Waiting for the rest of the lobby."
                : localVoteRejected
                    ? "Your vote is locked in as NO. Waiting for the rest of the lobby."
                    : "Vote now with /y or /n, or use the buttons below.";
            var footerText = localVoteAccepted || localVoteRejected
                ? $"{remainingSeconds}s left. Need {remainingYesVotes} more yes vote{(remainingYesVotes == 1 ? string.Empty : "s")} to start."
                : $"Action: /y starts ranked, /n stops it. {remainingSeconds}s left.";

            return new VoteOverlayStateMessage
            {
                IsVisible = state.IsVisible,
                Title = "RANKED MATCH STARTING VOTE",
                PromptText = promptText,
                InitiatorName = state.InitiatorName,
                SecondsRemaining = state.SecondsRemaining,
                SecondsRemainingPrecise = state.SecondsRemainingPrecise,
                VoteDurationSeconds = state.VoteDurationSeconds,
                EligibleCount = state.EligibleCount,
                YesVotes = state.YesVotes,
                NoVotes = state.NoVotes,
                RequiredYesVotes = state.RequiredYesVotes,
                FooterText = footerText,
                PlayerEntries = state.PlayerEntries ?? Array.Empty<VoteOverlayPlayerEntryMessage>()
            };
        }

        private static DraftOverlayStateMessage BuildRenderableDraftState(DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState)
        {
            state = state ?? DraftOverlayStateMessage.Hidden();
            ResolveDraftTurnContext(state, extendedState, out var isLocalTurn, out _);

            return new DraftOverlayStateMessage
            {
                IsVisible = state.IsVisible,
                IsCompleted = state.IsCompleted,
                Title = state.IsCompleted ? "TEAMS LOCKED" : "CAPTAIN DRAFT",
                RedCaptainName = state.RedCaptainName,
                BlueCaptainName = state.BlueCaptainName,
                CurrentTurnName = state.CurrentTurnName,
                CurrentTurnClientId = state.CurrentTurnClientId,
                CurrentTurnSteamId = state.CurrentTurnSteamId,
                AvailablePlayers = state.AvailablePlayers ?? Array.Empty<string>(),
                RedPlayers = state.RedPlayers ?? Array.Empty<string>(),
                BluePlayers = state.BluePlayers ?? Array.Empty<string>(),
                PendingLateJoinerCount = state.PendingLateJoinerCount,
                PendingLateJoiners = state.PendingLateJoiners ?? Array.Empty<string>(),
                DummyModeActive = state.DummyModeActive,
                FooterText = BuildDraftFooterText(state, extendedState, isLocalTurn)
            };
        }

        private static string BuildDraftFooterText(DraftOverlayStateMessage state, DraftOverlayExtendedMessage extendedState, bool isLocalTurn)
        {
            if (state == null)
            {
                return string.Empty;
            }

            if (state.IsCompleted)
            {
                return "Final teams are set. Match start is next.";
            }

            var role = ResolveLocalDraftRoleText(extendedState, isLocalTurn);
            var footerParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(role))
            {
                footerParts.Add(role);
            }

            if (isLocalTurn)
            {
                footerParts.Add("Action: pick 1 player from AVAILABLE PICKS.");
            }
            else if (!string.IsNullOrWhiteSpace(state.CurrentTurnName))
            {
                footerParts.Add($"Current captain: {NormalizeDraftName(state.CurrentTurnName)}.");
            }
            else
            {
                footerParts.Add("Waiting for the next captain turn.");
            }

            var pendingCount = Mathf.Max(state.PendingLateJoinerCount, extendedState?.PendingLateJoinerEntries?.Length ?? 0);
            if (pendingCount > 0)
            {
                footerParts.Add($"Late join requests: {pendingCount}.");
            }

            return string.Join("  ", footerParts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string ResolveLocalDraftRoleText(DraftOverlayExtendedMessage extendedState, bool isLocalTurn)
        {
            if (TryFindLocalDraftEntry(extendedState?.RedPlayerEntries, out var redEntry))
            {
                return redEntry.IsCaptain ? "Role: Red captain" : "Role: Red team player";
            }

            if (TryFindLocalDraftEntry(extendedState?.BluePlayerEntries, out var blueEntry))
            {
                return blueEntry.IsCaptain ? "Role: Blue captain" : "Role: Blue team player";
            }

            if (TryFindLocalDraftEntry(extendedState?.PendingLateJoinerEntries, out _))
            {
                return "Role: Waiting for captain approval";
            }

            if (TryFindLocalDraftEntry(extendedState?.AvailablePlayerEntries, out _))
            {
                return isLocalTurn ? "Role: Captain on the clock" : "Role: Waiting in the draft pool";
            }

            return isLocalTurn ? "Role: Captain on the clock" : "Role: Waiting for captains";
        }

        private static bool TryFindLocalDraftEntry(IEnumerable<DraftOverlayPlayerEntryMessage> entries, out DraftOverlayPlayerEntryMessage localEntry)
        {
            localEntry = null;
            if (entries == null)
            {
                return false;
            }

            var localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0UL;
            string localSteamId = null;
            if (TryGetLocalPlayer(out var localPlayer))
            {
                localSteamId = localPlayer.SteamId.Value.ToString();
            }

            localEntry = entries.FirstOrDefault(entry =>
                entry != null
                && ((localClientId != 0 && entry.ClientId != 0 && entry.ClientId == localClientId)
                    || (!string.IsNullOrWhiteSpace(localSteamId)
                        && !string.IsNullOrWhiteSpace(entry.SteamId)
                        && string.Equals(entry.SteamId, localSteamId, StringComparison.Ordinal))));
            return localEntry != null;
        }

        private static string NormalizeDraftName(string displayName)
        {
            return string.IsNullOrWhiteSpace(displayName) ? "Captain" : displayName.Trim();
        }

        private static void LogOverlayTick(OverlayUiState nextState)
        {
            if (!EnableOverlayHotPathDiagnostics)
            {
                return;
            }

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
                    player?.PlayerNumber ?? 0,
                    player?.IsSharedGoalie ?? false,
                    player?.ExcludedFromMmr ?? false,
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
                currentMatchResultState.UsePublicPresentation,
                playerSignature);

            if (!forceRefresh && string.Equals(signature, lastPostMatchRenderSignature, StringComparison.Ordinal))
            {
                return;
            }

            lastPostMatchRenderSignature = signature;
            DraftUIPlugin.Log($"[CLIENT] POST_MATCH render requested. Winner={currentMatchResultState.WinningTeam} publicPresentation={currentMatchResultState.UsePublicPresentation} Players={(currentMatchResultState.Players?.Length ?? 0)}");
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
                approvalInteractionActive = false;
                SyncApprovalPopupInteractionCursor();
                SendChatCommand($"/approve {currentApprovalRequestState.RequestId}");
                RefreshUi(forceRefresh: true);
            }
        }

        private static void OnApprovalRejectedClicked()
        {
            if (!string.IsNullOrWhiteSpace(currentApprovalRequestState?.RequestId))
            {
                approvalInteractionActive = false;
                SyncApprovalPopupInteractionCursor();
                SendChatCommand($"/reject {currentApprovalRequestState.RequestId}");
                RefreshUi(forceRefresh: true);
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

        private static void OnDiscordOnboardingVerifySubmitted(string code)
        {
            var trimmedCode = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim();
            if (string.IsNullOrWhiteSpace(trimmedCode))
            {
                return;
            }

            DraftUIPlugin.Log("Discord onboarding Verify submitted");
            SendChatCommand($"/link {trimmedCode}", queueIfUnavailable: true);
        }

        private static void OnDiscordOnboardingJoinClicked()
        {
            OpenExternalUrl(Constants.DISCORD_INVITE_URL, Constants.DISCORD_INVITE_URL, "Discord onboarding Join button");
        }

        private static void OnDiscordOnboardingLeaveClicked()
        {
            DraftUIPlugin.Log("[CLIENT][VERIFY] Mandatory verification leave selected from onboarding panel.");
            DisconnectFromMandatoryVerification("onboarding-leave", "Leave Server button");
        }

        private static void OnDiscordOnboardingCloseVerificationClicked()
        {
            DraftUIPlugin.Log("[CLIENT][VERIFY] Mandatory verification close selected from verification modal.");
            DisconnectFromMandatoryVerification("verification-modal-close", "Verification modal Leave Server button");
        }

        private static void OnWelcomeDiscordClicked()
        {
            OpenExternalUrl(Constants.DISCORD_INVITE_URL, Constants.DISCORD_INVITE_URL, "Welcome Discord button");
        }

        private static void OnWelcomeHostClicked()
        {
            OpenExternalUrl(Constants.BuildPuckLandingUrl(Constants.HOST_SOURCE_WELCOME), Constants.SPEEDHOSTING_PUCK_URL, "Welcome Host button");
        }

        private static void OnWelcomeContinueClicked()
        {
            DismissWelcomeScreen("Welcome UI Continued");
        }

        private static void OpenDiscordInvite(string url, string sourceLabel)
        {
            OpenExternalUrl(url, Constants.DISCORD_INVITE_URL, sourceLabel);
        }

        private static void OpenExternalUrl(string url, string fallbackUrl, string sourceLabel)
        {
            try
            {
                var targetUrl = string.IsNullOrWhiteSpace(url) ? fallbackUrl : url;
                Application.OpenURL(targetUrl);
                DraftUIPlugin.Log($"External URL opened via {sourceLabel}: {targetUrl}");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to open external URL from {sourceLabel}: {ex}");
            }
        }

        private static void OnPostMatchContinueClicked()
        {
            DismissPostMatch("Post-match UI Continued");
        }

        private static void OnPostMatchHostClicked()
        {
            OpenExternalUrl(Constants.BuildPuckLandingUrl(Constants.HOST_SOURCE_POSTMATCH), Constants.SPEEDHOSTING_PUCK_URL, "Post-match Host button");
        }

        private static void OnPostMatchCloseClicked()
        {
            DismissPostMatch("Post-match UI Closed");
        }

        private static void FlushPendingDiscordLinkCommand()
        {
            if (string.IsNullOrWhiteSpace(pendingDiscordLinkCommand))
            {
                return;
            }

            if (!TrySendChatCommand(pendingDiscordLinkCommand))
            {
                if (!hasLoggedPendingDiscordLinkWait && pendingDiscordLinkQueuedAt >= 0f)
                {
                    var elapsed = Time.unscaledTime - Mathf.Max(0f, pendingDiscordLinkQueuedAt);
                    if (elapsed >= 0.5f)
                    {
                        hasLoggedPendingDiscordLinkWait = true;
                        DraftUIPlugin.Log($"Discord onboarding is still waiting for chat readiness before sending queued command after {elapsed:0.00}s.");
                    }
                }

                return;
            }

            DraftUIPlugin.Log($"Discord onboarding queued command sent: {pendingDiscordLinkCommand}");
            pendingDiscordLinkCommand = null;
            pendingDiscordLinkQueuedAt = -1f;
            hasLoggedPendingDiscordLinkWait = false;
        }

        private static bool TrySendChatCommand(string message)
        {
            var chat = UIChat.Instance;
            if (!chat || uiChatSendMessageMethod == null)
            {
                return false;
            }

            uiChatSendMessageMethod.Invoke(chat, new object[] { message, false });
            return true;
        }

        private static void SendChatCommand(string message, bool queueIfUnavailable = false)
        {
            try
            {
                if (TrySendChatCommand(message))
                {
                    if (queueIfUnavailable)
                    {
                        pendingDiscordLinkCommand = null;
                        pendingDiscordLinkQueuedAt = -1f;
                        hasLoggedPendingDiscordLinkWait = false;
                    }

                    return;
                }

                if (queueIfUnavailable)
                {
                    pendingDiscordLinkCommand = message;
                    pendingDiscordLinkQueuedAt = Time.unscaledTime;
                    hasLoggedPendingDiscordLinkWait = false;
                    DraftUIPlugin.Log($"Discord onboarding queued command until chat is ready: {message}");
                    return;
                }

                DraftUIPlugin.LogError($"Unable to send UI command '{message}' because chat is not ready.");
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
                || currentUiState == OverlayUiState.Welcome
                || currentUiState == OverlayUiState.DiscordOnboarding
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

        private static bool ShouldAwaitDiscordOnboardingDecision()
        {
            return ShouldAwaitWelcomeFlow()
                && !IsOnboardingBypassedServerModeActive()
                && !discordOnboardingStateResolved
                && !discordOnboardingFallbackActive
                && !discordOnboardingDismissed;
        }

        private static bool ShouldShowDiscordOnboarding()
        {
            return ShouldAwaitWelcomeFlow()
                && !IsOnboardingBypassedServerModeActive()
                && discordOnboardingStateResolved
                && !discordOnboardingIsLinked
                && !discordOnboardingDismissed;
        }

        private static bool IsOnboardingBypassedServerModeActive()
        {
            return publicServerModeActive || trainingServerModeActive;
        }

        private static bool ShouldAwaitWelcomeFlow()
        {
            return welcomePendingAcknowledgement && HasLocalPlayerContext();
        }

        private static bool ShouldShowWelcomeScreen()
        {
            return ShouldAwaitWelcomeFlow()
                && !ShouldAwaitDiscordOnboardingDecision()
                && !ShouldShowDiscordOnboarding()
                && view?.Welcome?.Panel != null;
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
            return ShouldSuppressGameplaySelectionUi() && ShouldBlockGameplayState(state);
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

        private static void SendDiscordVerificationDeclined(string action)
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

                var message = new VerificationDeclinedMessage
                {
                    Action = string.IsNullOrWhiteSpace(action) ? "unknown" : action.Trim()
                };
                var capacity = RankedOverlayNetcode.EstimateCapacity(message);
                var writer = new FastBufferWriter(capacity, Allocator.Temp);
                try
                {
                    RankedOverlayNetcode.WriteJson(ref writer, message);
                    messagingManager.SendNamedMessage(RankedOverlayChannels.DiscordVerificationDeclined, NetworkManager.ServerClientId, writer);
                }
                finally
                {
                    writer.Dispose();
                }
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to notify server about mandatory verification refusal: {ex}");
            }
        }

        private static void DisconnectFromMandatoryVerification(string action, string sourceLabel)
        {
            var wasOnboardingVisible = DiscordOnboardingUIRenderer.IsOnboardingVisible(view);
            var wasVerificationModalOpen = DiscordOnboardingUIRenderer.IsVerificationModalOpen(view);
            DraftUIPlugin.Log($"[CLIENT][VERIFY] Leave button pressed. source={sourceLabel} action={action} resolved={discordOnboardingStateResolved} linked={discordOnboardingIsLinked} currentUi={currentUiState} onboardingVisible={wasOnboardingVisible} verificationModalVisible={wasVerificationModalOpen}");

            try
            {
                SendDiscordVerificationDeclined(action);
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"[CLIENT][VERIFY] Failed to send best-effort server verification decline before local disconnect: {ex}");
            }

            discordOnboardingDismissed = true;
            discordOnboardingFallbackActive = false;
            discordOnboardingDecisionRequestedAt = -1f;
            hasLoggedDiscordOnboardingFallback = false;
            DiscordOnboardingUIRenderer.HideOnboarding(view);

            if (view?.Container != null)
            {
                DraftUIRenderer.ShowHidden(view);
            }

            currentUiOpacity = 0f;
            targetUiOpacity = 0f;
            pendingHideAfterFade = false;
            UIInputState.isDraftUIOpen = false;
            ReleaseCursor();

            try
            {
                var networkManager = NetworkManager.Singleton;
                if (networkManager != null && networkManager.IsClient)
                {
                    DraftUIPlugin.Log($"[CLIENT][VERIFY] Disconnect path executed via native client shutdown. action={action} source={sourceLabel}");
                    networkManager.Shutdown(discardMessageQueue: true);
                    return;
                }

                DraftUIPlugin.LogError($"[CLIENT][VERIFY] Native disconnect path unavailable. action={action} source={sourceLabel} hasNetworkManager={(networkManager != null)} isClient={(networkManager != null && networkManager.IsClient)}");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"[CLIENT][VERIFY] Native disconnect path failed. action={action} source={sourceLabel} error={ex}");
            }
        }

        private static void DismissPostMatch(string actionLabel)
        {
            lastDismissedPostMatchSignature = BuildMatchResultSignature(currentMatchResultState != null && currentMatchResultState.IsVisible
                ? currentMatchResultState
                : cachedMatchResultState);
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

        private static string BuildMatchResultSignature(MatchResultMessage state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            var playerSignature = string.Join("|", (state.Players ?? Array.Empty<MatchResultPlayerMessage>())
                .Select(player => string.Join("~",
                    player?.Id ?? string.Empty,
                    player?.Username ?? string.Empty,
                    player?.PlayerNumber ?? 0,
                    player?.IsSharedGoalie ?? false,
                    player?.ExcludedFromMmr ?? false,
                    player?.Team ?? TeamResult.Unknown,
                    player?.Goals ?? 0,
                    player?.Assists ?? 0,
                    player?.Saves ?? 0,
                    player?.Shots ?? 0,
                    player?.MmrBefore ?? 0,
                    player?.MmrAfter ?? 0,
                    player?.MmrDelta ?? 0,
                    player?.IsMVP ?? false)));

            return string.Join("|",
                state.IsVisible,
                state.WinningTeam,
                state.UsePublicPresentation,
                playerSignature);
        }

        private static void DismissWelcomeScreen(string actionLabel)
        {
            var localStateBeforeDismiss = GetLocalPlayerStateName();
            welcomePendingAcknowledgement = false;
            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            DraftUIPlugin.Log($"{actionLabel}. localStateBeforeDismiss={localStateBeforeDismiss} teamSelectVisible={IsSelectionUiVisible()}");
            TryRequestLocalPlayerStateTransition("TeamSelect", "welcome-continue");
            ApplyUiState(OverlayUiState.TeamSelect, forceRefresh: true);
            RestoreSuppressedGameplayUi();
            RefreshUi(forceRefresh: true);
        }

        private static void ActivatePublicWelcomeFlow(string actionLabel)
        {
            var wasOnboardingVisible = DiscordOnboardingUIRenderer.IsOnboardingVisible(view);
            var wasVerificationModalOpen = DiscordOnboardingUIRenderer.IsVerificationModalOpen(view);
            welcomePendingAcknowledgement = true;
            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            discordOnboardingDismissed = true;
            discordOnboardingFallbackActive = false;
            discordOnboardingDecisionRequestedAt = -1f;
            hasLoggedDiscordOnboardingFallback = false;
            pendingDiscordLinkCommand = null;
            pendingDiscordLinkQueuedAt = -1f;
            hasLoggedPendingDiscordLinkWait = false;
            DiscordOnboardingUIRenderer.HideOnboarding(view);
            DraftUIPlugin.Log($"[CLIENT][MODE] Welcome path executed. action={actionLabel} onboardingVisibleBefore={wasOnboardingVisible} verificationModalBefore={wasVerificationModalOpen} welcomePending={welcomePendingAcknowledgement}");
            ApplyUiState(OverlayUiState.Welcome, forceRefresh: true);
            EnforceGameplayUiPriority();
            RefreshUi(forceRefresh: true);
        }

        private static bool TryHandleLocalTrainingChatCommand(string message)
        {
            return false;
        }

        private static void CompleteDiscordVerificationFlow(string actionLabel)
        {
            var wasOnboardingVisible = DiscordOnboardingUIRenderer.IsOnboardingVisible(view);
            var wasVerificationModalOpen = DiscordOnboardingUIRenderer.IsVerificationModalOpen(view);
            welcomePendingAcknowledgement = true;
            welcomeRequestedAt = -1f;
            hasLoggedWelcomeFallback = false;
            discordOnboardingDismissed = true;
            discordOnboardingFallbackActive = false;
            discordOnboardingDecisionRequestedAt = -1f;
            hasLoggedDiscordOnboardingFallback = false;
            pendingDiscordLinkCommand = null;
            pendingDiscordLinkQueuedAt = -1f;
            hasLoggedPendingDiscordLinkWait = false;
            DiscordOnboardingUIRenderer.HideOnboarding(view);
            DraftUIPlugin.Log($"[CLIENT][VERIFY] Popup close path executed. action={actionLabel} linked={discordOnboardingIsLinked} resolved={discordOnboardingStateResolved} onboardingVisibleBefore={wasOnboardingVisible} verificationModalBefore={wasVerificationModalOpen} welcomePending={welcomePendingAcknowledgement}");
            ApplyUiState(OverlayUiState.Welcome, forceRefresh: true);
            EnforceGameplayUiPriority();
            RefreshUi(forceRefresh: true);
            DraftUIPlugin.Log($"[CLIENT][VERIFY] Verification success close finished. onboardingVisibleNow={DiscordOnboardingUIRenderer.IsOnboardingVisible(view)} verificationModalNow={DiscordOnboardingUIRenderer.IsVerificationModalOpen(view)} currentUi={currentUiState} welcomePending={welcomePendingAcknowledgement} dismissed={discordOnboardingDismissed}");
        }

        private static void DismissDiscordOnboarding(string actionLabel)
        {
            var wasOnboardingVisible = DiscordOnboardingUIRenderer.IsOnboardingVisible(view);
            var wasVerificationModalOpen = DiscordOnboardingUIRenderer.IsVerificationModalOpen(view);
            discordOnboardingDismissed = true;
            discordOnboardingFallbackActive = false;
            discordOnboardingDecisionRequestedAt = -1f;
            hasLoggedDiscordOnboardingFallback = false;
            DiscordOnboardingUIRenderer.HideOnboarding(view);
            DraftUIPlugin.Log($"{actionLabel}. onboardingVisibleBefore={wasOnboardingVisible} verificationModalBefore={wasVerificationModalOpen}");
            ApplyUiState(OverlayUiState.Welcome, forceRefresh: true);
            EnforceGameplayUiPriority();
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
            UpdateDiscordOnboardingDecisionFailsafe();
            UpdateWelcomeFailsafe();
            UpdatePostMatchFailsafe();
        }

        private static void UpdateDiscordOnboardingDecisionFailsafe()
        {
            if (!ShouldAwaitDiscordOnboardingDecision() || discordOnboardingDecisionRequestedAt < 0f)
            {
                return;
            }

            var elapsed = Time.unscaledTime - Mathf.Max(0f, discordOnboardingDecisionRequestedAt);
            if (elapsed < DiscordOnboardingDecisionFailsafeDelay)
            {
                return;
            }

            if (!hasLoggedDiscordOnboardingFallback)
            {
                hasLoggedDiscordOnboardingFallback = true;
                DraftUIPlugin.LogError($"Discord onboarding state did not resolve after {elapsed:0.00}s. Keeping mandatory verification gate active.");
            }
        }

        private static void UpdateWelcomeFailsafe()
        {
            if (!ShouldAwaitWelcomeFlow() || welcomeRequestedAt < 0f)
            {
                return;
            }

            if (IsWelcomeFlowOverlayVisible())
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
            TryRequestLocalPlayerStateTransition("TeamSelect", "failsafe-recovery");
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
            return IsWelcomeFlowOverlayVisible() || IsDraftOverlayVisible() || IsPostMatchOverlayVisible();
        }

        private static bool IsWelcomeFlowOverlayVisible()
        {
            return IsDiscordOnboardingOverlayVisible() || IsWelcomeOverlayVisible();
        }

        private static bool IsDiscordOnboardingOverlayVisible()
        {
            return currentUiState == OverlayUiState.DiscordOnboarding
                && view?.Container != null
                && view.Container.style.display == DisplayStyle.Flex
                && view?.DiscordOnboarding?.Panel != null
                && view.DiscordOnboarding.Panel.style.display == DisplayStyle.Flex;
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

        private static bool TryRequestLocalPlayerStateTransition(string targetStateName, string source)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetStateName) || playerSetStateRpcMethod == null || !TryGetLocalPlayer(out var localPlayer) || !localPlayer)
                {
                    DraftUIPlugin.LogError($"[CLIENT][JOIN] Could not request local player state transition. source={source} targetState={targetStateName ?? "null"} hasMethod={(playerSetStateRpcMethod != null)}");
                    return false;
                }

                var currentStateName = localPlayer.State.Value.ToString() ?? string.Empty;
                var stateType = localPlayer.State.Value.GetType();
                var targetState = Enum.Parse(stateType, targetStateName, true);
                DraftUIPlugin.Log($"[CLIENT][JOIN] Requesting native player state transition. source={source} currentState={currentStateName} targetState={targetStateName}");
                playerSetStateRpcMethod.Invoke(localPlayer, new object[] { targetState, 0f });
                return true;
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to request local player state transition. source={source} targetState={targetStateName}: {ex}");
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
                LogCursorState("acquire-skipped-hidden");
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
                    LogCursorState("acquire-skipped-already-unlocked");
                    return;
                }

                uiManagerShowMouseMethod?.Invoke(uiManager, null);
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                if (forceResetInputs)
                {
                    ResetLocalInputs();
                }
                UIInputState.Sync(uiManager);
                UIInputState.isCursorLocked = false;
                LogCursorState("cursor-acquired");
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
                LogCursorState("release-skipped-visible-owner");
                return;
            }

            try
            {
                var uiManager = UIManager.Instance;
                if (!uiManager)
                {
                    UIInputState.isCursorLocked = true;
                    LogCursorState("release-no-ui-manager");
                    return;
                }

                UIInputState.Sync(uiManager);
                if (UIInputState.isCursorLocked)
                {
                    LogCursorState("release-noop-already-native");
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
                LogCursorState("cursor-released");
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
                    AcquireCursor(forceResetInputs: false);
                    return;
                }

                LogCursorState("cursor-consistency");
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

            var approvalCursorOverrideActive = approvalInteractionActive && UIInputState.isApprovalPopupInteractionOpen;

            if (UIInputState.isDraftUIOpen || UIInputState.isApprovalPopupInteractionOpen)
            {
                AcquireCursor(forceResetInputs: !approvalCursorOverrideActive);
            }
            else
            {
                ReleaseCursor();
            }

            LogCursorState("scoreboard-closed");
        }

        private static string DescribeCursorContext()
        {
            try
            {
                var uiManager = UIManager.Instance;
                var teamSelectVisible = uiManager != null && uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible;
                var positionSelectVisible = uiManager != null && uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible;
                var nativeMouseActive = uiManager != null && uiManager.isMouseActive;

                if (UIInputState.isApprovalPopupInteractionOpen)
                {
                    return "approval-popup-interaction";
                }

                if (teamSelectVisible || positionSelectVisible)
                {
                    return "team-select";
                }

                if (nativeMouseActive && !UIInputState.isScoreboardOpen && !ShouldOwnCursor(currentUiState))
                {
                    return "chat-open-or-native-ui";
                }

                return "gameplay";
            }
            catch
            {
                return "unknown";
            }
        }

        private static void LogCursorState(string reason)
        {
            if (!EnableOverlayHotPathDiagnostics)
            {
                return;
            }

            try
            {
                var uiManager = UIManager.Instance;
                var teamSelectVisible = uiManager != null && uiManager.TeamSelect != null && uiManager.TeamSelect.IsVisible;
                var positionSelectVisible = uiManager != null && uiManager.PositionSelect != null && uiManager.PositionSelect.IsVisible;
                var nativeMouseActive = uiManager != null && uiManager.isMouseActive;
                var popupVisible = currentApprovalRequestState != null && currentApprovalRequestState.IsVisible;
                var captainPending = IsApprovalPopupCaptainPending();
                var scoreboardSuppressed = approvalInteractionActive && CanBeginApprovalPopupInteraction();
                var signature = string.Join("|", new[]
                {
                    $"popup={popupVisible}",
                    $"captainPending={captainPending}",
                    $"tabHeld={approvalInteractionActive}",
                    $"approvalOverride={UIInputState.isApprovalPopupInteractionOpen}",
                    $"scoreboardSuppressed={scoreboardSuppressed}",
                    $"draftCursor={UIInputState.isDraftUIOpen}",
                    $"scoreboardOpen={UIInputState.isScoreboardOpen}",
                    $"teamSelect={teamSelectVisible}",
                    $"positionSelect={positionSelectVisible}",
                    $"nativeMouseActive={nativeMouseActive}",
                    $"cursorVisible={UnityEngine.Cursor.visible}",
                    $"cursorLock={UnityEngine.Cursor.lockState}",
                    $"context={DescribeCursorContext()}",
                    $"ui={currentUiState}"
                });

                if (string.Equals(signature, lastCursorDebugSignature, StringComparison.Ordinal))
                {
                    return;
                }

                lastCursorDebugSignature = signature;
                DraftUIPlugin.Log($"[CLIENT][CURSOR] {reason} {signature}");
            }
            catch (Exception ex)
            {
                DraftUIPlugin.LogError($"Failed to log cursor state: {ex}");
            }
        }

        [HarmonyPatch(typeof(UIManager), "UpdateMouseVisibility")]
        public static class UiManagerUpdateMouseVisibilityPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(UIManager __instance)
            {
                if (!UIInputState.isApprovalPopupInteractionOpen || !approvalInteractionActive)
                {
                    return true;
                }

                try
                {
                    uiManagerShowMouseMethod?.Invoke(__instance, null);
                    UIInputState.Sync(__instance);
                    UIInputState.isCursorLocked = false;
                    LogCursorState("approval-blocked-native-update-mouse-visibility");
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"Failed to suppress native mouse visibility update during approval override: {ex}");
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(UIManager), "HideMouse")]
        public static class UiManagerHideMousePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(UIManager __instance)
            {
                if (!UIInputState.isApprovalPopupInteractionOpen || !approvalInteractionActive)
                {
                    return true;
                }

                try
                {
                    uiManagerShowMouseMethod?.Invoke(__instance, null);
                    UIInputState.Sync(__instance);
                    UIInputState.isScoreboardOpen = false;
                    UIInputState.isCursorLocked = false;
                    LogCursorState("approval-blocked-native-hide-mouse");
                }
                catch (Exception ex)
                {
                    DraftUIPlugin.LogError($"Failed to suppress native HideMouse during approval override: {ex}");
                }

                return false;
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

            [HarmonyPatch(typeof(UIChat), "Client_SendClientChatMessage")]
            public static class UiChatClientSendClientChatMessagePatch
            {
                [HarmonyPrefix]
                public static bool Prefix(string message)
                {
                    if (TryHandleLocalTrainingChatCommand(message))
                    {
                        return false;
                    }

                    return !TrainingClientRuntime.TryHandleLocalChatCommand(message);
                }
            }

        [HarmonyPatch(typeof(UIManager), "Start")]
        public static class UiManagerStartPatch
        {
            [HarmonyPostfix]
            public static void Postfix(UIManager __instance)
            {
                EnsureMessagingHandlers();
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
                if (CanBeginApprovalPopupInteraction())
                {
                    approvalInteractionActive = true;
                    UIInputState.isScoreboardOpen = false;
                    SyncApprovalPopupInteractionCursor();
                    RefreshUi(forceRefresh: true);
                    return false;
                }

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
                if (approvalInteractionActive)
                {
                    NotifyScoreboardClosed();
                    return;
                }

                if (!IsBlockingGameplayUI())
                {
                    NotifyScoreboardOpened();
                    ScoreboardStarClientState.RefreshVisibleScoreboard();
                }
            }
        }

        [HarmonyPatch(typeof(UIManagerInputs), "OnScoreboardActionCanceled")]
        public static class UiManagerInputsScoreboardCanceledPatch
        {
            [HarmonyPrefix]
            public static bool Prefix()
            {
                if (!approvalInteractionActive)
                {
                    return true;
                }

                approvalInteractionActive = false;
                SyncApprovalPopupInteractionCursor();
                NotifyScoreboardClosed();
                RefreshUi(forceRefresh: true);
                return false;
            }

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

                DraftUIPlugin.Log($"[CLIENT][JOIN] Allowing native PlayerState RPC while overlay owns the screen. targetState={state} currentUi={currentUiState}");
                return true;
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

                DraftUIPlugin.Log($"[CLIENT][JOIN] Allowing native UIManagerStateController player-state update while overlay owns the screen. state={player.State.Value} currentUi={currentUiState}");
                return true;
            }

            [HarmonyPostfix]
            public static void Postfix(Dictionary<string, object> message)
            {
                if (!ShouldSuppressGameplaySelectionUi() || message == null || !message.TryGetValue("player", out var playerObject))
                {
                    return;
                }

                if (!(playerObject is Player player) || !player.IsLocalPlayer || !ShouldBlockGameplayState(player.State.Value))
                {
                    return;
                }

                DraftUIPlugin.Log($"[CLIENT][JOIN] Hiding native selection UI behind active overlay. state={player.State.Value} currentUi={currentUiState}");
                EnforceGameplayUiPriority();
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
            approvalInteractionActive = false;
            currentDraftState = DraftOverlayStateMessage.Hidden();
            currentDraftExtendedState = DraftOverlayExtendedMessage.Hidden();
            currentMatchResultState = MatchResultMessage.Hidden();
            cachedMatchResultState = MatchResultMessage.Hidden();
            postMatchDismissed = false;
            welcomePendingAcknowledgement = false;
            publicServerModeActive = false;
            trainingServerModeActive = false;
            TrainingClientRuntime.SetTrainingServerMode(false);
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